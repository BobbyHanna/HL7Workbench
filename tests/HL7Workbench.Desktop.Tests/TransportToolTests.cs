using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Transport;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class TransportToolTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private const string Message = "MSH|^~\\&|SYN-SEND|SYN-FAC|SYN-RECV|SYN-DEST|20300101120000||ADT^A01|SYN-DESKTOP|T|2.5.1\rPID|1||SYNTHETIC\r";
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-transport-ui-" + Guid.NewGuid().ToString("N"));
        public Files() => Directory.CreateDirectory(Root);
        public string Profiles => Path.Combine(Root, "transport-endpoints.json");
        public void Dispose() { foreach (var file in Directory.GetFiles(Root)) File.Delete(file); Directory.Delete(Root); }
    }
    [Fact]
    public async Task HumanConfirmationAfterIdleConnectionExpiresStillSendsOnceAndReceivesCorrelatedAa()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager();
            var text = Message + "NTE|1||" + new string('X', 504 - Message.Length - 8) + "\r";
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(text, 11))));
            var source = await MessageFileReader.OpenFileAsync(documents, "synthetic.hl7", input, Token);
            Assert.Equal(11, source.MessageCount);
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0, IdleTimeoutSeconds = 1 }, false);
            using var model = new TransportToolViewModel(documents, files.Profiles, false);
            model.Draft = model.Draft with { Port = listener.LocalEndpoint!.Port };
            var reviewing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var confirm = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            model.ConfirmAsync = (_, prompt) => { Assert.Contains("504 bytes", prompt); reviewing.TrySetResult(); return confirm.Task; };
            try
            {
                await model.ConnectAsync(); var sending = model.SendAsync(); await reviewing.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
                // Advance by the actual listener deadline, not a guessed delay:
                // a person can spend longer than the idle limit in this dialog.
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token); deadline.CancelAfter(5000);
                while (listener.Receipts.Count == 0) await Task.Delay(10, deadline.Token);
                Assert.Contains("timeout", listener.Receipts[0].Status); Assert.Empty(listener.Receipts[0].Payload);
                confirm.SetResult(true); await sending;
                var row = Assert.Single(model.Session.History); Assert.Equal("ApplicationAccepted", row.Status);
                Assert.True(row.WriteStarted); Assert.True(row.WriteCompleted); Assert.Equal("Outbound", row.Direction);
                Assert.Contains("write completed", model.Session.Status); Assert.Contains("ACK frames received: 1", model.Session.Status);
                Assert.Contains("application accepted (correlated AA)", model.Session.Status);
                var receipt = Assert.Single(listener.Receipts, r => r.Payload.Length > 0);
                Assert.Equal(Encoding.UTF8.GetBytes(text), receipt.Payload); Assert.Equal(new[] { "AA" }, receipt.AckCodes);
                Assert.Equal(507, receipt.ConnectionSocketBytesRead);
                var ack = Hl7Parser.Parse(Assert.Single(model.Session.History[0].Acknowledgments)); Assert.Equal("SYN-DESKTOP", ack.Get("MSA-2").Raw);
                Assert.False(model.Session.IsConnected);
                while (listener.Clients.Count != 0) await Task.Delay(10, deadline.Token);
            }
            finally { confirm.TrySetResult(false); model.Dispose(); await model.Disposal; }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancelOrCloseDuringHumanReviewNeverWritesOrPublishesSuccess(bool close)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); MessageFileReader.Open(documents, "synthetic.hl7", Hl7Parser.Parse(Message));
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
            using var model = new TransportToolViewModel(documents, files.Profiles, false); model.Draft = model.Draft with { Port = listener.LocalEndpoint!.Port };
            var reviewing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var confirm = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            model.ConfirmAsync = (_, _) => { reviewing.TrySetResult(); return confirm.Task; };
            await model.ConnectAsync(); var sending = model.SendAsync(); await reviewing.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            if (close) model.Dispose(); else model.Cancel(); confirm.TrySetResult(true); await sending;
            Assert.Empty(listener.Receipts); Assert.False(model.Session.IsConnected);
            Assert.All(model.Session.History, row => { Assert.False(row.WriteStarted); Assert.False(row.WriteCompleted); Assert.Empty(row.Acknowledgments); Assert.NotEqual("ApplicationAccepted", row.Status); });
            model.Dispose(); await model.Disposal; return true;
        }, Token);
    }
    [Fact]
    public async Task FailedConfirmedConnectionIsNotLabeledSentOrRetried()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); MessageFileReader.Open(documents, "synthetic.hl7", Hl7Parser.Parse(Message));
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
            using var model = new TransportToolViewModel(documents, files.Profiles, false);
            model.Draft = model.Draft with { Port = listener.LocalEndpoint!.Port, ConnectTimeoutSeconds = 1 };
            await model.ConnectAsync(); model.ConfirmAsync = async (_, _) => { await listener.StopAsync(); return true; };
            await model.SendAsync(); var row = Assert.Single(model.Session.History);
            Assert.True(row.Status is nameof(ExchangeOutcome.ConnectionFailed) or nameof(ExchangeOutcome.TimedOut));
            Assert.Equal("Outbound", row.Direction); Assert.False(row.WriteStarted); Assert.False(row.WriteCompleted); Assert.Empty(row.Acknowledgments);
            Assert.Equal(ExchangeFailureStage.Connection, row.FailureStage); Assert.Contains("write not attempted", model.Status); Assert.Contains("acceptance not established", model.Status);
            Assert.Empty(listener.Receipts); Assert.False(model.Session.IsConnected); Assert.Contains(documents.Activity.Snapshot(), e => e.Kind == ActivityKind.TransportFailed);
            model.Dispose(); await model.Disposal; return true;
        }, Token);
    }
    [Theory]
    [InlineData(false, 390)] [InlineData(true, 1100)]
    public async Task ThemedDockableControlsPersistProfilesAndExplicitlySendOneMessage(bool dark, int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager();
            var source = MessageFileReader.Open(documents, "synthetic.hl7", Hl7Parser.Parse(Message));
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
            using var model = new TransportToolViewModel(documents, files.Profiles, false); var confirmations = new List<string>();
            model.ConfirmAsync = (title, prompt) => { confirmations.Add(title + "\n" + prompt); return Task.FromResult(true); };
            var view = new TransportToolView(model); var window = new Window { Content = view, Width = width, Height = 900, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Pump(window); Assert.False(view.Send.IsEnabled); Assert.True(view.Connect.IsEnabled); Assert.Empty(model.Session.History);
                view.NameField.Text = "Synthetic loopback"; view.Port.Text = listener.LocalEndpoint!.Port.ToString(); Click(view.Save);
                var saved = Assert.Single(new TransportProfiles(files.Profiles).Load()); Assert.Equal("Synthetic loopback", saved.Name); Assert.Equal(listener.LocalEndpoint.Port, saved.Port);
                Click(view.Connect); await view.ActionTask; Pump(window); Assert.True(model.Session.IsConnected); Assert.True(view.Send.IsEnabled); Assert.Empty(listener.Receipts);
                Click(view.Send); Click(view.Send); await view.ActionTask; Pump(window);
                Assert.Single(model.Session.History); Assert.Single(listener.Receipts); Assert.Single(confirmations); Assert.Contains("Message count: 1", confirmations[0]); Assert.Contains("127.0.0.1", confirmations[0]);
                Assert.Equal("ApplicationAccepted", model.Session.History[0].Status); Assert.False(view.Send.IsEnabled); Assert.False(model.Session.IsConnected);
                view.History.SelectedItem = model.Session.History[0]; Pump(window); Assert.True(view.OpenAck.IsEnabled); Click(view.OpenAck); await view.ActionTask;
                var ack = documents.ActiveDocument!; Assert.NotSame(source, ack); Assert.True(ack.IsDirty); Assert.Equal("SYN-DESKTOP", ack.Message.Get("MSA-2").Raw);
                Assert.DoesNotContain("SYN-DESKTOP", string.Join("\n", documents.Activity.Snapshot()));
                Assert.True(view.History.Bounds.Height >= 60); Assert.True(view.Send.Bounds.Width > 50);
            }
            finally { window.Close(); model.Dispose(); await model.Disposal; }
            return true;
        }, Token);
    }
    [Fact]
    public async Task SendReviewCancellationAndTabSwitchPreventTransmission()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); var first = MessageFileReader.Open(documents, "synthetic.hl7", Hl7Parser.Parse(Message));
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
            using var model = new TransportToolViewModel(documents, files.Profiles, false); model.Draft = model.Draft with { Port = listener.LocalEndpoint!.Port };
            await model.ConnectAsync(); Assert.True(model.Session.IsConnected);
            model.ConfirmAsync = (_, _) => Task.FromResult(false); await model.SendAsync(); Assert.Empty(listener.Receipts);
            model.ConfirmAsync = (_, _) => { MessageFileReader.Open(documents, "other.hl7", Hl7Parser.Parse(Message.Replace("SYN-DESKTOP", "SYN-OTHER"))); return Task.FromResult(true); };
            await model.SendAsync(); Assert.Empty(listener.Receipts); Assert.Empty(model.Session.History); Assert.NotSame(first, documents.ActiveDocument);
            model.Dispose(); await model.Disposal; return true;
        }, Token);
    }
    [Fact]
    public async Task EditDuringReviewIsRejectedAndNoMessageIdentifierReachesRoutineStatus()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); var source = MessageFileReader.Open(documents, "synthetic.hl7", Hl7Parser.Parse(Message)); source.IsEditMode = true;
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
            using var model = new TransportToolViewModel(documents, files.Profiles, false); model.Draft = model.Draft with { Port = listener.LocalEndpoint!.Port }; await model.ConnectAsync();
            model.ConfirmAsync = (_, _) => { source.ReplaceWorkingText(Message + "NTE|1||SYN-CHANGED\r"); return Task.FromResult(true); };
            await model.SendAsync(); Assert.Empty(listener.Receipts); Assert.Empty(model.Session.History); Assert.Contains("changed", model.Status); Assert.DoesNotContain("SYN-DESKTOP", model.Status);
            await source.EditRefresh; model.Dispose(); await model.Disposal; return true;
        }, Token);
    }
    [Fact]
    public async Task NonLoopbackWarningCannotBeBypassedByTestDesignationOrListenerDefault()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); using var sender = new TransportToolViewModel(documents, files.Profiles, false);
            var warnings = new List<string>(); sender.ConfirmAsync = (_, text) => { warnings.Add(text); return Task.FromResult(false); };
            sender.Draft = sender.Draft with { Host = "192.0.2.1", TestEndpoint = true }; await sender.ConnectAsync(); Assert.False(sender.Session.IsConnected);
            Assert.Contains("do not prove nonproduction", Assert.Single(warnings));
            using var receiver = new TransportToolViewModel(documents, files.Profiles, true); receiver.ConfirmAsync = sender.ConfirmAsync;
            await receiver.StartAsync(new() { BindAddress = IPAddress.Any, Port = 0 }); Assert.False(receiver.Listener.IsRunning); Assert.Equal(2, warnings.Count);
            Assert.Contains("0.0.0.0", warnings[1]); sender.Dispose(); receiver.Dispose(); await sender.Disposal; await receiver.Disposal; return true;
        }, Token);
    }
    [Fact]
    public async Task ListenerButtonsStartStopRestartAndRetainInspectableReceiptsAcrossViewReplacement()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); using var model = new TransportToolViewModel(documents, files.Profiles, true);
            var view = new TransportToolView(model); var window = new Window { Content = view, Width = 800, Height = 850 }; window.Show();
            try
            {
                view.Port.Text = "0"; // The service supports ephemeral test ports; the user-facing profile requires a real port.
                await model.StartAsync(new() { Port = 0 }); var port = model.Listener.LocalEndpoint!.Port; Pump(window); Assert.False(view.Start.IsEnabled); Assert.True(view.Stop.IsEnabled);
                using var sender = new MllpConnection(); await sender.ConnectAsync(new(new() { Port = port }, IPAddress.Loopback), false, Token); await sender.SendAsync(Encoding.UTF8.GetBytes(Message), true, false, Token);
                model.Refresh(); Pump(window); Assert.Single(model.Listener.Receipts); view.History.SelectedItem = model.Listener.Receipts[0]; Click(view.Open); await view.ActionTask; Assert.Equal(Message, documents.ActiveDocument!.Message.RawText);
                window.Content = new TextBlock(); Pump(window); Assert.Equal(0, view.History.ItemCount); Assert.True(model.Listener.IsRunning);
                var replacement = new TransportToolView(model); window.Content = replacement; Pump(window); Assert.Single(replacement.History.Items);
                Click(replacement.Stop); await replacement.ActionTask; Pump(window); Assert.False(model.Listener.IsRunning); Assert.Empty(model.Listener.Clients);
                replacement.Port.Text = port.ToString(); Click(replacement.Start); await replacement.ActionTask; Pump(window); Assert.True(model.Listener.IsRunning);
                Assert.False(replacement.AckMode.IsEffectivelyEnabled);
            }
            finally { window.Close(); model.Dispose(); await model.Disposal; }
            return true;
        }, Token);
    }
    [Fact]
    public async Task DockToolsAreLazyReleaseNetworkOnCloseAndPersistOnlyPlacement()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); var window = new MainWindow(files.Root); window.Show(); Pump(window);
            try
            {
                var dock = window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!; var factory = (WorkspaceDockFactory)dock.Layout!.Factory!;
                var senderTool = factory.GetDockable<ITool>(WorkspaceDockFactory.ConnectionManagerId)!; var listenerTool = factory.GetDockable<ITool>(WorkspaceDockFactory.MllpListenerId)!;
                Assert.Null(senderTool.Context); Assert.Null(listenerTool.Context); Assert.False(File.Exists(files.Profiles));
                factory.ShowTool(WorkspaceDockFactory.ConnectionManagerId); factory.ShowTool(WorkspaceDockFactory.MllpListenerId); Pump(window);
                var sender = Assert.IsType<TransportToolViewModel>(senderTool.Context); var receiver = Assert.IsType<TransportToolViewModel>(listenerTool.Context);
                Assert.False(sender.Session.IsConnected); Assert.False(receiver.Listener.IsRunning); await receiver.StartAsync(new() { Port = 0 });
                var layout = LayoutCodec.Encode(factory.Capture()); Assert.DoesNotContain("127.0.0.1", layout); Assert.DoesNotContain("MSH|", layout); Assert.DoesNotContain("AckMode", layout);
                factory.CloseDockable(listenerTool); await receiver.Disposal; Assert.False(receiver.Listener.IsRunning); Assert.Null(listenerTool.Context);
                factory.ShowTool(WorkspaceDockFactory.MllpListenerId); Pump(window); var reconstructed = Assert.IsType<TransportToolViewModel>(listenerTool.Context); Assert.NotSame(receiver, reconstructed); Assert.False(reconstructed.Listener.IsRunning);
                factory.CloseDockable(senderTool); await sender.Disposal; Assert.Empty(sender.Session.History);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task CorruptProfilesArePreservedAndNeverAutoConnectOrOverwrite()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); const string json = "{\"Version\":1,\"Profiles\":[],\"Password\":\"SYN-SECRET\"}"; File.WriteAllText(files.Profiles, json);
            using var documents = new DocumentManager(); using var model = new TransportToolViewModel(documents, files.Profiles, false);
            Assert.False(model.Session.IsConnected); Assert.Contains("preserved", model.Status); model.SaveProfile(); Assert.Equal(json, File.ReadAllText(files.Profiles)); Assert.DoesNotContain("SYN-SECRET", model.Status);
            model.Dispose(); await model.Disposal; return true;
        }, Token);
    }
}
