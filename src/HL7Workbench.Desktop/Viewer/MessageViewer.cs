using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

public static class MessageViewer
{
    public static Control Create(MessageDocument document, bool dockedInspection = false)
    {
        if (document.Index is not null) return new FileDocumentView(document, dockedInspection);
        return CreateActive(document, dockedInspection);
    }
    internal static Control CreateActive(MessageDocument document, bool dockedInspection = false)
    {
        if (document.Source is { } source)
        {
            var inspection = document.ActiveInspection ?? new MessageInspection(new HL7Workbench.Core.Hl7Structure(source.Length,
                HL7Workbench.Core.Hl7Delimiters.Default, [], [], false), source);
            var view = new MessageInspectionView(inspection, CreateEditor(""), document, dockedInspection);
            if (document.Failure is { } failure) view.WindowStatus.Text = failure;
            return view;
        }
        if (!document.Message.HasDecodedText)
            return new TextBlock { Margin = new(16), TextWrapping = TextWrapping.Wrap,
                Text = "Text unavailable: the file cannot be decoded safely. Original bytes are retained. BOM-less files require UTF-8 in this stage." };
        var editor = CreateEditor(document.Message.RawText);
        editor.TextArea.TextView.LineTransformers.Add(new Hl7Colorizer(new(document.Message)));
        return new MessageInspectionView(document.Inspection, editor, document, dockedInspection);
    }
    internal static TextEditor CreateEditor(string text)
    {
        var editor = new TextEditor
        {
            Document = new TextDocument(text), IsReadOnly = true,
            ShowLineNumbers = true, WordWrap = false, FontFamily = new("Consolas, monospace"), FontSize = 14,
            Background = new SolidColorBrush(Color.Parse("#18212B")), Foreground = Brushes.WhiteSmoke,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        // Both the control and its editing pipeline refuse writes; the retained Core message
        // is never connected to editor changes. Selection and clipboard copy remain available.
        BoundedCopy.Attach(editor);
        return editor;
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
