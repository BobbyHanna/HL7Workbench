using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>View-owned, debounced symbol overlay. Edits clear old ranges synchronously; only the current revision can publish.</summary>
internal sealed class ScriptSemanticHighlighting : DocumentColorizingTransformer
{
    private static readonly SemaphoreSlim analysisGate = new(1, 1);
    private readonly TextEditor editor;
    private readonly ScriptEditorPalette palette;
    private readonly Func<string, CancellationToken, ScriptSymbolSpan[]> classify;
    private readonly TimeSpan debounce;
    private readonly object cancellationSync = new();
    private CancellationTokenSource? request;
    private ScriptSymbolSpan[] spans = [];
    private TextDocument? classifiedDocument;
    private long revision;
    private bool active;
    internal Task Pending { get; private set; } = Task.CompletedTask;
    internal IReadOnlyList<ScriptSymbolSpan> Spans => spans;

    internal ScriptSemanticHighlighting(TextEditor editor, ScriptEditorPalette palette,
        Func<string, CancellationToken, ScriptSymbolSpan[]>? classify = null, TimeSpan? debounce = null)
    {
        this.editor = editor; this.palette = palette;
        this.classify = classify ?? ScriptSymbolClassifier.Classify;
        this.debounce = debounce ?? TimeSpan.FromMilliseconds(200);
        editor.TextArea.TextView.LineTransformers.Add(this);
    }
    internal void Start()
    {
        if (active) return;
        active = true; editor.TextChanged += Changed; editor.DocumentChanged += Changed;
        Refresh();
    }
    internal void Stop()
    {
        if (!active) return;
        active = false; editor.TextChanged -= Changed; editor.DocumentChanged -= Changed;
        revision++;
        lock (cancellationSync) { request?.Cancel(); request = null; }
        Clear();
    }
    private void Changed(object? sender, EventArgs e) => Refresh();
    private void Clear()
    {
        spans = []; classifiedDocument = null; editor.TextArea.TextView.Redraw();
    }
    private void Refresh()
    {
        var version = ++revision;
        Clear();
        var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        lock (cancellationSync) { request?.Cancel(); request = cancellation; }
        Pending = AnalyzeAsync(editor.Document, editor.Text, version, cancellation);
    }
    private async Task AnalyzeAsync(TextDocument document, string source, long version, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        try
        {
            await Task.Delay(debounce, token).ConfigureAwait(false);
            await analysisGate.WaitAsync(token).ConfigureAwait(false);
            ScriptSymbolSpan[] result;
            try { result = await Task.Run(() => classify(source, token), token).ConfigureAwait(false); }
            finally { analysisGate.Release(); }
            token.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!active || token.IsCancellationRequested || version != revision || !ReferenceEquals(document, editor.Document)) return;
                spans = result; classifiedDocument = document; editor.TextArea.TextView.Redraw();
            }, DispatcherPriority.Background, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* Best-effort colors fall back to the lexer; never retain/log compiler exception text or source. */ }
        finally
        {
            // Pending is exposed for lifecycle draining/tests. Do not let its completed
            // async state/closures retain an additional source copy or an old document.
            source = ""; document = null!;
            // Cancellation and disposal share a tiny guard, so shutdown never has to
            // pump the UI dispatcher merely to release a completed request's source.
            lock (cancellationSync)
            {
                if (ReferenceEquals(request, cancellation)) request = null;
                cancellation.Dispose();
            }
        }
    }
    protected override void ColorizeLine(DocumentLine line)
    {
        if (!ReferenceEquals(classifiedDocument, editor.Document)) return;
        // Binary-search into immutable sorted spans; scrolling never scans the script
        // prefix or asks Roslyn for symbols on the rendering/UI thread.
        var low = 0; var high = spans.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (spans[middle].Offset + spans[middle].Length <= line.Offset) low = middle + 1;
            else high = middle;
        }
        for (var i = low; i < spans.Length && spans[i].Offset < line.EndOffset; i++)
        {
            var span = spans[i]; var start = Math.Max(line.Offset, span.Offset); var end = Math.Min(line.EndOffset, span.Offset + span.Length);
            if (start < end) ChangeLinePart(start, end, e => e.TextRunProperties.SetForegroundBrush(
                new SolidColorBrush(Color.Parse(palette.SymbolForeground(span.Kind)))));
        }
    }
}
