namespace HL7Workbench.Core;

internal static class Hl7Tokenizer
{
    internal static bool ContainsUnescaped(string value, IEnumerable<char> forbidden, char? escape)
    {
        var separators = forbidden.ToArray();
        var source = new StringHl7TextSource(value);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is '\r' or '\n') return true;
            if (value[i] == escape)
            {
                var closing = checked((int)Hl7StructureScanner.ClosingEscape(source, escape!.Value, i, value.Length));
                if (closing >= 0)
                {
                    if (value.AsSpan(i, closing - i + 1).ContainsAny('\r', '\n')) return true;
                    i = closing;
                    continue;
                }
            }
            if (separators.Contains(value[i])) return true;
        }
        return false;
    }
}
