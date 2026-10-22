using System.ComponentModel;
using System.Text.Json;
using HL7Workbench.Core;
using HL7Workbench.Transport;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Interactive review/picker coordination only; Workspace owns all scheduling and measurements.</summary>
public sealed class LoadTestingViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private readonly TransportProfiles profiles;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? actionCancellation;
    private bool busy, disposed, selectionChanged, syntheticConfirmed;
    private Guid? reviewed;
    private Task observation = Task.CompletedTask;
    public LoadTestingSession Session { get; }
    public IReadOnlyList<TransportEndpoint> Profiles { get; private set; } = [];
    public TransportEndpoint Endpoint { get; private set; } = new();
    public LoadTestingOptions Options { get; private set; } = new();
    public bool IsBusy => busy;
    public bool CanConfigure => !disposed && !busy && !Session.IsActive;
    public bool SyntheticConfirmed { get => syntheticConfirmed; set { if (syntheticConfirmed == value) return; syntheticConfirmed = value; reviewed = null; Changed(); } }
    public string Status { get; private set; } = "Confirm synthetic test data, prepare and review. Opening this tool never starts network activity.";
    public string Source => Session.Prepared is { } run ? $"Captured Document {run.DocumentNumber}, revision {run.Revision}, {run.SourceCount} messages" +
        (run.IsActive(documents) ? " · current active source" : Session.IsActive ? " · sending the authorized immutable snapshot; source changed/closed/inactive" : " · source stale/inactive: prepare again") :
        documents.ActiveDocument is { } doc ? $"Active Document {doc.ActivityNumber}, revision {doc.SearchRevision}, {doc.MessageCount} messages" : "Open a synthetic HL7 document.";
    public Func<string, string, Task<bool>> ConfirmAsync { get; set; } = (_, _) => Task.FromResult(false);
    public Func<bool, Task<FileSaveDestination?>> ChooseExportAsync { get; set; } = _ => Task.FromResult<FileSaveDestination?>(null);
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Released;
    public Task ActionTask { get; private set; } = Task.CompletedTask;
    public Task Disposal { get; private set; } = Task.CompletedTask;
    public LoadTestingViewModel(DocumentManager documents, string profilePath)
    {
        this.documents = documents; profiles = new(profilePath); Session = new(documents);
        ReloadProfiles(); documents.PropertyChanged += ContextChanged;
    }
    public void ReloadProfiles()
    {
        if (!CanConfigure) return;
        try { Profiles = profiles.Load(); Endpoint = Profiles.FirstOrDefault(p => p.Id == Endpoint.Id) ?? Profiles.FirstOrDefault(p => p.Host is "127.0.0.1" or "::1") ?? new(); reviewed = null; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { Profiles = []; Status = "Saved endpoint profiles unavailable. Configure an authorized test endpoint in Connection Manager."; }
        Changed();
    }
    public void Configure(TransportEndpoint endpoint, LoadTestingOptions options)
    {
        if (!CanConfigure) throw new InvalidOperationException("Stop and drain before changing configuration.");
        endpoint.Validate(); options.Validate(); Endpoint = endpoint; Options = options; reviewed = null; Changed();
    }
    public void InvalidateSelection() { selectionChanged = true; reviewed = null; Changed(); }
    private bool Matches(PreparedLoadTest run) => syntheticConfirmed && !selectionChanged && run.Endpoint.Profile == Endpoint && run.Options == Options && run.IsActive(documents);
    public Task PrepareAsync(int? first = null, int? last = null, IReadOnlyList<int>? positions = null) => RunAsync(async token =>
    {
        reviewed = null;
        if (!syntheticConfirmed) { Status = "Confirm that the captured source is synthetic test data before preparing."; return; }
        var source = documents.ActiveDocument ?? throw new InvalidOperationException("Open a synthetic source document.");
        var selection = BatchTransmissionSession.SelectPositions(source.MessageCount, first, last, positions);
        var destination = await ResolvedEndpoint.ResolveAsync(Endpoint, token);
        await Session.PrepareAsync(source, selection, destination, Options, token); selectionChanged = false;
        Status = "Immutable source and pinned destination prepared. Review does not open a socket.";
    });
    public Task ReviewAsync() => RunAsync(async token =>
    {
        reviewed = null; var run = Session.Prepared ?? throw new InvalidOperationException("Prepare first.");
        if (!Matches(run)) { Status = "Source or configuration changed; Prepare and review again."; return; }
        if (await ConfirmAsync("Review controlled load test", run.Review).WaitAsync(token) && !disposed && ReferenceEquals(run, Session.Prepared) && Matches(run))
        { reviewed = run.Id; Status = "Review recorded. Start requires explicit authorization of this exact test destination and run."; }
    });
    public Task StartAsync() => RunAsync(async token =>
    {
        var run = Session.Prepared ?? throw new InvalidOperationException("Prepare first.");
        if (reviewed != run.Id || !Matches(run)) { reviewed = null; Status = "Prepare and review this exact source/configuration before Start."; return; }
        if (!await ConfirmAsync("Authorize controlled load testing", run.Review).WaitAsync(token)) return;
        var external = run.Endpoint.Scope == AddressScope.Loopback || await ConfirmAsync("Authorize exact non-loopback load destination",
            TransportToolViewModel.Warning(run.Endpoint.Display) + "\nExplicit authorization to load-test this exact target is required. Private IPs and saved Test labels do not establish authorization.").WaitAsync(token);
        if (disposed || !external || reviewed != run.Id || !Matches(run)) return;
        token.ThrowIfCancellationRequested(); reviewed = null; Status = "Controlled test running. Target rate is a ceiling, not guaranteed throughput. No retries.";
        // The action token ends with this short command. The run belongs to the
        // model lifetime, so returning from Start cannot cancel its own exchanges.
        observation = ObserveAsync(Session.StartAsync(run.Id, true, external, lifetime.Token));
    });
    private async Task ObserveAsync(Task task)
    { await task; Status = $"{Session.Progress.State}: {Session.Progress.StopReason}. Review write/ACK/acceptance and uncertainty separately. No automatic retry."; Changed(); }
    public void Stop()
    { actionCancellation?.Cancel(); Session.Cancel(); reviewed = null; Status = "Stop requested; new attempts prevented and active operations canceled/drained. Delivery may be uncertain."; Changed(); }
    public Task ExportAsync(bool summaryOnly) => RunAsync(async token =>
    {
        var run = Session.Prepared; var destination = await ChooseExportAsync(summaryOnly).WaitAsync(token);
        if (disposed || destination is null || !ReferenceEquals(run, Session.Prepared)) return;
        await Session.ExportAsync(destination, summaryOnly, token); Status = "Privacy-safe local report exported explicitly. No HL7 or source/host identifiers included.";
    });
    private Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (disposed || busy) return Task.CompletedTask;
        return ActionTask = Run();
        async Task Run()
        {
            busy = true; Changed(); using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); actionCancellation = cancellation;
            try { await action(cancellation.Token); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or System.Net.Sockets.SocketException or OperationCanceledException or ArgumentException or InvalidOperationException or NotSupportedException or Hl7LimitException)
            { Status = "Load operation unavailable, canceled or stale. Review source, authorization and limits. Nothing retries automatically."; documents.Activity.Record(ActivityKind.TransportFailed); }
            finally { actionCancellation = null; busy = false; Changed(); }
        }
    }
    private void ContextChanged(object? sender, PropertyChangedEventArgs e) => Changed();
    public void Refresh() => Changed();
    private void Changed() { if (!disposed) PropertyChanged?.Invoke(this, new(null)); }
    public void Dispose()
    {
        if (disposed) return; disposed = true; lifetime.Cancel(); Session.Dispose(); documents.PropertyChanged -= ContextChanged; Disposal = DrainAsync();
    }
    private async Task DrainAsync() { await Task.WhenAll(ActionTask, observation, Session.Disposal); lifetime.Dispose(); Released?.Invoke(); }
}
