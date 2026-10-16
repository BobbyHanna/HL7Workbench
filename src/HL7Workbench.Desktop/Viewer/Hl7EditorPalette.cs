using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Only colors used by the current HL7 renderers. Replacing a palette never changes text or structure.</summary>
public sealed record Hl7EditorPalette(IBrush EditorBackground, IBrush EditorForeground, IBrush EditorSelectionBackground,
    IBrush EditorSelectionForeground, IBrush EditorCaret, IBrush LineNumbers, IBrush CurrentLineBackground, IBrush CurrentLineBorder,
    IBrush StructuralSelectionBackground, IBrush OmissionBackground, IBrush OmissionForeground, IBrush SegmentForeground,
    IBrush FieldSeparatorForeground, IBrush ComponentSeparatorForeground, IBrush SubcomponentSeparatorForeground,
    IBrush RepetitionSeparatorForeground, IBrush EscapeForeground)
{
    private static IBrush Brush(string color) => new ImmutableSolidColorBrush(Color.Parse(color));
    public static Hl7EditorPalette Dark { get; } = new(Brush("#18212B"), Brush("#F5F5F5"), Brush("#264F78"), Brush("#F5F5F5"),
        Brush("#F5F5F5"), Brush("#A0A0A0"), Brush("#202C38"), Brush("#384A65"), Brush("#384A65"), Brush("#32303A"), Brush("#778899"),
        Brushes.DeepSkyBlue, Brushes.Gold, Brushes.LightGreen, Brushes.Plum, Brushes.Orange, Brushes.LightSalmon);
    public static Hl7EditorPalette Light { get; } = new(Brush("#FFFFFF"), Brush("#20242B"), Brush("#CCE3FC"), Brush("#142338"),
        Brush("#20242B"), Brush("#626A76"), Brush("#F3F7FB"), Brush("#CBD5E1"), Brush("#DCEAF8"), Brush("#F0EEF5"), Brush("#62596F"),
        Brush("#005A9E"), Brush("#825600"), Brush("#146B35"), Brush("#7B287D"), Brush("#924400"), Brush("#9A3D22"));

    public IBrush SyntaxForeground(Hl7HighlightKind kind) => kind switch
    {
        Hl7HighlightKind.Segment => SegmentForeground, Hl7HighlightKind.Field => FieldSeparatorForeground,
        Hl7HighlightKind.Component => ComponentSeparatorForeground, Hl7HighlightKind.Subcomponent => SubcomponentSeparatorForeground,
        Hl7HighlightKind.Repetition => RepetitionSeparatorForeground, _ => EscapeForeground
    };
}
