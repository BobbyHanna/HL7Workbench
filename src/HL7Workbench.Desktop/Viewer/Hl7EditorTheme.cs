using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Preference → Avalonia effective variant → resolved palette. No editor document is replaced.</summary>
public sealed class Hl7EditorTheme : AvaloniaObject
{
    private Hl7EditorTheme() { }
    public const string PaletteResourceKey = "WorkbenchHl7EditorPalette";
    public static readonly AttachedProperty<Hl7EditorPalette?> PaletteProperty =
        AvaloniaProperty.RegisterAttached<Hl7EditorTheme, TextEditor, Hl7EditorPalette?>("Palette");
    static Hl7EditorTheme() => PaletteProperty.Changed.AddClassHandler<TextEditor>((editor, _) => Apply(editor));
    public static Hl7EditorPalette Resolve(ThemeVariant effectiveVariant) => effectiveVariant == ThemeVariant.Light ? Hl7EditorPalette.Light : Hl7EditorPalette.Dark;
    public static Hl7EditorPalette PaletteFor(TextEditor editor) => editor.GetValue(PaletteProperty) ?? Resolve(editor.ActualThemeVariant);
    internal static void Attach(TextEditor editor)
    {
        // Dynamic resource binding also permits a later named/custom palette to be
        // substituted at this one boundary, without changing either colorizer.
        editor.Bind(PaletteProperty, new DynamicResourceExtension(PaletteResourceKey));
        editor.ActualThemeVariantChanged += (_, _) => Apply(editor);
        Apply(editor);
    }
    private static void Apply(TextEditor editor)
    {
        var palette = PaletteFor(editor);
        editor.Background = palette.EditorBackground; editor.Foreground = palette.EditorForeground; editor.LineNumbersForeground = palette.LineNumbers;
        editor.TextArea.Caret.CaretBrush = palette.EditorCaret;
        editor.TextArea.SelectionBrush = palette.EditorSelectionBackground; editor.TextArea.SelectionForeground = palette.EditorSelectionForeground;
        editor.TextArea.TextView.CurrentLineBackground = palette.CurrentLineBackground;
        editor.TextArea.TextView.CurrentLineBorder = new Pen(palette.CurrentLineBorder, 1);
        // Repaint existing visual lines only. Do not touch text, undo, selection,
        // caret or viewport, nor initiate parsing/search/source reads.
        editor.TextArea.TextView.Redraw();
    }
}
