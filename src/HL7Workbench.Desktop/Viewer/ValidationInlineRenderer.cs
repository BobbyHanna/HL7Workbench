using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>View-owned diagnostic decoration. Source intervals are mapped explicitly; omitted text is never painted as source.</summary>
internal sealed class ValidationInlineRenderer : IBackgroundRenderer
{
    internal const int MaximumDecorations = 200;
    private readonly TextEditor editor;
    private readonly MessageDocument document;
    private readonly MessagePresentation? presentation;
    private readonly CharacterRange? display;
    private bool attached;
    public KnownLayer Layer => KnownLayer.Selection;
    internal ValidationInlineRenderer(TextEditor editor, MessageDocument document, MessagePresentation? presentation = null, CharacterRange? display = null)
    {
        this.editor = editor; this.document = document; this.presentation = presentation; this.display = display;
        editor.TextArea.TextView.BackgroundRenderers.Add(this);
        editor.AttachedToVisualTree += (_, _) => { if (attached) return; attached = true; document.PropertyChanged += Changed; editor.TextArea.TextView.Redraw(); };
        editor.DetachedFromVisualTree += (_, _) => { attached = false; document.PropertyChanged -= Changed; };
    }
    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(MessageDocument.SchemaValidation)) editor.TextArea.TextView.Redraw(); }
    internal IReadOnlyList<(Hl7SchemaDiagnostic Diagnostic, CharacterRange Display)> Decorations()
    {
        var diagnostics = document.SchemaValidation?.Diagnostics ?? [];
        return diagnostics.OrderByDescending(d => d.Severity).ThenBy(d => d.Range.Offset).Take(MaximumDecorations)
            .SelectMany(d => Map(d.Range).Select(r => (d, r))).Take(MaximumDecorations).ToArray();
    }
    private IEnumerable<CharacterRange> Map(CharacterRange raw)
    {
        if (presentation is null)
        {
            if (raw.Offset > editor.Document.TextLength) yield break;
            yield return new(raw.Offset, Math.Min(raw.Length, editor.Document.TextLength - raw.Offset)); yield break;
        }
        var window = display ?? new CharacterRange(0, presentation.Length);
        foreach (var fragment in presentation.Intersecting(window))
        {
            if (fragment.Source is not { } source) continue;
            var start = Math.Max(raw.Offset, source.Offset); var end = Math.Min(raw.End, source.End);
            if (end > start || raw.Length == 0 && raw.Offset >= source.Offset && raw.Offset <= source.End)
            {
                var a = fragment.Display.Offset + start - source.Offset; var b = a + Math.Max(0, end - start);
                a = Math.Max(a, window.Offset); b = Math.Min(b, window.End);
                if (b >= a) yield return new(a - window.Offset, b - a);
            }
        }
    }
    public void Draw(TextView textView, DrawingContext context)
    {
        if (!textView.VisualLinesValid) return;
        var dark = editor.ActualThemeVariant != ThemeVariant.Light;
        foreach (var (diagnostic, range) in Decorations())
        {
            if (range.Offset > editor.Document.TextLength) continue;
            var brush = diagnostic.Severity switch
            {
                Hl7DiagnosticSeverity.Error => new SolidColorBrush(Color.Parse(dark ? "#FF8E91" : "#B4232B")),
                Hl7DiagnosticSeverity.Warning => new SolidColorBrush(Color.Parse(dark ? "#FFD166" : "#956000")),
                _ => new SolidColorBrush(Color.Parse(dark ? "#8ABFFF" : "#175DB5"))
            };
            var pen = new Pen(brush, 1.5);
            if (range.Length == 0)
            {
                var location = editor.Document.GetLocation((int)range.Offset);
                if (textView.GetVisualLine(location.Line) is null) continue;
                var p = textView.GetVisualPosition(new(location), VisualYPosition.LineBottom) - textView.ScrollOffset;
                context.DrawLine(pen, new(p.X, p.Y - 6), new(p.X, p.Y));
                context.DrawLine(pen, new(1, p.Y - 6), new(5, p.Y - 2)); continue;
            }
            var segment = new TextSegment { StartOffset = checked((int)range.Offset), Length = checked((int)range.Length) };
            foreach (var rectangle in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                var start = Math.Max(0, rectangle.Left); var end = Math.Min(textView.Bounds.Width, rectangle.Right); var y = rectangle.Bottom - 1;
                for (var x = start; x < end; x += 4)
                { context.DrawLine(pen, new(x, y), new(Math.Min(x + 2, end), y - 2)); context.DrawLine(pen, new(Math.Min(x + 2, end), y - 2), new(Math.Min(x + 4, end), y)); }
            }
        }
    }
    internal static FieldToolTipTarget? ToolTipAt(MessageDocument? document, long sourceOffset)
    {
        var diagnostics = document?.SchemaValidation?.Diagnostics.Where(d => d.Range.Contains(sourceOffset) || d.IsBoundary && Math.Abs(d.Range.Offset - sourceOffset) <= 1)
            .OrderByDescending(d => d.Severity).Take(4).ToArray();
        if (diagnostics is not { Length: > 0 }) return null;
        var text = string.Join("\n\n", diagnostics.Select(Format));
        if (text.Length > 2048) text = text[..2048] + "…";
        return new((diagnostics[0].ProfileId, diagnostics[0].RuleId, diagnostics[0].Range), text);
    }
    internal static string Format(Hl7SchemaDiagnostic d) => $"{d.Severity} · {d.Path ?? "Message boundary"}\n{d.RuleId}: {d.Message}\nProfile: {d.ProfileId}\nRule source: {d.Provenance}";
}
