using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using HL7Workbench.Core;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Window-only spans over shared Core structure. No payload-sized highlight map.</summary>
internal sealed class SourceColorizer(Hl7Structure? structure, Func<long> origin, HL7Workbench.Workspace.MessagePresentation? presentation = null) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        var offset = origin();
        if (presentation is null) { ColorizeRange(offset + line.Offset, offset + line.EndOffset, offset); return; }
        foreach (var f in presentation.Intersecting(new(offset + line.Offset, line.Length)))
        {
            var a = Math.Max(offset + line.Offset, f.Display.Offset); var b = Math.Min(offset + line.EndOffset, f.Display.End);
            if (f.Source is { } raw)
            {
                var shift = raw.Offset - f.Display.Offset + offset;
                ColorizeRange(a + raw.Offset - f.Display.Offset, b + raw.Offset - f.Display.Offset, shift);
            }
            else ChangeLinePart(checked((int)(a - offset)), checked((int)(b - offset)), e =>
            {
                e.TextRunProperties.SetForegroundBrush(Brushes.LightSlateGray);
                e.TextRunProperties.SetBackgroundBrush(new SolidColorBrush(Color.Parse("#32303A")));
                e.TextRunProperties.SetTypeface(new Typeface("Consolas, monospace", FontStyle.Italic));
            });
        }
    }
    private void ColorizeRange(long start, long end, long offset)
    {
        if (structure is null) return;
        void Mark(long position, long length, IBrush brush)
        {
            var a = Math.Max(start, position); var b = Math.Min(end, position + length);
            if (a < b) ChangeLinePart(checked((int)(a - offset)), checked((int)(b - offset)), e => e.TextRunProperties.SetForegroundBrush(brush));
        }
        var low = 0; var high = structure.Segments.Count;
        while (low < high) { var mid = low + (high - low) / 2; if (structure.Segments[mid].Range.Offset <= start) low = mid + 1; else high = mid; }
        for (var i = Math.Max(0, low - 1); i < structure.Segments.Count && structure.Segments[i].Range.Offset < end; i++)
        {
            var segment = structure.Segments[i];
            if (!segment.CanEdit && segment.Fields.Count == 0) continue;
            Mark(segment.Range.Offset, segment.Identifier.Length, Brushes.DeepSkyBlue);
            if (segment.Range.Length > 3) Mark(segment.Range.Offset + 3, 1, Brushes.Gold);
            foreach (var field in Relevant(segment.Fields))
            {
                if (segment.Identifier == "MSH" && field.Number <= 2)
                {
                    if (field.Number == 2)
                    {
                        var colors = new IBrush[] { Brushes.LightGreen, Brushes.Orange, Brushes.LightSalmon, Brushes.Plum };
                        for (var j = 0; j < Math.Min(4, field.Range.Length); j++) Mark(field.Range.Offset + j, 1, colors[j]);
                    }
                    continue;
                }
                if (field.Number > 1) Mark(field.Range.Offset - 1, 1, Brushes.Gold);
                Visit(field);
                // Escape accents do not infer structure. Only the loaded characters are examined;
                // the scanner has already established global pairing and structural boundaries.
                if (structure.Delimiters.Escape is { } escape)
                    for (var p = Math.Max(start, field.Range.Offset); p < Math.Min(end, field.Range.End); p++)
                        if (CurrentContext.Document.GetCharAt(checked((int)(p - offset))) == escape) Mark(p, 1, Brushes.LightSalmon);
            }
        }
        void Visit(StructureNode node)
        {
            if (node.Range.End < start || node.Range.Offset > end) return;
            var brush = node.Kind switch { StructureKind.Field => Brushes.Orange, StructureKind.Repetition => Brushes.LightGreen, _ => Brushes.Plum };
            foreach (var child in Relevant(node.Children))
            {
                if (child.Number > 1) Mark(child.Range.Offset - 1, 1, brush);
                Visit(child);
            }
        }
        IEnumerable<StructureNode> Relevant(IReadOnlyList<StructureNode> nodes)
        {
            var a = 0; var b = nodes.Count;
            while (a < b) { var mid = a + (b - a) / 2; if (nodes[mid].Range.Offset <= start) a = mid + 1; else b = mid; }
            for (var i = Math.Max(0, a - 1); i < nodes.Count && nodes[i].Range.Offset <= end; i++) yield return nodes[i];
        }
    }
}
