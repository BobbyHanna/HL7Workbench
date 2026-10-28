using System.Xml;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Small lexical editor definition only; SQL grammar and execution remain exclusively SQLite's responsibility.</summary>
internal static class SqlHighlighting
{
    internal static void Attach(TextEditor editor)
    {
        void Apply()
        {
            var light = editor.ActualThemeVariant == ThemeVariant.Light;
            var definition = $$"""
                <SyntaxDefinition name="Workbench SQL" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                  <Color name="Keyword" foreground="{{(light ? "#005FB8" : "#569CD6")}}" />
                  <Color name="String" foreground="{{(light ? "#A31515" : "#CE9178")}}" />
                  <Color name="Comment" foreground="{{(light ? "#008000" : "#6A9955")}}" />
                  <Color name="Number" foreground="{{(light ? "#098658" : "#B5CEA8")}}" />
                  <RuleSet ignoreCase="true">
                    <Span color="Comment" begin="--" />
                    <Span color="Comment" begin="/\*" end="\*/" multiline="true" />
                    <Span color="String" begin="'" end="'" multiline="true" />
                    <Span color="String" begin="&quot;" end="&quot;" multiline="true" />
                    <Keywords color="Keyword">
                      <Word>SELECT</Word><Word>FROM</Word><Word>WHERE</Word><Word>JOIN</Word><Word>ON</Word><Word>AS</Word>
                      <Word>LEFT</Word><Word>RIGHT</Word><Word>INNER</Word><Word>OUTER</Word><Word>CROSS</Word><Word>FULL</Word>
                      <Word>GROUP</Word><Word>BY</Word><Word>HAVING</Word><Word>ORDER</Word><Word>ASC</Word><Word>DESC</Word>
                      <Word>LIMIT</Word><Word>OFFSET</Word><Word>DISTINCT</Word><Word>AND</Word><Word>OR</Word><Word>NOT</Word>
                      <Word>NULL</Word><Word>IS</Word><Word>IN</Word><Word>LIKE</Word><Word>GLOB</Word><Word>EXISTS</Word>
                      <Word>CASE</Word><Word>WHEN</Word><Word>THEN</Word><Word>ELSE</Word><Word>END</Word><Word>CAST</Word>
                      <Word>WITH</Word><Word>RECURSIVE</Word><Word>UNION</Word><Word>ALL</Word><Word>OVER</Word><Word>PARTITION</Word>
                      <Word>COUNT</Word><Word>SUM</Word><Word>AVG</Word><Word>MIN</Word><Word>MAX</Word><Word>COALESCE</Word>
                      <Word>HL7_VALUE</Word><Word>HL7_RAW</Word><Word>HL7_STATE</Word><Word>HL7_STATUS</Word>
                    </Keywords>
                    <Rule color="Number">\b\d+(\.\d+)?\b</Rule>
                  </RuleSet>
                </SyntaxDefinition>
                """;
            using var input = new StringReader(definition);
            using var xml = XmlReader.Create(input, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            editor.SyntaxHighlighting = HighlightingLoader.Load(xml, HighlightingManager.Instance);
        }
        Apply(); editor.ActualThemeVariantChanged += (_, _) => Apply();
    }
}
