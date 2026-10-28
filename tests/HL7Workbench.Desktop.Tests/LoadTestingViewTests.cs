using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Transport;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class LoadTestingViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-load-ui-" + Guid.NewGuid().ToString("N"));
        public string Profiles => Path.Combine(Root, "transport-endpoints.json");
        public Files() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static LoadTestingOptions Fast => new() { MessagesPerSecond = 20, AttemptLimit = 2, DurationSeconds = 5 };
    private static async Task<MessageDocument> Source(DocumentManager documents)
    {
        const string first = "MSH|^~\\&|SYN-SEND|SYN-FAC|SYN-RECV|SYN-DEST|20300101120000||ADT^A01|SYN-LOAD-UI-1|T|2.5.1\rPID|1||SYNTHETIC\r";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(first + first.Replace("SYN-LOAD-UI-1", "SYN-LOAD-UI-2")));
        return await MessageFileReader.OpenFileAsync(documents, "synthetic-load.hl7", input, Token);
    }
    private static Task Done(Task task) => ObservableCompletion.TaskAsync(task, "Load command/execution/disposal", Token);
    private static Task Wait(Func<bool> condition, Func<string> diagnostic) => ObservableCompletion.UntilAsync(condition, diagnostic, Token);
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FullPositionReviewScrollsWithoutHidingConsentAndDefaultsToCancel(bool dark)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var owner = new Window { RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; owner.Show();
            var prompt = "I am authorized to load-test this exact destination using synthetic test data.\nCaptured positions:\n" + string.Join("\n", Enumerable.Range(1, 1000));
            var dialog = LoadTestingConfirmationDialog.Create("Review controlled load test", prompt); var answer = dialog.ShowDialog<bool>(owner); Pump(dialog);
            try
            {
                var scroll = dialog.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "LoadAuthorizationScroll");
                Assert.True(scroll.Extent.Height > scroll.Viewport.Height); Assert.Equal(prompt, ((SelectableTextBlock)scroll.Content!).Text);
                var buttons = dialog.GetVisualDescendants().OfType<Button>().ToArray(); var cancel = buttons.Single(b => Equals(b.Content, "Cancel"));
                Assert.True(cancel.IsFocused); Assert.True(cancel.Bounds.Height > 20); Click(cancel); Assert.False(await answer.WaitAsync(TimeSpan.FromSeconds(5), Token));
            }
            finally { dialog.Close(); owner.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false, 390)] [InlineData(true, 1100)]
    public async Task ThemedControlsRequireSyntheticAttestationReviewAndRunConsentAndExportSafeReports(bool dark, int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); await Source(documents);
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false); new TransportProfiles(files.Profiles).Save([new() { Port = listener.LocalEndpoint!.Port }]);
            using var model = new LoadTestingViewModel(documents, files.Profiles); var prompts = new List<string>(); model.ConfirmAsync = (_, text) => { prompts.Add(text); return Task.FromResult(true); };
            var view = new LoadTestingView(model); var window = new Window { Content = view, Width = width, Height = 950, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Pump(window); Assert.Equal("1", view.Concurrency.Text); Assert.Equal("1", view.Rate.Text); Assert.Equal("60", view.Duration.Text); Assert.False(view.Prepare.IsEnabled); Assert.Empty(listener.Receipts);
                Click(view.Prepare); await Done(view.ActionTask); Assert.Null(model.Session.Prepared); view.Synthetic.IsChecked = true; view.Rate.Text = "20"; view.Duration.Text = "5"; view.Attempts.Text = "2";
                Click(view.Prepare); await Done(view.ActionTask); Pump(window); Assert.NotNull(model.Session.Prepared); Assert.Empty(listener.Receipts);
                Click(view.Start); await Done(view.ActionTask); Assert.Contains("review", model.Status); Assert.Empty(listener.Receipts);
                Click(view.Review); await Done(view.ActionTask);
                await ObservableCompletion.RunAsync(model, async () =>
                    { Click(view.Start); Click(view.Start); await Done(view.ActionTask); await model.Session.Execution; },
                    () => model.Status.StartsWith("Completed:", StringComparison.Ordinal) && view.ExportCsv.IsEnabled && !model.Session.IsActive,
                    () => $"Load state {model.Session.Progress.State}; active {model.Session.IsActive}; busy {model.IsBusy}; export enabled {view.ExportCsv.IsEnabled}", Token);
                Pump(window);
                Assert.Equal(2, listener.Receipts.Count); Assert.Equal(2, model.Session.Progress.ApplicationAccepted); Assert.Equal(2, prompts.Count);
                Assert.All(prompts, p => { Assert.Contains("127.0.0.1", p); Assert.Contains("Global target: 20", p); Assert.Contains("authorized to load-test", p); Assert.Contains("identical MSH-10", p); });
                Assert.Contains("Target 20.00/s", view.Progress.Text); Assert.Contains("Application accepted 2", view.Progress.Text); Assert.Contains("p99", view.Latency.Text); Assert.Equal(2, view.Results.ItemCount);
                Assert.True(view.ExportCsv.IsEnabled); model.ChooseExportAsync = summary => Task.FromResult<FileSaveDestination?>(FileSaveDestination.NewFile(Path.Combine(files.Root, summary ? "summary.txt" : "report.csv")));
                Click(view.ExportCsv); await Done(view.ActionTask); Click(view.ExportSummary); await Done(view.ActionTask);
                Assert.DoesNotContain("SYN-", await File.ReadAllTextAsync(Path.Combine(files.Root, "report.csv"), Token)); Assert.DoesNotContain("127.0.0.1", await File.ReadAllTextAsync(Path.Combine(files.Root, "summary.txt"), Token));
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark; Pump(window); Assert.Equal(window.RequestedThemeVariant, view.ActualThemeVariant);
                Assert.True(view.Prepare.Bounds.Width > 30); Assert.True(view.Results.Bounds.Height >= 60); Assert.False(view.Start.IsEnabled);
            }
            finally { window.Close(); model.Dispose(); await Done(model.Disposal); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData("edit")] [InlineData("switch")] [InlineData("endpoint")] [InlineData("options")] [InlineData("selection")] [InlineData("attestation")]
    public async Task SourceOrConfigurationChangesInvalidateAuthorizationWithoutAnySend(string change)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); var source = await Source(documents);
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
            using var model = new LoadTestingViewModel(documents, files.Profiles) { SyntheticConfirmed = true }; model.Configure(new() { Port = listener.LocalEndpoint!.Port }, Fast); model.ConfirmAsync = (_, _) => Task.FromResult(true);
            await Done(model.PrepareAsync()); await Done(model.ReviewAsync());
            if (change == "edit") { source.IsEditMode = true; source.ReplaceWorkingText(source.EditableText! + "NTE|1||SYN-EDIT\r"); await Done(source.EditRefresh); }
            if (change == "switch") await Source(documents);
            if (change == "endpoint") model.Configure(model.Endpoint with { Port = listener.LocalEndpoint.Port == 65535 ? 2575 : listener.LocalEndpoint.Port + 1 }, Fast);
            if (change == "options") model.Configure(model.Endpoint, Fast with { Concurrency = 2 });
            if (change == "selection") model.InvalidateSelection();
            if (change == "attestation") model.SyntheticConfirmed = false;
            await Done(model.StartAsync()); Assert.Empty(listener.Receipts); Assert.Equal(0, model.Session.Progress.Attempts); Assert.Contains("review", model.Status);
            model.Dispose(); await Done(model.Disposal); return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancelOrToolCloseDuringAuthorizationPreventsLateConfirmationFromStarting(bool close)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); await Source(documents);
            using var model = new LoadTestingViewModel(documents, files.Profiles) { SyntheticConfirmed = true }; await Done(model.PrepareAsync());
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reply = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            model.ConfirmAsync = (_, _) => { entered.TrySetResult(); return reply.Task; }; var reviewing = model.ReviewAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            if (close) model.Dispose(); else model.Stop(); await Done(reviewing); reply.TrySetResult(true); await Done(model.StartAsync()); Assert.Equal(0, model.Session.Progress.Attempts);
            model.Dispose(); await Done(model.Disposal); return true;
        }, Token);
    }
    [Fact]
    public async Task MalformedSourceIsRejectedWithoutContentDisclosureOrFailureDuringToolDisposal()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN-PRIVATE-BAD-HEADER\rPID|1||SYN-PRIVATE\r"));
            await MessageFileReader.OpenFileAsync(documents, "SYN-PRIVATE.hl7", input, Token);
            using var model = new LoadTestingViewModel(documents, files.Profiles) { SyntheticConfirmed = true };
            await Done(model.PrepareAsync()); Assert.Null(model.Session.Prepared); Assert.Equal(0, model.Session.Progress.Attempts);
            Assert.DoesNotContain("SYN-PRIVATE", model.Status); Assert.Contains("unavailable", model.Status);
            model.Dispose(); await Done(model.Disposal); Assert.Equal(LoadTestingState.Disposed, model.Session.Progress.State); return true;
        }, Token);
    }
    [Fact]
    public async Task NonLoopbackNeedsAdditionalConsentEvenForSavedTestProfilesAndTlsNeverFallsBack()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); await Source(documents);
            new TransportProfiles(files.Profiles).Save([new() { Host = "192.0.2.1", TestEndpoint = true }]);
            using var model = new LoadTestingViewModel(documents, files.Profiles) { SyntheticConfirmed = true }; Assert.Equal("127.0.0.1", model.Endpoint.Host);
            model.Configure(model.Profiles[0], Fast); var prompts = new List<string>(); model.ConfirmAsync = (title, prompt) => { prompts.Add(title + " " + prompt); return Task.FromResult(!title.Contains("non-loopback")); };
            await Done(model.PrepareAsync()); await Done(model.ReviewAsync()); await Done(model.StartAsync()); Assert.Equal(3, prompts.Count); Assert.Equal(0, model.Session.Progress.Attempts);
            Assert.Contains("192.0.2.1", prompts[2]); Assert.Contains("do not establish authorization", prompts[2]);
            model.Configure(model.Endpoint with { Security = TransportSecurity.TlsUnavailable }, Fast); await Done(model.PrepareAsync()); Assert.Equal(0, model.Session.Progress.Attempts);
            model.Dispose(); await Done(model.Disposal); return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReconstructedViewRetainsLockedConfigurationAndItsSingleOwnedRun(bool deferObservation)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); await Source(documents);
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = TestAckMode.None }, false);
            using var model = new LoadTestingViewModel(documents, files.Profiles) { SyntheticConfirmed = true }; model.Configure(new() { Port = listener.LocalEndpoint!.Port, AckTimeoutSeconds = 5 }, Fast with { Concurrency = 2 }); model.ConfirmAsync = (_, _) => Task.FromResult(true);
            using var observation = new UiObservationBarrier();
            var view = new LoadTestingView(model); Assert.Equal(2, model.Options.Concurrency); Assert.Equal(20, model.Options.MessagesPerSecond);
            var window = new Window { Content = view, Width = 700, Height = 900 }; window.Show();
            try
            {
                await Done(model.PrepareAsync()); await Done(model.ReviewAsync());
                await Done(deferObservation ? observation.Start(model.StartAsync) : model.StartAsync());
                await Wait(() => listener.Receipts.Count == 2, () => $"Load receipts {listener.Receipts.Count}; attempts {model.Session.Progress.Attempts}");
                var execution = model.Session.Execution; var prepared = model.Session.Prepared;
                window.Content = new TextBlock(); Pump(window); Assert.Equal(0, view.Results.ItemCount); Assert.True(model.Session.IsActive);
                var next = new LoadTestingView(model); window.Content = next; Pump(window); Assert.Equal("2", next.Concurrency.Text); Assert.Equal("20", next.Rate.Text); Assert.True(next.Stop.IsEnabled); Assert.False(next.Prepare.IsEnabled);
                Assert.Same(execution, model.Session.Execution); Assert.Same(prepared, model.Session.Prepared); Assert.Equal(2, model.Session.Progress.Attempts);
                window.Height = 350; Pump(window); Assert.InRange(next.Stop.TranslatePoint(new Point(0, 0), window)!.Value.Y, 0, window.Height - next.Stop.Bounds.Height);
                Assert.Throws<InvalidOperationException>(() => model.Configure(model.Endpoint, Fast));
                await ObservableCompletion.RunAsync(model, async () =>
                {
                    Click(next.Stop); await model.Session.Execution;
                    if (deferObservation)
                    {
                        await observation.Queued.WaitAsync(TimeSpan.FromSeconds(5), Token);
                        Assert.Equal(LoadTestingState.Canceled, model.Session.Progress.State);
                        Assert.False(model.Status.StartsWith("Canceled:", StringComparison.Ordinal));
                        observation.Dispose();
                    }
                }, () => model.Status.StartsWith("Canceled:", StringComparison.Ordinal) && !model.Session.IsActive && next.ExportCsv.IsEnabled && next.Results.ItemCount == 2,
                    () => $"Load state {model.Session.Progress.State}; active {model.Session.IsActive}; results {next.Results.ItemCount}; export enabled {next.ExportCsv.IsEnabled}", Token);
                Pump(window);
                Assert.Equal(2, model.Session.Progress.Canceled); Assert.Equal(2, model.Session.Progress.Uncertain); Assert.Equal(2, next.Results.ItemCount); Assert.Equal(2, listener.Receipts.Count);
                Assert.Equal(LoadTestingState.Canceled, model.Session.Progress.State); Assert.True(next.Prepare.IsEnabled);
                Assert.Same(execution, model.Session.Execution); Assert.Same(prepared, model.Session.Prepared); Assert.False(next.Start.IsEnabled);
                await Wait(() => listener.Clients.Count == 0, () => $"Load connected clients {listener.Clients.Count}");
            }
            finally { observation.Dispose(); window.Close(); model.Dispose(); await Done(model.Disposal); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData("close-tool")] [InlineData("reset")] [InlineData("close-window")]
    public async Task ActualDockAndWindowClosureCancelAndDrainWithoutRestartingOrInitializingOtherSenders(string action)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); var window = new MainWindow(files.Root); window.Show(); Pump(window);
            var dock = window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!; var factory = (WorkspaceDockFactory)dock.Layout!.Factory!;
            var documents = (DocumentManager)typeof(MainWindow).GetField("documents", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!; var source = await Source(documents); factory.OpenDocument(source.Id);
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.LoadTestingId)!; Assert.Null(tool.Context);
            Assert.Null(factory.GetDockable<ITool>(WorkspaceDockFactory.BatchTransmissionId)!.Context); Assert.Null(factory.GetDockable<ITool>(WorkspaceDockFactory.ConnectionManagerId)!.Context);
            await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = TestAckMode.None }, false);
            factory.ShowTool(tool.Id!); var model = Assert.IsType<LoadTestingViewModel>(tool.Context); model.SyntheticConfirmed = true; model.Configure(new() { Port = listener.LocalEndpoint!.Port, AckTimeoutSeconds = 5 }, Fast); model.ConfirmAsync = (_, _) => Task.FromResult(true);
            await Done(model.PrepareAsync()); await Done(model.ReviewAsync()); await Done(model.StartAsync());
            await Wait(() => listener.Receipts.Count == 1, () => $"Load receipts {listener.Receipts.Count}; attempts {model.Session.Progress.Attempts}");
            var layout = LayoutCodec.Encode(factory.Capture()); Assert.DoesNotContain("127.0.0.1", layout); Assert.DoesNotContain(model.Session.Prepared!.Id.ToString(), layout);
            if (action == "close-tool") factory.CloseDockable(tool);
            if (action == "reset") typeof(MainWindow).GetMethod("ResetLayout", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
            if (action == "close-window") await ObservableCompletion.CloseAsync(window, Token);
            await Done(model.Disposal);
            await Wait(() => listener.Clients.Count == 0, () => $"Load connected clients {listener.Clients.Count}");
            Assert.Single(listener.Receipts); Assert.Equal(LoadTestingState.Disposed, model.Session.Progress.State);
            if (action == "close-tool") { factory.ShowTool(tool.Id!); var next = Assert.IsType<LoadTestingViewModel>(tool.Context); Assert.NotSame(model, next); Assert.Equal(LoadTestingState.Empty, next.Session.Progress.State); Assert.False(next.SyntheticConfirmed); }
            if (action != "close-window") await ObservableCompletion.CloseAsync(window, Token);
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DockFloatAndResizeRestoreOnlyPlacementWithoutRunSourceOrAuthorization(bool floating)
    {
        using var files = new Files(); using var documents = new DocumentManager(); await Source(documents);
        var tools = new ToolWindowRegistry(); tools.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
        tools.Register(new(WorkspaceDockFactory.LoadTestingId, "Load Testing", () => new LoadTestingViewModel(documents, files.Profiles), true, true));
        using var factory = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.LoadTestingId); var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.LoadTestingId)!;
        var old = Assert.IsType<LoadTestingViewModel>(tool.Context); old.SyntheticConfirmed = true; await Done(old.PrepareAsync());
        if (floating) factory.FloatDockable(tool); ((IToolDock)tool.Owner!).Proportion = .31;
        var layout = factory.Capture(); var json = LayoutCodec.Encode(layout); Assert.DoesNotContain(old.Session.Prepared!.Id.ToString(), json); Assert.DoesNotContain("127.0.0.1", json); Assert.DoesNotContain("SYN-LOAD", json);
        factory.CloseDockable(tool); await Done(old.Disposal); factory.Restore(LayoutCodec.Decode(json), []);
        Assert.False(tools.IsCreated(WorkspaceDockFactory.LoadTestingId)); factory.ShowTool(WorkspaceDockFactory.LoadTestingId);
        var next = Assert.IsType<LoadTestingViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.LoadTestingId)!.Context); Assert.NotSame(old, next); Assert.Equal(LoadTestingState.Empty, next.Session.Progress.State); Assert.False(next.SyntheticConfirmed);
        var placement = Assert.Single(factory.Capture().ToolPlacements!, p => p.Id == WorkspaceDockFactory.LoadTestingId); Assert.Equal(floating, placement.Floating is not null);
        if (floating) Assert.Equal(Assert.Single(layout.ToolPlacements!, p => p.Id == WorkspaceDockFactory.LoadTestingId).Floating, placement.Floating); else Assert.Equal(.31, placement.Proportion);
        factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.LoadTestingId)!); await Done(next.Disposal);
        factory.CloseFloatingWindows();
    }
}
