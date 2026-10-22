using System.ComponentModel;
using System.Text.Json;
using HL7Workbench.Core;
using HL7Workbench.Transport;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Only review, selection and document/picker coordination live in Desktop.</summary>
public sealed class BatchTransmissionViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private readonly TransportProfiles profiles;
    private readonly CancellationTokenSource lifetime = new();
    private bool busy, disposed, selectionChanged;
    private CancellationTokenSource? actionCancellation;
    private Task observation = Task.CompletedTask;
    private Guid? reviewed;
    private string status = "Prepare a captured batch; no network activity occurs before Start confirmation.";
    public BatchTransmissionSession Session { get; }
    public IReadOnlyList<TransportEndpoint> Profiles { get; private set; } = [];
    public TransportEndpoint Endpoint { get; private set; } = new();
    public BatchTransmissionOptions Options { get; private set; } = new();
    public bool IsBusy => busy;
    public bool CanConfigure => !busy && !disposed && !Session.IsActive;
    public string Status => status;
    public string Source => Session.Prepared is { } b ? $"Captured Document {b.DocumentNumber}, revision {b.Revision}, batch {b.Id}" +
        (b.IsActive(documents) ? " · source current and active" : Session.IsActive ? " · source changed/closed/inactive; sending the authorized immutable snapshot" : " · source changed/closed/inactive: prepare and review again") :
        documents.ActiveDocument is { } d ? $"Active Document {d.ActivityNumber}, revision {d.SearchRevision}, {d.MessageCount} messages" : "Open an HL7 source document.";
    public Func<string, string, Task<bool>> ConfirmAsync { get; set; } = (_, _) => Task.FromResult(false);
    public Func<Task<FileSaveDestination?>> ChooseExportAsync { get; set; } = () => Task.FromResult<FileSaveDestination?>(null);
    public event Action<MessageDocument>? OpenedDocument;
    public event Action? Released;
    public event PropertyChangedEventHandler? PropertyChanged;
    public Task ActionTask { get; private set; } = Task.CompletedTask;
    public Task Disposal { get; private set; } = Task.CompletedTask;
    public BatchTransmissionViewModel(DocumentManager documents, string profilePath)
    {
        this.documents = documents; profiles = new(profilePath); Session = new(documents);
        ReloadProfiles(); documents.PropertyChanged += ContextChanged;
    }
    public void ReloadProfiles()
    {
        if (!CanConfigure) return;
        try
        {
            Profiles = profiles.Load(); Endpoint = Profiles.FirstOrDefault(p => p.Id == Endpoint.Id) ?? Profiles.FirstOrDefault(p => p.Host is "127.0.0.1" or "::1") ?? Profiles.FirstOrDefault() ?? new();
            reviewed = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { Profiles = []; status = "Saved endpoint profiles unavailable; existing configuration preserved. Configure a test endpoint in Connection Manager."; }
        Changed();
    }
    public void Configure(TransportEndpoint endpoint, BatchTransmissionOptions options)
    {
        if (!CanConfigure) throw new InvalidOperationException("Cancel and drain before changing destination/configuration.");
        endpoint.Validate(); options.Validate(); Endpoint = endpoint; Options = options; reviewed = null; Changed();
    }
    public Task PrepareAsync(int? first = null, int? last = null, IReadOnlyList<int>? positions = null) => RunAsync(async token =>
    {
        reviewed = null;
        var source = documents.ActiveDocument ?? throw new InvalidOperationException("Open an HL7 document.");
        var selection = BatchTransmissionSession.SelectPositions(source.MessageCount, first, last, positions);
        var endpoint = await ResolvedEndpoint.ResolveAsync(Endpoint, token);
        await Session.PrepareAsync(source, selection, endpoint, Options, token);
        selectionChanged = false;
        status = $"Prepared {selection.Length} immutable message(s). Review before Start. No socket opened.";
    });
    public void InvalidateSelection() { selectionChanged = true; reviewed = null; Changed(); }
    private bool ConfigurationMatches(PreparedTransmissionBatch batch) => !selectionChanged && batch.Endpoint.Profile == Endpoint && batch.Options == Options && batch.IsActive(documents);
    public Task ReviewAsync() => RunAsync(async token =>
    {
        var batch = Session.Prepared ?? throw new InvalidOperationException("Prepare first."); reviewed = null;
        if (!ConfigurationMatches(batch)) { status = "Source or configuration changed; Prepare and review again."; return; }
        var accepted = await ConfirmAsync("Review captured batch", batch.Review).WaitAsync(token);
        if (!disposed && accepted && ReferenceEquals(batch, Session.Prepared) && ConfigurationMatches(batch))
        { reviewed = batch.Id; status = "Review recorded. Start still requires explicit run confirmation."; }
    });
    public Task StartAsync() => RunAsync(async token =>
    {
        var batch = Session.Prepared ?? throw new InvalidOperationException("Prepare first.");
        if (reviewed != batch.Id || !ConfigurationMatches(batch)) { reviewed = null; status = "Prepare and review the current source/configuration before Start."; return; }
        if (!await ConfirmAsync("Authorize batch transmission", batch.Review).WaitAsync(token)) return;
        var external = batch.Endpoint.Scope == AddressScope.Loopback ||
            await ConfirmAsync("Confirm exact non-loopback batch destination", TransportToolViewModel.Warning(batch.Endpoint.Display)).WaitAsync(token);
        if (disposed || !external || reviewed != batch.Id || !ConfigurationMatches(batch)) return;
        reviewed = null; status = "Authorized batch running. Inspect application acknowledgment separately from write completion.";
        // The engine owns the long-running task. Do not keep the action lock held:
        // Pause/Resume/Cancel and view replacement must remain available.
        token.ThrowIfCancellationRequested();
        observation = ObserveAsync(Session.StartAsync(batch.Id, true, external, lifetime.Token));
    });
    private async Task ObserveAsync(Task execution)
    {
        await execution;
        status = $"{Session.Progress.State}: execution ended. Inspect write, commit and application acknowledgment counts; no automatic retry.";
        Changed();
    }
    public void Pause() { Session.Pause(); status = "Pause requested: an active exchange may finish; no following message starts while paused."; Changed(); }
    public void Resume()
    {
        if (Session.Prepared is not { } b || b.Endpoint.Profile != Endpoint || b.Options != Options)
        { status = "Destination/batch changed; cancel, prepare and authorize a new run."; Changed(); return; }
        try { Session.Resume(b.Id); status = "Resumed remaining messages only. No retries."; }
        catch (InvalidOperationException) { status = "Resume unavailable: this batch is no longer paused. No messages resent."; }
        Changed();
    }
    public void Cancel() { actionCancellation?.Cancel(); Session.Cancel(); reviewed = null; status = "Cancellation requested. An interrupted exchange may have reached its destination; no retry."; Changed(); }
    public Task OpenAckAsync(BatchMessageResult row, int index) => RunAsync(async token =>
    {
        var document = await Session.OpenAckAsync(row, index, token);
        if (!disposed) OpenedDocument?.Invoke(document);
    });
    public Task ExportAsync() => RunAsync(async token =>
    {
        var batch = Session.Prepared; var destination = await ChooseExportAsync().WaitAsync(token);
        if (destination is null || disposed || !ReferenceEquals(batch, Session.Prepared)) return;
        await Session.ExportAsync(destination, token); status = "Privacy-safe CSV report exported explicitly; no HL7 payloads or identifiers included.";
    });
    public Task OpenMessageAsync(BatchMessageResult row) => RunAsync(async token =>
    {
        var document = await Session.OpenMessageAsync(row, token);
        if (!disposed) OpenedDocument?.Invoke(document);
    });
    private Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (disposed || busy) return Task.CompletedTask;
        return ActionTask = Run();
        async Task Run()
        {
            busy = true; Changed();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); actionCancellation = cancellation;
            try { await action(cancellation.Token); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Net.Sockets.SocketException or OperationCanceledException or ArgumentException or InvalidOperationException or NotSupportedException or Hl7LimitException)
            { status = "Batch operation unavailable, canceled or stale. Review source, limits and configuration; no retry."; documents.Activity.Record(ActivityKind.TransportFailed); }
            finally { actionCancellation = null; busy = false; Changed(); }
        }
    }
    private void ContextChanged(object? sender, PropertyChangedEventArgs e) => Changed();
    public void Refresh() => Changed();
    private void Changed() { if (!disposed) PropertyChanged?.Invoke(this, new(null)); }
    public void Dispose()
    {
        if (disposed) return; disposed = true; lifetime.Cancel(); Session.Dispose(); documents.PropertyChanged -= ContextChanged;
        Disposal = DrainAsync();
    }
    private async Task DrainAsync()
    { await Task.WhenAll(Session.Disposal, ActionTask, observation); lifetime.Dispose(); Released?.Invoke(); }
}
