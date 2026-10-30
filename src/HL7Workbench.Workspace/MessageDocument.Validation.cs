using System.ComponentModel;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public enum SchemaValidationState { NotApplied, Validating, Valid, IssuesFound, Stale, Incomplete, Unavailable }

public sealed partial class MessageDocument
{
    public const int MaximumAppliedSchemas = 128;
    private sealed record AppliedSchema(string Id, string Signature);
    private readonly Dictionary<MessageId, AppliedSchema> appliedSchemas = [];
    private readonly HashSet<Task> validationOperations = [];
    private readonly object validationOperationsSync = new();
    private CancellationTokenSource? validationRequest;
    private long validationGeneration;
    private (MessageId Id, long Content, long Revision)? validationKey;
    private ValidationProfileLibrary validationLibrary = new();
    private Func<EditorPreferences>? validationPreferences;
    public ValidationProfileLibrary ValidationLibrary => validationLibrary;
    public EditorPreferences ValidationPreferences => validationPreferences?.Invoke() ?? new();
    public SchemaValidationState ValidationState { get; private set; }
    public Hl7SchemaValidationResult? SchemaValidation { get; private set; }
    public string? AppliedSchemaId => ActiveMessageId is { } id ? appliedSchemas.GetValueOrDefault(id)?.Id : null;
    public string? AppliedSchemaName => AppliedSchemaId is { } id ? validationLibrary.Find(id)?.Profile.Name ?? "Unavailable profile" : null;
    public string ValidationStatus { get; private set; } = "No schema applied. Apply Schema explicitly to validate this message.";
    public Task ValidationRefresh { get; private set; } = Task.CompletedTask;
    public async Task<IReadOnlyList<Hl7ValidationProfile>> DetectSchemasAsync(CancellationToken token = default)
    {
        var currentMessage = message; var currentStructure = Structure;
        var detected = await ReadTextAsync(source => currentMessage is not null ? Hl7ValidationCatalog.Detect(currentMessage) :
            currentStructure is not null ? Hl7ValidationCatalog.Detect(currentStructure, source, searchEncoding) : [], token);
        return detected ?? [];
    }
    internal void AttachValidationLibrary(ValidationProfileLibrary library, Func<EditorPreferences> preferences)
    {
        validationLibrary.Changed -= ValidationLibraryChanged; validationLibrary = library; validationPreferences = preferences;
        validationLibrary.Changed += ValidationLibraryChanged;
    }
    private string Signature(Hl7ResolvedProfile resolved) => string.Join("|", resolved.Lineage.Select(id => id + ":" + (validationLibrary.Find(id)?.Revision ?? "missing")));
    /// <summary>Explicit, active-message-only application. Selections are session state and never change source or history.</summary>
    public Task ApplySchemaAsync(string profileId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (ActiveMessageId is not { } id || IsLoading) throw new InvalidOperationException("Select a prepared message before applying a schema.");
        var resolved = validationLibrary.Resolve(profileId);
        if (!appliedSchemas.ContainsKey(id) && appliedSchemas.Count >= MaximumAppliedSchemas) throw new InvalidOperationException("Applied-schema selection budget reached. Remove an existing selection first.");
        appliedSchemas[id] = new(profileId, Signature(resolved)); validationKey = null;
        ValidationContentChanged(); return ValidationRefresh;
    }
    public void RemoveSchema()
    {
        if (ActiveMessageId is { } id) appliedSchemas.Remove(id);
        CancelValidation(); validationKey = null; SchemaValidation = null;
        SetValidationState(SchemaValidationState.NotApplied, "No schema applied. Apply Schema explicitly to validate this message.");
    }
    private void CancelValidation()
    { validationGeneration++; validationRequest?.Cancel(); validationRequest?.Dispose(); validationRequest = null; }
    private void SetValidationState(SchemaValidationState state, string status)
    {
        ValidationState = state; ValidationStatus = status;
        PropertyChanged?.Invoke(this, new(nameof(SchemaValidation)));
    }
    private void ValidationLibraryChanged(object? sender, EventArgs e)
    {
        if (ActiveMessageId is not { } id || !appliedSchemas.TryGetValue(id, out var selection)) return;
        try { if (selection.Signature == Signature(validationLibrary.Resolve(selection.Id))) return; }
        catch (ArgumentException) { }
        validationKey = null; ValidationContentChanged();
    }
    private void ValidationContentChanged()
    {
        if (disposed) return;
        if (ActiveMessageId is not { } id || !appliedSchemas.TryGetValue(id, out var selection))
        {
            if (ValidationState != SchemaValidationState.NotApplied || validationRequest is not null)
            { CancelValidation(); validationKey = null; SchemaValidation = null; SetValidationState(SchemaValidationState.NotApplied, "No schema applied to this message."); }
            return;
        }
        if (IsLoading || IsRefreshingEdit)
        {
            CancelValidation(); validationKey = null; SchemaValidation = null;
            SetValidationState(SchemaValidationState.Stale, "Validation is stale while the active message structure refreshes."); return;
        }
        Hl7ResolvedProfile resolved;
        try
        {
            resolved = validationLibrary.Resolve(selection.Id);
            if (selection.Signature != Signature(resolved)) throw new ArgumentException("Applied profile changed.");
        }
        catch (ArgumentException)
        {
            CancelValidation(); SchemaValidation = null;
            SetValidationState(SchemaValidationState.Unavailable, "The applied profile or an inherited definition changed or is unavailable. Explicitly apply a current profile to resume."); return;
        }
        var key = (id, ContentVersion, SearchRevision);
        if (validationKey == key) return;
        CancelValidation(); validationKey = key; SchemaValidation = null;
        validationRequest = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        SetValidationState(SchemaValidationState.Validating, "Validating this message under the explicitly applied profile…");
        var operation = ValidateActiveAsync(key, resolved.Profile, validationGeneration, validationRequest.Token);
        ValidationRefresh = operation;
        lock (validationOperationsSync) validationOperations.Add(operation);
        _ = DrainOperationAsync(operation);
    }
    private async Task DrainOperationAsync(Task operation)
    { try { await operation; } finally { lock (validationOperationsSync) validationOperations.Remove(operation); } }
    private async Task ValidateActiveAsync((MessageId Id, long Content, long Revision) key, Hl7ValidationProfile profile, long serial, CancellationToken token)
    {
        bool Current() => !disposed && serial == validationGeneration && ActiveMessageId == key.Id && ContentVersion == key.Content && SearchRevision == key.Revision;
        try
        {
            // The editor's existing 180 ms structural debounce runs first. Yield
            // once here so Apply Schema never parses/evaluates on the UI thread.
            await Task.Yield(); token.ThrowIfCancellationRequested(); await work.WaitAsync(token);
            Hl7SchemaValidationResult result;
            try
            {
                if (!Current()) return;
                var text = EditableText; var structure = Structure; var source = Source;
                if (text is null && (structure is null || source is null)) throw new InvalidOperationException("Readable complete message structure is unavailable.");
                // Borrow the existing bounded spool under the document work lease.
                // No full-message/attachment materialization or second spool.
                source?.BindCancellation(token);
                try
                {
                    await scanConcurrency.WaitAsync(token);
                    try { result = await Task.Run(() => text is not null ? Hl7SchemaValidator.Validate(Hl7Parser.Parse(text, new() { Encoding = searchEncoding }), profile, token) :
                        Hl7SchemaValidator.Validate(structure!, source!, searchEncoding, profile, token), token); }
                    finally { scanConcurrency.Release(); }
                }
                finally { source?.BindCancellation(lifetime.Token); }
            }
            finally { work.Release(); }
            if (!Current() || token.IsCancellationRequested) return;
            SchemaValidation = result;
            var state = !result.Complete ? SchemaValidationState.Incomplete : result.Errors + result.Warnings > 0 ? SchemaValidationState.IssuesFound : SchemaValidationState.Valid;
            SetValidationState(state, !result.Complete ? "Validation incomplete; inspect findings and coverage notices." : result.Errors + result.Warnings > 0 ?
                "Issues found under the applied profile." : "No issues found under the applied profile.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception e) when (e is Hl7LimitException or ArgumentException or InvalidOperationException or IOException or ObjectDisposedException or System.Text.EncoderFallbackException)
        {
            if (Current()) { SchemaValidation = null; SetValidationState(SchemaValidationState.Unavailable, "Validation unavailable: source, profile or structural budget could not be prepared. Source bytes are retained."); }
        }
    }
    private Task[] StopValidation()
    {
        validationLibrary.Changed -= ValidationLibraryChanged; validationPreferences = null; CancelValidation();
        SchemaValidation = null; appliedSchemas.Clear();
        // Completion can drain on a pool thread when there is no UI context.
        // ICollection.ToArray reads Count before CopyTo; a concurrent removal
        // could otherwise leave a null task in the disposal snapshot.
        lock (validationOperationsSync) return validationOperations.ToArray();
    }
}
