using System.Net;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Dock.Model.Controls;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Transport;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class BatchTransmissionViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private const string Message = "MSH|^~\\&|SYN-SEND|SYN-FAC|SYN-RECV|SYN-DEST|20300101120000||ADT^A01|SYN-BATCH-UI|T|2.5.1\rPID|1||SYNTHETIC\r";
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-batch-ui-" + Guid.NewGuid().ToString("N"));
        public string Profiles => Path.Combine(Root, "transport-endpoints.json");
        public Files() => Directory.CreateDirectory(Root);
        public void Dispose() { foreach (var file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories)) File.Delete(file); foreach (var directory in Directory.GetDirectories(Root)) Directory.Delete(directory); Directory.Delete(Root); }
    }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static async Task<MessageDocument> Source(DocumentManager documents)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Message + Message.Replace("SYN-BATCH-UI", "SYN-BATCH-UI-2")));
        return await MessageFileReader.OpenFileAsync(documents, "synthetic.hl7", input, Token);
    }
    private static async Task Wait(Func<bool> condition)
    { using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token); deadline.CancelAfter(5000); while (!condition()) await Task.Delay(10, deadline.Token); }
    [Theory]
    [InlineData(false, 390)] [InlineData(true, 1100)]
    public async Task ThemedControlsPrepareReviewConfirmRunAndExportWithoutDuplicateStart(bool dark, int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); await Source(documents);
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
            new TransportProfiles(files.Profiles).Save([new() { Port = listener.LocalEndpoint!.Port }]);
            using var model = new BatchTransmissionViewModel(documents, files.Profiles); var prompts = new List<string>();
            model.ConfirmAsync = (_, prompt) => { prompts.Add(prompt); return Task.FromResult(true); };
            var view = new BatchTransmissionView(model); var window = new Window { Content = view, Width = width, Height = 900, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Pump(window); Assert.Equal("1000", view.Interval.Text); Assert.Equal(0, view.Policy.SelectedIndex); Assert.Empty(listener.Receipts);
                view.Interval.Text = "250"; Click(view.Prepare); await view.ActionTask; Pump(window); Assert.Equal(2, view.Results.ItemCount); Assert.Empty(listener.Receipts);
                Click(view.Start); await view.ActionTask; Assert.Empty(listener.Receipts); Assert.Contains("review", model.Status);
                Click(view.Review); await view.ActionTask;
                using var completionCancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
                var completion = ObservableCompletion.WaitAsync(model,
                    () => model.Status.StartsWith("Completed:", StringComparison.Ordinal) && view.Export.IsEnabled && !model.Session.IsActive,
                    () => $"Batch state {model.Session.Progress.State}; active {model.Session.IsActive}; busy {model.IsBusy}; export enabled {view.Export.IsEnabled}", completionCancellation.Token);
                try
                {
                    Click(view.Start); Click(view.Start); await view.ActionTask;
                    await completion; await model.Session.Execution; Pump(window);
                }
                finally { completionCancellation.Cancel(); }
                Assert.Equal(2, listener.Receipts.Count); Assert.Equal(2, model.Session.Progress.Accepted); Assert.Equal(2, prompts.Count);
                Assert.All(prompts, p => { Assert.Contains("127.0.0.1", p); Assert.Contains("Messages: 2", p); Assert.Contains("250 ms", p); Assert.Contains("StopOnUnsuccessful", p); });
                Assert.True(view.Export.IsEnabled); Assert.Contains("application accepted 2", view.Progress.Text);
                Assert.Contains("completed exchanges 2", view.Progress.Text); Assert.Contains("unsuccessful completed exchanges 0", view.Progress.Text);
                Assert.Contains("transport failures 0", view.Progress.Text); Assert.Contains("not attempted/skipped 0", view.Progress.Text);
                view.Results.SelectedIndex = 0; Pump(window); Click(view.OpenAck); await view.ActionTask; Assert.Equal(2, documents.Documents.Count); Assert.True(documents.ActiveDocument!.IsDirty);
                Click(view.OpenMessage); await view.ActionTask; Assert.Equal(3, documents.Documents.Count); Assert.Equal(Message, documents.ActiveDocument!.Message.RawText);
                model.Resume(); Assert.Equal(2, model.Session.Progress.Accepted); Assert.Contains("no longer paused", model.Status);
                model.ChooseExportAsync = () => Task.FromResult<FileSaveDestination?>(FileSaveDestination.NewFile(Path.Combine(files.Root, "report.csv")));
                Click(view.Export); await view.ActionTask; Assert.DoesNotContain("SYN-", await File.ReadAllTextAsync(Path.Combine(files.Root, "report.csv"), Token));
                Assert.True(view.Prepare.Bounds.Width > 20); Assert.True(view.Results.Bounds.Height >= 80);
            }
            finally { window.Close(); model.Dispose(); await model.Disposal; }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData("switch")] [InlineData("edit")] [InlineData("endpoint")] [InlineData("selection")] [InlineData("new-batch")]
    public async Task ChangesAfterReviewCannotReuseAuthorization(string change)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); var source = await Source(documents);
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
            using var model = new BatchTransmissionViewModel(documents, files.Profiles); model.Configure(new() { Port = listener.LocalEndpoint!.Port }, new() { IntervalMilliseconds = 250 }); model.ConfirmAsync = (_, _) => Task.FromResult(true);
            await model.PrepareAsync(); await model.ReviewAsync();
            if (change == "switch") MessageFileReader.Open(documents, "other.hl7", Hl7Parser.Parse(Message));
            if (change == "edit") { source.IsEditMode = true; source.ReplaceWorkingText(Message + "NTE|1||SYN-EDIT\r"); await source.EditRefresh; }
            if (change == "endpoint") model.Configure(model.Endpoint with { Port = listener.LocalEndpoint.Port + (listener.LocalEndpoint.Port == 65535 ? -1 : 1) }, model.Options);
            if (change == "selection") model.InvalidateSelection();
            if (change == "new-batch") await model.PrepareAsync(1, 1);
            await model.StartAsync(); Assert.Equal(0, model.Session.Progress.Attempted); Assert.Empty(listener.Receipts); Assert.Contains("review", model.Status);
            model.Dispose(); await model.Disposal; return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancelOrCloseDuringReviewCannotStartAfterLateConfirmation(bool close)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); await Source(documents);
            using var model = new BatchTransmissionViewModel(documents, files.Profiles); await model.PrepareAsync();
            var prompt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reply = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            model.ConfirmAsync = (_, _) => { prompt.TrySetResult(); return reply.Task; };
            var reviewing = model.ReviewAsync(); await prompt.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            if (close) model.Dispose(); else model.Cancel(); await reviewing; reply.TrySetResult(true); await model.StartAsync();
            Assert.Equal(0, model.Session.Progress.Attempted); model.Dispose(); await model.Disposal; return true;
        }, Token);
    }
    [Fact]
    public async Task NonLoopbackRequiresAdditionalExactDestinationConsentAndTlsNeverFallsBack()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); await Source(documents);
            using var model = new BatchTransmissionViewModel(documents, files.Profiles); model.Configure(new() { Host = "192.0.2.1", TestEndpoint = true }, new());
            var prompts = new List<string>(); model.ConfirmAsync = (title, prompt) => { prompts.Add(title + " " + prompt); return Task.FromResult(!title.Contains("non-loopback")); };
            await model.PrepareAsync(); await model.ReviewAsync(); await model.StartAsync(); Assert.Equal(0, model.Session.Progress.Attempted); Assert.Equal(3, prompts.Count);
            Assert.Contains("192.0.2.1", prompts[2]); Assert.Contains("do not prove nonproduction", prompts[2]);
            model.Configure(model.Endpoint with { Security = TransportSecurity.TlsUnavailable }, new()); await model.PrepareAsync(); Assert.Equal(0, model.Session.Progress.Attempted);
            model.Dispose(); await model.Disposal; return true;
        }, Token);
    }
    [Fact]
    public async Task DetachedViewsKeepLiveOwnershipAndCloseCancelsAnActiveExchange()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); await Source(documents);
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = TestAckMode.None }, false);
            using var model = new BatchTransmissionViewModel(documents, files.Profiles); model.Configure(new() { Port = listener.LocalEndpoint!.Port, AckTimeoutSeconds = 10 }, new()); model.ConfirmAsync = (_, _) => Task.FromResult(true);
            var view = new BatchTransmissionView(model); var window = new Window { Content = view, Width = 700, Height = 850 }; window.Show();
            try
            {
                await model.PrepareAsync(); await model.ReviewAsync(); await model.StartAsync(); await Wait(() => listener.Receipts.Count == 1);
                model.Pause(); Assert.Equal(BatchTransmissionState.PausePending, model.Session.Progress.State);
                window.Content = new TextBlock(); Pump(window); Assert.Equal(0, view.Results.ItemCount); Assert.True(model.Session.IsActive);
                var replacement = new BatchTransmissionView(model); window.Content = replacement; Pump(window); Assert.True(replacement.Resume.IsEnabled); Assert.False(replacement.Prepare.IsEnabled);
                Assert.Throws<InvalidOperationException>(() => model.Configure(model.Endpoint with { Port = 2576 }, model.Options));
                Click(replacement.Cancel); await model.Session.Execution; Pump(window); Assert.Equal(1, model.Session.Progress.Canceled); Assert.Equal(1, model.Session.Progress.NotAttempted);
                Assert.Contains("unsuccessful completed exchanges 1", replacement.Progress.Text); Assert.Contains("not attempted/skipped 1", replacement.Progress.Text);
                Assert.Single(listener.Receipts); await Wait(() => listener.Clients.Count == 0);
            }
            finally { window.Close(); model.Dispose(); await model.Disposal; }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData("close-tool")] [InlineData("reset")] [InlineData("close-window")]
    public async Task DockCloseResetAndApplicationShutdownDrainBatchesWithoutLayoutRestart(string action)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); var window = new MainWindow(files.Root); window.Show(); Pump(window);
            var dock = window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!; var factory = (WorkspaceDockFactory)dock.Layout!.Factory!;
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.BatchTransmissionId)!; Assert.Null(tool.Context);
            var documents = (DocumentManager)typeof(MainWindow).GetField("documents", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!; var source = await Source(documents); factory.OpenDocument(source.Id);
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = TestAckMode.None }, false);
            factory.ShowTool(WorkspaceDockFactory.BatchTransmissionId); Pump(window); var model = Assert.IsType<BatchTransmissionViewModel>(tool.Context);
            model.Configure(new() { Port = listener.LocalEndpoint!.Port, AckTimeoutSeconds = 10 }, new()); model.ConfirmAsync = (_, _) => Task.FromResult(true);
            await model.PrepareAsync(); await model.ReviewAsync(); await model.StartAsync(); await Wait(() => listener.Receipts.Count == 1);
            var layout = LayoutCodec.Encode(factory.Capture()); Assert.DoesNotContain("127.0.0.1", layout); Assert.DoesNotContain(model.Session.Prepared!.Id.ToString(), layout); Assert.DoesNotContain("SYN-BATCH", layout);
            if (action == "close-tool") factory.CloseDockable(tool);
            if (action == "reset")
            {
                typeof(MainWindow).GetMethod("ResetLayout", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
            }
            // Window close first cancels/drains, then reaches Closed and disposes the
            // models. Disposal is still the initial completed task before Closed.
            if (action == "close-window") { window.Close(); await Wait(() => !window.IsVisible); }
            await model.Disposal; await Wait(() => listener.Clients.Count == 0); Assert.Single(listener.Receipts); Assert.Equal(BatchTransmissionState.Disposed, model.Session.Progress.State);
            if (action == "close-tool") { factory.ShowTool(WorkspaceDockFactory.BatchTransmissionId); Pump(window); var replacement = Assert.IsType<BatchTransmissionViewModel>(tool.Context); Assert.NotSame(model, replacement); Assert.Equal(BatchTransmissionState.Empty, replacement.Session.Progress.State); }
            window.Close(); await Wait(() => !window.IsVisible); return true;
        }, Token);
    }
}
