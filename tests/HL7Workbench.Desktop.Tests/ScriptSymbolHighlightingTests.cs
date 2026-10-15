using Avalonia.Controls;
using Avalonia.Headless;
using AvaloniaEdit;
using HL7Workbench.Desktop.Viewer;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ScriptSymbolHighlightingTests
{
    private static ScriptSymbolSpan[] Named(string source, ScriptSymbolSpan[] spans, string name) =>
        spans.Where(s => source.AsSpan(s.Offset, s.Length).SequenceEqual(name)).ToArray();

    [Fact]
    public void CompilerResolvesFacadeRelativeChainedExtensionAndLocalCallsWithoutColoringPropertiesOrDelegatesAsMethods()
    {
        const string source = """
            using Alias = System.String;
            Message.SetText("PID-5", "SYNTHETIC");
            foreach(var pid in Message.Segments("PID")) pid.SetText("5", "SYNTHETIC");
            Message.Segments("PID").First().Clear("19");
            var raw = Message.Read("PID-3").Raw;
            Action callback = () => {}; callback(); callback.Invoke();
            void Helper() { } Helper();
            Alias label = "SYNTHETIC";
            new Synthetic().Run();
            class Synthetic { public string Label { get; } public void Run() { } }
            """;
        var spans = ScriptSymbolClassifier.Classify(source, TestContext.Current.CancellationToken);
        foreach (var name in new[] { "SetText", "Segments", "First", "Clear", "Read", "Helper", "Run", "Invoke" })
        {
            var matches = Named(source, spans, name); Assert.NotEmpty(matches);
            Assert.All(matches, s => Assert.Equal(ScriptSymbolKind.Method, s.Kind));
        }
        foreach (var name in new[] { "Message", "pid", "Raw", "callback", "Label" })
            Assert.All(Named(source, spans, name), s => Assert.Equal(ScriptSymbolKind.Neutral, s.Kind));
        foreach (var name in new[] { "Action", "Synthetic" })
            Assert.All(Named(source, spans, name), s => Assert.Equal(ScriptSymbolKind.Type, s.Kind));
        Assert.Contains(Named(source, spans, "Alias"), s => s.Kind == ScriptSymbolKind.Type);
    }

    [Theory]
    [InlineData("foreach(var pid in Message.Segments(\"PID\")) { pid.SetText(\"5\", \"SYNTHETIC\"); pid.", "SetText")]
    [InlineData("Message.Clear(\"PID-19\");\nvar broken = ;", "Clear")]
    [InlineData("Message.Clear(\"PID-19\"); MissingType.MissingMethod();", "Clear")]
    public void IncompleteAndErroneousScriptsKeepResolvableSymbolsAndLeaveUnknownNamesNeutral(string source, string method)
    {
        var spans = ScriptSymbolClassifier.Classify(source, TestContext.Current.CancellationToken);
        Assert.Contains(Named(source, spans, method), s => s.Kind == ScriptSymbolKind.Method);
        Assert.All(Named(source, spans, "MissingMethod"), s => Assert.Equal(ScriptSymbolKind.Neutral, s.Kind));
        Assert.All(spans, s => Assert.InRange(s.Offset + s.Length, 1, source.Length));
    }

    [Fact]
    public void StringsCommentsRawStringsAndTypeNamesUseCompilerContextWithoutExecutingScript()
    {
        const string source = """"
            // Message.SetText("PID-5", "SYNTHETIC");
            string raw = """Message.SetText("PID-5", "SYNTHETIC")""";
            var text = $"synthetic {Message.Read("PID-3").Raw}";
            Environment.Exit(17);
            """";
        var spans = ScriptSymbolClassifier.Classify(source, TestContext.Current.CancellationToken);
        Assert.Single(spans, s => s.Kind == ScriptSymbolKind.Comment);
        Assert.Contains(spans, s => s.Kind == ScriptSymbolKind.String && source.Substring(s.Offset, s.Length).Contains("SetText", StringComparison.Ordinal));
        Assert.Equal(ScriptSymbolKind.Method, Assert.Single(Named(source, spans, "Read")).Kind);
        Assert.Equal(ScriptSymbolKind.Neutral, Assert.Single(Named(source, spans, "Raw")).Kind);
        Assert.Equal(ScriptSymbolKind.Type, Assert.Single(Named(source, spans, "Environment")).Kind);
        Assert.Equal(ScriptSymbolKind.Method, Assert.Single(Named(source, spans, "Exit")).Kind);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => ScriptSymbolClassifier.Classify(source, canceled.Token));
        Assert.Empty(ScriptSymbolClassifier.Classify(new string(' ', 65537), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StaleNoncooperativeResultsCannotReplaceNewerRangesAndBindingRunsOffUiThread()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var oldRelease = new ManualResetEventSlim(); using var newRelease = new ManualResetEventSlim();
            var oldStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var newStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = Environment.CurrentManagedThreadId; var ranOffUi = false;
            var editor = new TextEditor { Text = "old()" };
            var window = new Window { Width = 700, Height = 350, Content = editor }; window.Show();
            var overlay = new ScriptSemanticHighlighting(editor, ScriptEditorPalette.Dark, (text, _) =>
            {
                ranOffUi = Environment.CurrentManagedThreadId != thread;
                if (text.StartsWith("old", StringComparison.Ordinal)) { oldStarted.TrySetResult(); oldRelease.Wait(TestContext.Current.CancellationToken); }
                else { newStarted.TrySetResult(); newRelease.Wait(TestContext.Current.CancellationToken); }
                return [new(0, text.Length - 2, ScriptSymbolKind.Method)];
            }, TimeSpan.Zero);
            try
            {
                overlay.Start(); var old = overlay.Pending; await oldStarted.Task;
                editor.Text = "newer()"; var current = overlay.Pending; Assert.Empty(overlay.Spans);
                oldRelease.Set(); await old; await newStarted.Task;
                Assert.Empty(overlay.Spans); // Old result arrived, but the newer binder is still held.
                newRelease.Set(); await current;
                Assert.Equal(5, Assert.Single(overlay.Spans).Length); Assert.True(ranOffUi);
                overlay.Stop(); Assert.Empty(overlay.Spans);
            }
            finally { oldRelease.Set(); newRelease.Set(); overlay.Stop(); window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DetachCancelsPendingWorkAndFailuresFallBackWithoutPublishingExceptionContent()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var editor = new TextEditor { Text = "// synthetic draft" };
            var overlay = new ScriptSemanticHighlighting(editor, ScriptEditorPalette.Dark, (_, _) => throw new Exception("SYNTHETIC PRIVATE"), TimeSpan.Zero);
            overlay.Start(); await overlay.Pending;
            Assert.Empty(overlay.Spans); Assert.True(overlay.Pending.IsCompletedSuccessfully);
            editor.Text += "\n// newer"; var pending = overlay.Pending; overlay.Stop(); await pending;
            Assert.Empty(overlay.Spans); Assert.Equal("// synthetic draft\n// newer", editor.Text);
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
