using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

public static class MessageViewer
{
    public static Control Create(MessageDocument document)
    {
        if (!document.Message.HasDecodedText)
            return new TextBlock { Margin = new(16), TextWrapping = TextWrapping.Wrap,
                Text = "Text unavailable: the file cannot be decoded safely. Original bytes are retained. BOM-less files require UTF-8 in this stage." };
        var editor = new TextEditor
        {
            Document = new TextDocument(document.Message.RawText), IsReadOnly = true,
            ShowLineNumbers = true, WordWrap = false, FontFamily = new("Consolas, monospace"), FontSize = 14,
            Background = new SolidColorBrush(Color.Parse("#18212B")), Foreground = Brushes.WhiteSmoke,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        // Both the control and its editing pipeline refuse writes; the retained Core message
        // is never connected to editor changes. Selection and clipboard copy remain available.
        editor.TextArea.TextView.LineTransformers.Add(new Hl7Colorizer(new(document.Message)));
        return new MessageInspectionView(document.Inspection, editor);
    }
}

internal sealed class Hl7Colorizer(Hl7HighlightMap map) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        // A fixed dark canvas and light value text give the accents consistent contrast in either app theme.
        foreach (var span in map.Intersecting(line.Offset, line.Length))
            ChangeLinePart(Math.Max(line.Offset, span.Offset), Math.Min(line.EndOffset, span.Offset + span.Length),
                element => element.TextRunProperties.SetForegroundBrush(span.Kind switch
                {
                    Hl7HighlightKind.Segment => Brushes.DeepSkyBlue,
                    Hl7HighlightKind.Field => Brushes.Gold,
                    Hl7HighlightKind.Component => Brushes.LightGreen,
                    Hl7HighlightKind.Repetition => Brushes.Orange,
                    Hl7HighlightKind.Subcomponent => Brushes.Plum,
                    _ => Brushes.LightSalmon
                }));
    }
}
