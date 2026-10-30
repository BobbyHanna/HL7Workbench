using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ScriptEditorAssistanceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static (string Source, int Caret) Mark(string text)
    { var caret = text.IndexOf("$$", StringComparison.Ordinal); Assert.True(caret >= 0); return (text.Replace("$$", "", StringComparison.Ordinal), caret); }
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }

    [Theory]
    [InlineData("void Main(){ Message.SetText$$(\"PID-5\",\"SYNTHETIC\"); }", "set-text")]
    [InlineData("void Main(){ foreach(var pid in Message.Segments(\"PID\")) pid.Read$$(\"5\"); }", "segment-read")]
    [InlineData("void Main(){ Message.Segments(\"PID\").First().Clear$$(\"19\"); }", "segment-clear")]
    [InlineData("using I=HL7Workbench.Scripting.SyntheticIdentityLibrary; var g=new I(42); void Main(){g.Get$$(\"synthetic\",Cancellation);}", "identity-library")]
    [InlineData("var w=new DeidentificationWorkflow(new DeidentificationOptions(),Cancellation); void Main(){ w.Apply$$(Message); }", "workflow")]
    [InlineData("void Main(){ var x=Message.Read(\"PID-5\").Raw$$; }", "states")]
    [InlineData("void Main(){ Cancellation.ThrowIfCancellationRequested$$(); }", "globals")]
    [InlineData("void Main$$(){}", "lifecycle")]
    [InlineData("void Main(){ ScriptDeferredMessage$$ held; }", "hold")]
    [InlineData("void Main(){ ScriptWorkerSession$$ host; }", "host-infrastructure")]
    public void F1ResolvesActualGlobalsMethodsAliasesChainedAndRelativeReceivers(string marked, string topic)
    { var (source, caret) = Mark(marked); var help = ScriptApiLookup.Find(source, caret, Token); Assert.Contains(topic, help.Topics); Assert.NotEmpty(help.Signatures); }

    [Theory]
    [InlineData("void Main(){ Message.Nonexistent$$(); }")]
    [InlineData("void Main(){var s=\"Message.SetText$$\";}")]
    [InlineData("// Message.SetText$$\nvoid Main(){}")]
    [InlineData("void Main(){new ScriptMessage().SetText$$();} class ScriptMessage {public void SetText(){}}")]
    [InlineData("void Main(){ DateTime.Now$$.ToString(); }")]
    public void UnknownStringsCommentsBclAndUserShadowingNeverGuessBySpelling(string marked)
    { var (source, caret) = Mark(marked); var help = ScriptApiLookup.Find(source, caret, Token); Assert.Empty(help.Topics); Assert.Contains("No supported", help.Notice); }

    [Fact]
    public void TypesAndInvalidOverloadCallsPresentChoicesRatherThanInventingAnOverload()
    {
        var (source, caret) = Mark("void Main(){ SyntheticIdentity$$ x; }");
        var type = ScriptApiLookup.Find(source, caret, Token); Assert.True(type.Topics.Count > 1); Assert.Contains("Several topics", type.Notice);
        (source, caret) = Mark("void Main(){ Message.SetText$$(123); }");
        var candidate = ScriptApiLookup.Find(source, caret, Token); Assert.Contains("set-text", candidate.Topics); Assert.Contains("Ambiguous", candidate.Notice);
        Assert.Contains("string path, string value", candidate.Signatures); Assert.DoesNotContain("int", candidate.Signatures);
    }

    [Theory]
    [InlineData("void Main(){ Message.$$ }", "SetText")]
    [InlineData("void Main(){ foreach(var pid in Message.Segments(\"PID\")){ pid.$$ } }", "Identifier")]
    [InlineData("var w=new DeidentificationWorkflow(new DeidentificationOptions(),Cancellation); void Main(){ w.$$ }", "Summary")]
    [InlineData("void Main(){ var p=DeidentificationPreset.$$; }", "PatientDeidentifier")]
    [InlineData("void Main(){ Message.Se$$ }", "SetRaw")]
    public void ExplicitCompletionUsesReceiverTypeAndOffersCatalogDescriptions(string marked, string expected)
    {
        var (source, caret) = Mark(marked); var items = ScriptApiLookup.Complete(source, caret, Token);
        Assert.Contains(items, i => i.Text == expected && i.Description.Contains("Errors:", StringComparison.Ordinal));
        Assert.DoesNotContain(items, i => i.Text is "Serialize" or "Model" or "CurrentMessage");
    }

    [Fact]
    public void ParameterHelpIncludesExactContractsInsideArgumentsAndDirectivesAreNeverResolved()
    {
        var (source, caret) = Mark("void Main(){ Message.SetText(\"PID-5\", $$); }");
        var help = ScriptApiLookup.Find(source, caret, Token, true); Assert.Contains("void ScriptMessage.SetText(string path, string value)", help.Signatures);
        (source, caret) = Mark("#r \"unavailable.dll\"\n#load \"unavailable.csx\"\nvoid Main(){ Message.Read$$(\"PID-5\"); }");
        Assert.Contains("read", ScriptApiLookup.Find(source, caret, Token).Topics);
        Assert.Empty(ScriptApiLookup.Complete(new string('x', 65537), 1, Token));
        Assert.Empty(ScriptApiLookup.Find(new string('x', 65537), 1, Token).Topics);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task F1AndCompletionOpeningNeverExecuteOrChangeScriptSourceAndStaleViewsCannotPublish(bool detach)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-api-assist-" + Guid.NewGuid().ToString("N"));
            using var documents = new DocumentManager(); using var script = new ScriptEditorViewModel(documents, new ScriptLibrary(root));
            await script.Session.NewAsync(() => Task.FromResult(UnsavedChangesDecision.Cancel));
            var (source, caret) = Mark("throw new Exception(\"MUST NEVER EXECUTE\"); void Main(){Message.SetText$$(\"PID-5\",\"SYNTHETIC\");}");
            script.Session.Source = source; var view = new ScriptEditorView(script); var reference = new ScriptApiReferenceViewModel(); var requests = 0;
            script.ApiLookupRequested += (topics, notice) => { requests++; reference.OpenTopics(topics, notice); };
            var window = new Window { Content = view, Width = 900, Height = 600 }; window.Show(); Pump(window); view.Editor.CaretOffset = caret;
            try
            {
                var dirty = script.Session.IsDirty; var undo = view.Editor.Document.UndoStack.CanUndo;
                view.Editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F1 }); await view.Assistance.Pending;
                Assert.Equal("set-text", reference.Selected!.Id); Assert.Equal(1, requests); Assert.False(script.IsBusy); Assert.Empty(documents.Documents);
                Assert.Equal(source, script.Session.Source); Assert.Equal(source, view.Editor.Text); Assert.Equal(dirty, script.Session.IsDirty); Assert.Equal(undo, view.Editor.Document.UndoStack.CanUndo);
                (source, caret) = Mark("void Main(){Message.$$}"); script.Session.Source = source; Pump(window); view.Editor.CaretOffset = caret;
                view.Assistance.RequestCompletion(); await view.Assistance.Pending; Assert.NotEmpty(view.Assistance.Completions); Assert.Equal(source, script.Session.Source);
                view.Assistance.RequestHelp(); var pending = view.Assistance.Pending;
                if (detach) window.Content = null; else view.Editor.Text = "void Main(){}";
                await pending; Assert.Equal(1, requests); Assert.Empty(view.Assistance.Completions);
            }
            finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
            return true;
        }, Token);
    }

    [Fact]
    public async Task ImmutableLibraryScriptsAllowHelpButCannotBeChangedThroughCompletion()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-api-library-" + Guid.NewGuid().ToString("N"));
            using var documents = new DocumentManager(); var library = new ScriptLibrary(root); using var script = new ScriptEditorViewModel(documents, library);
            await script.Session.OpenAsync(ScriptLibrary.ExampleId, () => Task.FromResult(UnsavedChangesDecision.Cancel));
            var original = library.Open(ScriptLibrary.ExampleId); var view = new ScriptEditorView(script); var window = new Window { Content = view }; window.Show(); Pump(window);
            try
            {
                Assert.True(view.Editor.IsReadOnly); view.Assistance.RequestCompletion(); await view.Assistance.Pending;
                Assert.Empty(view.Assistance.Completions); Assert.Equal(original.Source, script.Session.Source); Assert.Equal(original, library.Open(original.Id));
            }
            finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
            return true;
        }, Token);
    }

    [Fact]
    public async Task ExplicitCompletionInsertionRespectsReadonlyAndUsesTheExistingUndoableEditorTransaction()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var editor = new AvaloniaEdit.TextEditor(); var (source, caret) = Mark("void Main(){ Message.Se$$ }"); editor.Text = source;
            var assistance = new ScriptEditorAssistance(editor, (_, _) => { }); var window = new Window { Content = editor }; window.Show(); Pump(window); assistance.Start(); editor.CaretOffset = caret;
            try
            {
                editor.TextArea.Focus(); assistance.RequestCompletion(); await assistance.Pending; Pump(window);
                var popup = assistance.ActiveCompletion; Assert.NotNull(popup); Assert.NotNull(TopLevel.GetTopLevel(popup.CompletionList));
                var selected = popup.CompletionList.CompletionData.Single(c => c.Text == "SetText");
                var segment = new AvaloniaEdit.Document.SimpleSegment(caret - 2, 2);
                editor.IsReadOnly = true; selected.Complete(editor.TextArea, segment, EventArgs.Empty); Assert.Equal(source, editor.Text);
                editor.IsReadOnly = false; selected.Complete(editor.TextArea, segment, EventArgs.Empty);
                Assert.Equal(source.Replace("Message.Se", "Message.SetText", StringComparison.Ordinal), editor.Text);
                Assert.True(editor.Document.UndoStack.CanUndo); editor.Undo(); Assert.Equal(source, editor.Text);
            }
            finally { assistance.Stop(); window.Close(); }
            return true;
        }, Token);
    }

    [Fact]
    public async Task ExplicitSignatureHelpUsesKeyboardWithoutChangingCodeAndEscapeDismissesIt()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var editor = new AvaloniaEdit.TextEditor(); var (source, caret) = Mark("void Main(){ Message.SetText(\"PID-5\", $$); }"); editor.Text = source;
            var assistance = new ScriptEditorAssistance(editor, (_, _) => { }); var window = new Window { Content = editor }; window.Show(); Pump(window); assistance.Start(); editor.CaretOffset = caret;
            try
            {
                editor.TextArea.Focus(); editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space, KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift }); await assistance.Pending;
                Assert.Contains("string path, string value", assistance.SignatureHelp); Assert.Equal(source, editor.Text);
                editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Assert.Empty(assistance.SignatureHelp);
            }
            finally { assistance.Stop(); window.Close(); }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(false, 320, 1)] [InlineData(true, 320, 1.5)] [InlineData(false, 1100, 2)] [InlineData(true, 1100, 1)]
    public async Task ContractSectionsRelatedBackCopyAndLayoutRemainUsableAcrossThemesAndScales(bool dark, double width, double scale)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var model = new ScriptApiReferenceViewModel(); model.Query = "SetText"; model.Selected = model.Results.Single(e => e.Id == "set-text");
            var view = new ScriptApiReferenceView(model); var transform = new LayoutTransformControl { Child = view, LayoutTransform = new ScaleTransform(scale, scale) };
            var window = new Window { Content = transform, Width = width * scale, Height = 720 * scale, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show(); Pump(window);
            try
            {
                Assert.Contains("EditRejected", view.Errors.Text); Assert.Contains("source document", view.Mutation.Text); Assert.Contains("Whole document", view.Modes.Text);
                Assert.Equal(model.Selected.ExactSignature, view.Signature.Text);
                model.OpenRelated("states"); Assert.Equal("states", model.Selected!.Id); model.Back(); Assert.Equal("set-text", model.Selected!.Id); Assert.Equal("SetText", model.Query);
                await view.CopyAsync(false); Assert.Equal(model.Selected.ExactSignature, await window.Clipboard!.TryGetTextAsync());
                view.CopyCatalog.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await view.ClipboardTask;
                Assert.Contains("ContractVersion", await window.Clipboard!.TryGetTextAsync());
                model.OpenTopics([], "No supported API symbol."); Assert.Empty(model.Results); Assert.False(view.CopySignature.IsEnabled); Assert.Contains("No supported", view.Status.Text);
                model.Back(); Assert.Equal("set-text", model.Selected!.Id); Pump(window);
                foreach (var bar in view.GetVisualDescendants().OfType<AdaptiveActionBar>())
                    foreach (var child in bar.Children.Where(c => c.IsVisible)) Assert.InRange(child.Bounds.Right, 0, bar.Bounds.Width + .5);
                view.Search.Focus(NavigationMethod.Tab); Assert.True(view.Search.IsKeyboardFocusWithin);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
}
