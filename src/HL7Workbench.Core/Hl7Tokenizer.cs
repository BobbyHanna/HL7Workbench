namespace HL7Workbench.Core;

internal readonly record struct Hl7Slice(int Start, int Length);

internal static class Hl7Tokenizer
{
    // An unmatched escape does not suppress subsequent structural delimiters.
    internal static IEnumerable<Hl7Slice> Split(string text, char? separator, char? escape)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == escape)
            {
                var closing = text.IndexOf(escape!.Value, i + 1);
                if (closing >= 0) { i = closing; continue; }
            }
            if (text[i] != separator) continue;
            yield return new(start, i - start);
            start = i + 1;
        }
        yield return new(start, text.Length - start);
    }

    internal static bool HasAmbiguousEscape(string text, Hl7Delimiters delimiters)
    {
        if (delimiters.Escape is not { } escape) return false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != escape) continue;
            var closing = text.IndexOf(escape, i + 1);
            if (closing < 0) break;
            for (var j = i + 1; j < closing; j++)
                if (text[j] == delimiters.Field || text[j] == delimiters.Repetition ||
                    text[j] == delimiters.Component || text[j] == delimiters.Subcomponent)
                    return true;
            i = closing;
        }
        return false;
    }

    internal static bool ContainsUnescaped(string value, IEnumerable<char> forbidden, char? escape)
    {
        var separators = forbidden.ToArray();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is '\r' or '\n') return true;
            if (value[i] == escape)
            {
                var closing = value.IndexOf(escape!.Value, i + 1);
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
