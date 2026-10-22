using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using HL7Workbench.Core;
using HL7Workbench.Transport;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Dialog coordination only; network protocols and source capture stay below Desktop.</summary>
public sealed class TransportToolViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private readonly TransportProfiles profiles;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? actionCancellation;
    private bool disposed, busy, profileFailure;
    private string status = "";
    private int observedReceipts;
    public bool IsListener { get; }
    public TransportSession Session { get; }
    public MllpListener Listener { get; } = new();
    public TransportEndpoint Draft { get; set; } = new();
    public IReadOnlyList<TransportEndpoint> Profiles { get; private set; } = [];
    public bool IsBusy => busy || Session.IsBusy;
    public bool CanConfigure => !IsBusy && !Session.IsConnected && !Listener.IsRunning;
    public string Status => status.Length > 0 ? status : IsListener ? Listener.IsRunning ? "Listening · " + Listener.LocalEndpoint : "Stopped · default loopback only" : Session.Status;
    public string ActiveMessage => documents.ActiveDocument is { } d ? $"Active document {d.ActivityNumber} · message {d.MessageNumber:N0} of {d.MessageCount:N0}. Blank position sends this message only." : "Open a document to select one message.";
    public string Clients => $"Clients: {Listener.Clients.Count} · refused/overflow: {Listener.RejectedClients}\n" + string.Join("\n", Listener.Clients);
    public Task ActionTask { get; private set; } = Task.CompletedTask;
    public Task Disposal { get; private set; } = Task.CompletedTask;
    public Func<string, string, Task<bool>> ConfirmAsync { get; set; } = (_, _) => Task.FromResult(false);
    public event Action<MessageDocument>? OpenedDocument;
    public event Action? Released;
    public event PropertyChangedEventHandler? PropertyChanged;
    public TransportToolViewModel(DocumentManager documents, string profilePath, bool listener)
    {
        this.documents = documents; IsListener = listener; profiles = new(profilePath); Session = new(documents);
        Session.PropertyChanged += ContextChanged; documents.PropertyChanged += ContextChanged;
        if (!listener)
        {
            try { Profiles = profiles.Load(); Draft = Profiles.FirstOrDefault() ?? new(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { status = "Profile file unavailable or invalid; existing file preserved. Correct it before saving profiles."; profileFailure = true; }
        }
    }
    private void ContextChanged(object? sender, PropertyChangedEventArgs e) => Changed();
    public void SelectProfile(TransportEndpoint profile) { if (!CanConfigure) return; Draft = profile; status = ""; Changed(); }
    public void NewProfile() { if (!CanConfigure) return; Draft = new(); status = "New profile; use Save profile to keep it."; Changed(); }
    public void SaveProfile(bool delete = false)
    {
        if (!CanConfigure || profileFailure) return;
        try
        {
            Draft.Validate(); var next = Profiles.Where(p => p.Id != Draft.Id).ToList(); if (!delete) next.Add(Draft);
            profiles.Save(next); Profiles = next; status = delete ? "Profile removed." : "Profile saved locally. Notes must contain no secrets or patient data."; Changed();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { status = "Unable to save endpoint profile; prior file retained."; Changed(); }
    }
    public Task ConnectAsync() => RunAsync(async token =>
    {
        if (Session.IsConnected) return;
        var endpoint = await ResolvedEndpoint.ResolveAsync(Draft, token);
        var confirmed = endpoint.Scope == AddressScope.Loopback || await ConfirmAsync("Non-loopback connection", Warning(endpoint.Display));
        if (!confirmed || disposed) return;
        await Session.ConnectAsync(endpoint, confirmed, token);
    });
    public Task SendAsync(int? position = null) => RunAsync(async token =>
    {
        var document = documents.ActiveDocument; var destination = Session.Destination;
        if (document is null || destination is null || !Session.IsConnected) { status = "Select a document and connect explicitly first."; return; }
        var capture = await document.CaptureTransportMessageAsync(position ?? document.MessageNumber, destination.Profile.Encoding, token);
        var review = destination.Display + $"\nDocument {document.ActivityNumber}, message {capture.MessageNumber}. Message count: 1. Payload: {capture.Length:N0} bytes." +
            (capture.Transcoded ? "\nExplicit encoding conversion: characters retained; source bytes differ." : "\nSource encoding retained.") +
            "\nAfter confirmation, close the checked connection, connect once to this reviewed address, and send this one message. MLLP framing is added only on the wire. No retry or address fallback. Connect and send once?";
        if (!await ConfirmAsync("Review one-message send", review) || disposed) return;
        var confirmed = destination.Scope == AddressScope.Loopback || await ConfirmAsync("Confirm non-loopback send", Warning(destination.Display));
        if (disposed || !confirmed || !ReferenceEquals(documents.ActiveDocument, document)) return;
        await Session.SendAsync(capture, true, confirmed, token);
    });
    public Task StartAsync(ListenerOptions options) => RunAsync(async _ =>
    {
        var confirmed = IPAddress.IsLoopback(options.BindAddress) || await ConfirmAsync("Expose test listener", Warning(options.BindAddress + ":" + options.Port));
        if (!confirmed || disposed) return;
        Listener.Start(options, confirmed); documents.Activity.Record(ActivityKind.TransportListenerStarted);
    });
    public Task StopAsync() => RunAsync(async _ => { await Listener.StopAsync(); documents.Activity.Record(ActivityKind.TransportListenerStopped); });
    public void Cancel() { actionCancellation?.Cancel(); Session.Disconnect(); status = "Canceled/disconnected. A write may already have reached its destination; no retry."; Changed(); }
    public void Refresh()
    {
        if (disposed) return;
        var count = Listener.Receipts.Count;
        if (count > observedReceipts) documents.Activity.Record(ActivityKind.TransportReceived, count: count - observedReceipts);
        observedReceipts = count; Changed();
    }
    public void ClearHistory() { Session.ClearHistory(); Listener.ClearReceipts(); observedReceipts = 0; Changed(); }
    public Task OpenAsync(object item, int? ack = null) => RunAsync(async token =>
    {
        var document = item switch
        {
            TransportHistoryEntry h => await Session.OpenPayloadAsync(h, ack, token),
            TransportReceipt { Payload.Length: > 0 } r => await TransportSession.OpenLocalPayloadAsync(documents, r.Payload, r.Encoding, "Received MLLP.hl7", token),
            _ => null
        };
        if (!disposed && document is not null) OpenedDocument?.Invoke(document);
    });
    private Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (disposed || IsBusy) return Task.CompletedTask;
        return ActionTask = Core();
        async Task Core()
        {
            busy = true; status = ""; Changed();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); actionCancellation = cancellation;
            try { await action(cancellation.Token); }
            catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ArgumentException or InvalidOperationException or NotSupportedException or Hl7LimitException)
            { status = "Transport operation unavailable, canceled or timed out. Review configuration, source and encoding. No retry."; documents.Activity.Record(ActivityKind.TransportFailed); }
            finally { actionCancellation = null; busy = false; Changed(); }
        }
    }
    public static string Warning(string destination) => destination + "\nThis is a non-loopback endpoint. Traffic may reach another system. Private IP addresses and a Test endpoint label do not prove nonproduction. Plain TCP is unencrypted. Confirm only an authorized test destination.";
    private void Changed() { if (!disposed) PropertyChanged?.Invoke(this, new(null)); }
    public void Dispose()
    {
        if (disposed) return; disposed = true; lifetime.Cancel(); Session.Dispose();
        Released?.Invoke();
        documents.PropertyChanged -= ContextChanged; Session.PropertyChanged -= ContextChanged;
        Disposal = DrainAsync();
    }
    private async Task DrainAsync()
    { await Listener.DisposeAsync(); await ActionTask; lifetime.Dispose(); }
}
