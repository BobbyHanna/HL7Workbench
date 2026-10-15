using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Rendering;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Editor-only palette. Token categories and editor chrome share one theme without changing global C# definitions.</summary>
public sealed record ScriptEditorPalette(string Foreground, string Background, string Keyword, string Method, string Type,
    string String, string Comment, string Number, string LineNumber, string Selection, string CurrentLine, string CurrentLineBorder)
{
    public static ScriptEditorPalette Dark { get; } = new("#D4D4D4", "#1E1E1E", "#C586C0", "#DCDCAA", "#4EC9B0",
        "#CE9178", "#6A9955", "#B5CEA8", "#A0A0A0", "#264F78", "#252526", "#3F3F46");

    public string TokenForeground(string? category) => category switch
    {
        "Comment" or "DocComment" or "DocCommentTag" => Comment,
        "String" or "Char" => String,
        "NumberLiteral" => Number,
        "ValueTypeKeywords" or "ReferenceTypeKeywords" => Type,
        "Keywords" or "GotoKeywords" or "ContextKeywords" or "ExceptionKeywords" or "CheckedKeyword" or
        "UnsafeKeywords" or "OperatorKeywords" or "ParameterModifiers" or "Modifiers" or "Visibility" or
        "NamespaceKeywords" or "GetSetAddRemove" or "TrueFalse" or "TypeKeywords" or "SemanticKeywords" or
        "ThisOrBaseReference" or "NullOrValueKeywords" or "Preprocessor" => Keyword,
        // AvaloniaEdit's MethodCall rule is only identifier-before-'(' and also matches
        // delegate invocations. It cannot establish a method or user-defined type symbol.
        _ => Foreground
    };

    internal string SymbolForeground(ScriptSymbolKind kind) => kind switch
    {
        ScriptSymbolKind.Method => Method, ScriptSymbolKind.Type => Type, ScriptSymbolKind.Keyword => Keyword,
        ScriptSymbolKind.String => String, ScriptSymbolKind.Comment => Comment, ScriptSymbolKind.Number => Number,
        _ => Foreground
    };

    public void Apply(TextEditor editor)
    {
        editor.Background = Brush(Background); editor.Foreground = Brush(Foreground);
        editor.LineNumbersForeground = Brush(LineNumber);
        editor.TextArea.Caret.CaretBrush = Brush(Foreground);
        editor.TextArea.SelectionBrush = Brush(Selection);
        editor.TextArea.SelectionForeground = Brush(Foreground);
        editor.TextArea.TextView.CurrentLineBackground = Brush(CurrentLine);
        editor.TextArea.TextView.CurrentLineBorder = new Pen(Brush(CurrentLineBorder), 1);
        editor.Options.HighlightCurrentLine = true;
        editor.Options.EnableHyperlinks = editor.Options.EnableEmailHyperlinks = false;
        editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        var transformers = editor.TextArea.TextView.LineTransformers;
        foreach (var old in transformers.OfType<HighlightingColorizer>().ToArray()) transformers.Remove(old);
        transformers.Add(new PaletteColorizer(editor.SyntaxHighlighting, this));
    }

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));

    private sealed class PaletteColorizer(IHighlightingDefinition definition, ScriptEditorPalette palette) : HighlightingColorizer(definition)
    {
        protected override void ApplyColorToElement(VisualLineElement element, HighlightingColor color)
        {
            // Reuse lexical spans (including imported XML comments), but never inherit a
            // light-theme inline foreground/background, bold weight or fixed font size.
            // Style-only nested spans keep their parent's string/comment foreground.
            if (color.Foreground is not null)
                element.TextRunProperties.SetForegroundBrush(Brush(palette.TokenForeground(color.Name)));
        }
    }
}
