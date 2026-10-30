using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Explicit help/completion only. View lifetime and revision checks reject detached/stale compiler results.</summary>
internal sealed class ScriptEditorAssistance
{
    private static readonly SemaphoreSlim analysisGate = new(1, 1);
    private readonly TextEditor editor;
    private readonly Action<IReadOnlyList<string>, string> showReference;
    private CancellationTokenSource? request;
    private CompletionWindow? completion;
    private bool active;
    private long revision;
    internal Task Pending { get; private set; } = Task.CompletedTask;
    internal IReadOnlyList<ScriptApiCompletion> Completions { get; private set; } = [];
    internal CompletionWindow? ActiveCompletion => completion;
    internal string SignatureHelp { get; private set; } = "";

    internal ScriptEditorAssistance(TextEditor editor, Action<IReadOnlyList<string>, string> showReference)
    { this.editor = editor; this.showReference = showReference; }
    internal void Start()
    {
        if (active) return; active = true;
        editor.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Tunnel);
        editor.TextChanged += Changed; editor.DocumentChanged += Changed;
    }
    internal void Stop()
    {
        if (!active) return; active = false;
        editor.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
        editor.TextChanged -= Changed; editor.DocumentChanged -= Changed; Changed(null, EventArgs.Empty);
    }
    private void Changed(object? sender, EventArgs e)
    {
        revision++; request?.Cancel(); completion?.Close(); completion = null;
        Completions = []; SignatureHelp = ""; ToolTip.SetIsOpen(editor, false); ToolTip.SetTip(editor, null);
    }
    private void KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Changed(null, EventArgs.Empty); return; }
        if (e.Key == Key.F1 && e.KeyModifiers == KeyModifiers.None) { e.Handled = true; RequestHelp(); }
        else if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control) { e.Handled = true; RequestCompletion(); }
        else if (e.Key == Key.Space && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)) { e.Handled = true; RequestSignatureHelp(); }
    }
    internal void RequestHelp() => Begin(0);
    internal void RequestCompletion() => Begin(1);
    internal void RequestSignatureHelp() => Begin(2);
    private void Begin(int kind)
    {
        if (!active || kind == 1 && editor.IsReadOnly) return;
        Changed(null, EventArgs.Empty);
        var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3)); request = cancellation;
        Pending = AnalyzeAsync(kind, editor.Document, editor.Text, editor.CaretOffset, revision, cancellation);
    }
    private async Task AnalyzeAsync(int kind, TextDocument document, string source, int caret, long version, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        try
        {
            await analysisGate.WaitAsync(token);
            ScriptApiHelp? help = null; IReadOnlyList<ScriptApiCompletion> items = [];
            try
            {
                await Task.Run(() =>
                {
                    if (kind == 1) items = ScriptApiLookup.Complete(source, caret, token);
                    else help = ScriptApiLookup.Find(source, caret, token, kind == 2);
                }, token);
            }
            finally { analysisGate.Release(); }
            token.ThrowIfCancellationRequested();
            if (!active || version != revision || !ReferenceEquals(document, editor.Document) || caret != editor.CaretOffset) return;
            if (kind == 0) showReference(help!.Topics, help.Notice);
            else if (kind == 2)
            {
                SignatureHelp = help!.Signatures.Length == 0 ? "No supported invocation at this position. Place the cursor inside its arguments." : help.Signatures;
                ToolTip.SetTip(editor, new TextBlock { Text = SignatureHelp, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 });
                ToolTip.SetIsOpen(editor, true);
            }
            else
            {
                Completions = items;
                if (items.Count == 0) return;
                completion = new CompletionWindow(editor.TextArea);
                var start = caret; while (start > 0 && Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsIdentifierPartCharacter(source[start - 1])) start--;
                completion.StartOffset = start; completion.EndOffset = caret;
                foreach (var item in items) completion.CompletionList.CompletionData.Add(new ApiCompletion(item));
                completion.Show();
            }
        }
        catch (OperationCanceledException)
        {
            if (active && version == revision && caret == editor.CaretOffset && kind == 0)
                showReference([], "API lookup timed out. Search the offline reference; no script was executed.");
        }
        catch (Exception)
        {
            // Never show compiler exception text or user script fragments in help.
            if (active && version == revision && kind == 0) showReference([], "API lookup unavailable. Search the offline reference; no script was executed.");
        }
        finally
        {
            source = ""; document = null!;
            if (ReferenceEquals(request, cancellation)) request = null;
            cancellation.Dispose();
        }
    }
    private sealed class ApiCompletion(ScriptApiCompletion item) : ICompletionData
    {
        public IImage? Image => null;
        public string Text => item.Text;
        public object Content => item.Text;
        public object Description => new ScrollViewer { MaxWidth = 520, MaxHeight = 320,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new TextBlock { Text = item.Description, TextWrapping = TextWrapping.Wrap } };
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            if (textArea.GetVisualAncestors().OfType<TextEditor>().FirstOrDefault()?.IsReadOnly != false) return;
            textArea.Document.Replace(completionSegment, item.Text);
        }
    }
}
