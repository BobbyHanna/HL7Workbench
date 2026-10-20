using System.ComponentModel;
using System.Globalization;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Dialog-local configuration; preview computes only identifiers, never message bodies.</summary>
internal sealed class SyntheticGenerationViewModel(DocumentManager documents, MessageDocument? target) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private SyntheticMessageType messageType;
    private string count = "1", seed = "42", timestamp = "2030-01-01T00:00:00+00:00", prefix = "SYN";
    private bool insertIntoTarget;
    private MessageInsertionPosition position = MessageInsertionPosition.End;
    private CancellationTokenSource? cancellation;
    public SyntheticMessageType MessageType { get => messageType; set { messageType = value; Changed(); } }
    public string Count { get => count; set { count = value; Changed(); } }
    public string Seed { get => seed; set { seed = value; Changed(); } }
    public string Timestamp { get => timestamp; set { timestamp = value; Changed(); } }
    public string Prefix { get => prefix; set { prefix = value; Changed(); } }
    public bool InsertIntoTarget { get => insertIntoTarget; set { insertIntoTarget = value; Changed(); } }
    public MessageInsertionPosition Position { get => position; set { position = value; Changed(); } }
    public bool HasTarget => target is not null;
    public string TargetLabel => target is null ? "No active document" : "Active document: " + target.Title;
    public bool IsBusy { get; private set; }
    public string Status { get; private set; } = "";
    public SyntheticGenerationResult? Result { get; private set; }
    public bool CanGenerate => !IsBusy && TryOptions(out _, out _);
    public string Preview => TryOptions(out var options, out var error) ?
        $"HL7 v2.5.1 · generator v{SyntheticMessageGenerator.Version}\n{options!.TypeLabel} · {options.Count} message(s) · " +
        (InsertIntoTarget ? TargetLabel + " · " + MessageInsertionWorkflow.Choices.Single(c => c.Position == Position).Label : "New unsaved document") +
        $"\nControl IDs: {options.ControlId(1)} … {options.ControlId(options.Count)}\nSynthetic examples only; no clinical or implementation-guide conformance claim." : error;
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
    private bool TryOptions(out SyntheticGenerationOptions? options, out string error)
    {
        options = null; error = "";
        if (!int.TryParse(Count, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) { error = "Enter a whole count from 1 to 1,000."; return false; }
        var seedValue = SyntheticGenerationOptions.DefaultSeed;
        if (!string.IsNullOrWhiteSpace(Seed) && !uint.TryParse(Seed, NumberStyles.None, CultureInfo.InvariantCulture, out seedValue))
        { error = "Seed must be an integer from 0 to 4,294,967,295, or blank for 42."; return false; }
        if (!DateTimeOffset.TryParseExact(Timestamp, ["yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss'Z'"],
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
        { error = "Use a timestamp with seconds and offset, e.g. 2030-01-01T00:00:00+00:00."; return false; }
        try { options = new SyntheticGenerationOptions { MessageType = MessageType, Count = number, Seed = seedValue, BaseTimestamp = at, Prefix = Prefix }.Normalize(); }
        catch (ArgumentException e) { error = e.Message; return false; }
        if (InsertIntoTarget)
        {
            if (target is null || !documents.Documents.Contains(target)) { error = "The captured destination is no longer open. Choose a new document."; return false; }
            if (target.InsertionUnavailableReason is { } reason) { error = reason; return false; }
            if (number > MessageFileIndex.MaxIndexedMessages - target.MessageCount) { error = "The complete batch would exceed the 100,000-message document limit."; return false; }
            if (target.ActiveMessageId is null && Position is MessageInsertionPosition.BeforeCurrent or MessageInsertionPosition.AfterCurrent)
            { error = "Select a current message or use Beginning/End."; return false; }
        }
        return true;
    }
    public async Task GenerateAsync()
    {
        if (IsBusy) return;
        if (!TryOptions(out var options, out var error)) { Status = error; Changed(); return; }
        IsBusy = true; Result = null; Status = "Generating…"; cancellation = new(); Changed();
        try
        {
            Result = await new SyntheticGeneration(documents).GenerateAsync(options!, InsertIntoTarget ? target : null, Position,
                new Progress<SyntheticGenerationProgress>(p => { if (IsBusy) { Status = $"{p.Phase}: {p.Completed:N0} / {p.Total:N0}"; Changed(); } }), cancellation.Token);
            Status = $"Generated {Result.MessageIds.Count} synthetic message(s).";
        }
        catch (OperationCanceledException) { Status = "Generation canceled; no batch published."; }
        catch (Hl7LimitException) { Status = "The complete batch exceeds a content, message, line, document or edit/history limit. Reduce the count or choose a new document."; }
        catch (InvalidOperationException e) { Status = e.Message + " Complete or undo unfinished drafts, use a safe insertion position, or choose a new document."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.EncoderFallbackException or NotSupportedException or ArgumentException)
        { Status = "Generation could not be published. Check temporary storage access and destination encoding, or choose a new document. Existing work is retained."; }
        finally { cancellation.Dispose(); cancellation = null; IsBusy = false; Changed(); }
    }
    public void Cancel() => cancellation?.Cancel();
}
