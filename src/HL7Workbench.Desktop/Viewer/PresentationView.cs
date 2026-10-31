using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Compact editor or naturally scrolling virtualized editor blocks over the same projection.</summary>
public sealed class PresentationView : ContentControl
{
    private readonly MessageDocument document;
    private readonly MessageInspection inspection;
    private readonly MessagePresentation presentation;
    private readonly ListBox? blocks;
    private bool attached;
    private bool selecting;
    public TextEditor Editor { get; }
    public MessagePresentation Presentation => presentation;
    public bool IsVirtualized => blocks is not null;
    public Task Ready { get; private set; } = Task.CompletedTask;

    public PresentationView(MessageDocument document, MessageInspection inspection, TextEditor editor)
        : this(document, inspection, editor, null) { }
    private readonly Func<Action, TimeSpan, IDisposable>? scheduleHover;
    internal PresentationView(MessageDocument document, MessageInspection inspection, TextEditor editor,
        Func<Action, TimeSpan, IDisposable>? scheduleHover)
    {
        this.scheduleHover = scheduleHover;
        this.document = document; this.inspection = inspection; Editor = editor;
        presentation = document.Presentation ?? throw new InvalidOperationException("Presentation required.");
        if (!presentation.RequiresVirtualization) { Content = editor; Setup(editor, new(0, presentation.Length)); }
        else
        {
            blocks = new ListBox { ItemsSource = presentation.Blocks,
                ItemTemplate = new FuncDataTemplate<PresentationBlock>((block, _) =>
                {
                    if (block is null) return new TextBlock();
                    var text = MessageViewer.CreateEditor(""); text.ShowLineNumbers = false;
                    text.Height = 48; text.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                    text.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    Setup(text, block.Display);
                    return new StackPanel { Children = { new TextBlock { Text = $"Display block {block.Number + 1:N0} · continuation boundaries are presentation only", [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.10"), Opacity = .65, IsHitTestVisible = false }, text } };
                }) };
            Content = blocks;
            // The primary editor is not mounted in virtual mode; resolve against
            // the mounted list rather than copying that detached editor's theme.
            blocks.Bind(BackgroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(WorkbenchThemeResources.EditorBackground));
        }
    }
    private void Setup(TextEditor editor, CharacterRange display)
    {
        // Soft wrapping the compact preview keeps later fields visible without changing source lines.
        editor.WordWrap = !presentation.RequiresVirtualization;
        var toolTips = new FieldToolTipLifecycle(editor, local =>
        {
            var hit = display.Offset + local;
            var fragment = presentation.Hit(hit);
            if (fragment?.Marker is not null) return new(fragment.Display,
                "Display-only omission marker · right-click to View Full Value or export original source bytes.");
            return presentation.SourceOffset(hit) is { } raw ? ValidationInlineRenderer.ToolTipAt(document, raw) ?? FieldToolTipTarget.From(inspection.ResolveOffset(raw)) : null;
        }, scheduleHover);
        _ = new ValidationInlineRenderer(editor, document, presentation, display);
        var loading = 0L;
        CancellationTokenSource? cancel = null;
        editor.TextArea.TextView.LineTransformers.Add(new SourceColorizer(document.Structure, () => display.Offset, () => Hl7EditorTheme.PaletteFor(editor), presentation));
        editor.TextArea.TextView.LineTransformers.Add(new PresentationSelectionColorizer(inspection, presentation, display.Offset, () => Hl7EditorTheme.PaletteFor(editor)));
        async Task Load()
        {
            var version = ++loading; cancel?.Cancel(); cancel?.Dispose(); cancel = new();
            try
            {
                var text = await document.ReadAsync(s => presentation.Read(s, display), cancel.Token);
                if (text is null || version != loading || !attached) return;
                selecting = true;
                try { editor.Document = new TextDocument(text); editor.ScrollToHome(); }
                finally { selecting = false; }
                NavigateEditor(editor, display, false);
                Dispatcher.UIThread.Post(() =>
                { if (attached && version == loading) NavigateEditor(editor, display, inspection.SelectedRange64 is not null); }, DispatcherPriority.Loaded);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or System.IO.IOException)
            { if (attached && version == loading && e is System.IO.IOException) document.RecordActivity(ActivityKind.SourceReadFailed); }
        }
        editor.AttachedToVisualTree += (_, _) => Ready = Load();
        editor.DetachedFromVisualTree += (_, _) =>
        {
            loading++; cancel?.Cancel(); cancel?.Dispose(); cancel = null; selecting = true;
            try { editor.Document = new TextDocument(); ToolTip.SetIsOpen(editor.TextArea.TextView, false); }
            finally { selecting = false; }
        };
        long? Offset(int local) => presentation.SourceOffset(display.Offset + local);
        editor.TextArea.Caret.PositionChanged += (_, _) => { if (!selecting && Offset(editor.CaretOffset) is { } offset) inspection.SelectOffset(offset); };
        editor.TextArea.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.ClickCount != 2 || !e.GetCurrentPoint(editor).Properties.IsLeftButtonPressed) return;
            if (editor.GetPositionFromPoint(e.GetPosition(editor)) is not { } position) return;
            var hit = display.Offset + editor.Document.GetOffset(position.Location);
            var fragment = presentation.Hit(hit);
            var offset = presentation.SourceOffset(hit) ?? fragment?.Omitted?.Offset;
            if (offset is { } raw) { e.Handled = true; inspection.SelectDeepestOffset(raw); }
        }, RoutingStrategies.Tunnel);
        void CloseTip() => toolTips.Close();
        BoundedCopy.Attach(editor, () => presentation.IsContiguousSource(new(display.Offset + editor.SelectionStart, editor.SelectionLength)));
        ViewerRequests.AttachMenu(new(editor, document, inspection, presentation, display.Offset));
        editor.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty && !editor.IsVisible) CloseTip(); };
        // View-owned handlers reference only this editor. Document subscription is removed on detach.
        editor.DetachedFromVisualTree += (_, _) => editors.RemoveAll(p => ReferenceEquals(p.Editor, editor));
        editor.AttachedToVisualTree += (_, _) => { if (!editors.Any(p => ReferenceEquals(p.Editor, editor))) editors.Add((editor, display)); };
    }
    private readonly List<(TextEditor Editor, CharacterRange Display)> editors = [];
    private CharacterRange? validationNavigation;
    internal void NavigateValidationRange(CharacterRange range)
    {
        validationNavigation = range;
        var offset = presentation.DisplayOffset(range.Offset);
        if (blocks is not null)
        {
            var block = presentation.Blocks.FirstOrDefault(b => b.Display.Contains(offset) || offset == presentation.Length && b.Display.End == offset);
            if (block is not null) blocks.ScrollIntoView(block);
        }
        foreach (var pair in editors.ToArray()) NavigateEditor(pair.Editor, pair.Display, true);
    }
    private void NavigateEditor(TextEditor editor, CharacterRange display, bool navigate)
    {
        if ((validationNavigation ?? inspection.SelectedRange64) is not { } raw)
        {
            selecting = true;
            try { if (navigate) editor.Select(editor.CaretOffset, 0); editor.TextArea.TextView.Redraw(); }
            finally { selecting = false; }
            return;
        }
        var start = presentation.DisplayOffset(raw.Offset); var end = presentation.DisplayOffset(raw.End);
        selecting = true;
        try
        {
            var a = Math.Clamp(start - display.Offset, 0, editor.Document.TextLength); var b = Math.Clamp(end - display.Offset, a, editor.Document.TextLength);
            if (navigate) editor.Select((int)a, (int)(b - a));
            if (navigate && display.Contains(start))
            {
                var location = editor.Document.GetLocation((int)a); editor.ScrollTo(location.Line, location.Column);
                var view = editor.TextArea.TextView;
                if (!editor.WordWrap && view.VisualLinesValid)
                {
                    var point = view.GetVisualPosition(new TextViewPosition(location), VisualYPosition.LineMiddle);
                    editor.ScrollToHorizontalOffset(Math.Max(0, point.X - view.Bounds.Width / 2));
                }
            }
            editor.TextArea.TextView.Redraw();
        }
        finally { selecting = false; }
    }
    private void SelectionChanged(object? sender, InspectionSelectionChangedEventArgs e)
    {
        validationNavigation = null;
        if (e.NavigateRaw && inspection.SelectedRange64 is { } raw && blocks is not null)
        {
            var offset = presentation.DisplayOffset(raw.Offset);
            var low = 0; var high = presentation.Blocks.Count;
            while (low < high) { var mid = low + (high - low) / 2; if (presentation.Blocks[mid].Display.End <= offset) low = mid + 1; else high = mid; }
            if (low < presentation.Blocks.Count) blocks.ScrollIntoView(presentation.Blocks[low]);
        }
        foreach (var pair in editors.ToArray()) NavigateEditor(pair.Editor, pair.Display, e.NavigateRaw);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); attached = true; inspection.SelectionChanged += SelectionChanged; }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { attached = false; inspection.SelectionChanged -= SelectionChanged; base.OnDetachedFromVisualTree(e); }
}

internal sealed class PresentationSelectionColorizer(MessageInspection inspection, MessagePresentation presentation, long origin, Func<Hl7EditorPalette> palette) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        if (inspection.SelectedRange64 is not { } selected) return;
        foreach (var f in presentation.Intersecting(new(origin + line.Offset, line.Length)))
        {
            long a, b;
            if (f.Source is { } source)
            { a = f.Display.Offset + Math.Max(0, selected.Offset - source.Offset); b = f.Display.Offset + Math.Min(source.Length, selected.End - source.Offset); }
            else if (f.Omitted is { } omitted && selected.Offset < omitted.End && selected.End > omitted.Offset)
            { a = f.Display.Offset; b = f.Display.End; }
            else continue;
            a = Math.Max(a, origin + line.Offset); b = Math.Min(b, origin + line.EndOffset);
            if (a < b) ChangeLinePart((int)(a - origin), (int)(b - origin), e => e.TextRunProperties.SetBackgroundBrush(palette().StructuralSelectionBackground));
        }
    }
}
