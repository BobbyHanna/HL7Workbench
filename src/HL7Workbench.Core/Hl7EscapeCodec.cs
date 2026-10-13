using System.Collections.ObjectModel;
using System.Text;

namespace HL7Workbench.Core;

public sealed record Hl7EscapeResult(string Text, ReadOnlyCollection<Hl7Diagnostic> Diagnostics);

public static class Hl7EscapeCodec
{
    public static Hl7EscapeResult Decode(string raw, Hl7Delimiters delimiters, Encoding? encoding = null,
        int maxOutputCharacters = 8 * 1024 * 1024, int maxDiagnostics = 1024)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(delimiters);
        if (maxOutputCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maxOutputCharacters));
        if (maxDiagnostics < 1) throw new ArgumentOutOfRangeException(nameof(maxDiagnostics));
        Hl7Parser.Check(raw.Length, maxOutputCharacters, nameof(maxOutputCharacters));
        encoding = Hl7ParseOptions.Strict(encoding ?? new UTF8Encoding(false, true));
        var result = new StringBuilder(raw.Length);
        var diagnostics = new Hl7DiagnosticCollector(maxDiagnostics);
        void AddDiagnostic(Hl7Diagnostic diagnostic)
        {
            diagnostics.Add(diagnostic);
        }
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] != delimiters.Escape) { result.Append(raw[i]); continue; }
            var end = raw.IndexOf(delimiters.Escape!.Value, i + 1);
            if (end < 0)
            {
                AddDiagnostic(new("UnterminatedEscape", Hl7DiagnosticSeverity.Warning, "Escape sequence has no closing delimiter.", i));
                result.Append(raw.AsSpan(i));
                break;
            }
            var token = raw[(i + 1)..end];
            string? decoded = token switch
            {
                "F" => delimiters.Field.ToString(), "S" => delimiters.Component.ToString(),
                "R" => delimiters.Repetition.ToString(), "T" => delimiters.Subcomponent?.ToString(),
                "E" => delimiters.Escape?.ToString(), "P" => delimiters.Truncation?.ToString(), _ => null
            };
            if (decoded is null && token.StartsWith('X'))
            {
                try
                {
                    if (token.Length <= 1) throw new FormatException();
                    var bytes = Convert.FromHexString(token.AsSpan(1));
                    Hl7Parser.Check((long)result.Length + encoding.GetCharCount(bytes) + raw.Length - end - 1,
                        maxOutputCharacters, nameof(maxOutputCharacters));
                    decoded = encoding.GetString(bytes);
                }
                catch (Exception exception) when (exception is FormatException or DecoderFallbackException)
                {
                    AddDiagnostic(new("InvalidHexEscape", Hl7DiagnosticSeverity.Warning, "Hex escape cannot be decoded using the selected encoding.", i));
                }
            }
            else if (decoded is null)
                AddDiagnostic(new("UnsupportedEscape", Hl7DiagnosticSeverity.Warning, "Escape is unsupported and retained verbatim.", i));
            result.Append(decoded ?? raw[i..(end + 1)]);
            i = end;
        }
        return new(result.ToString(), diagnostics.ToList().AsReadOnly());
    }

    public static string Encode(string text, Hl7Delimiters delimiters, int maxOutputCharacters = 8 * 1024 * 1024) =>
        Encode(text, delimiters, maxOutputCharacters, null);

    public static string Encode(string text, Hl7Delimiters delimiters, int maxOutputCharacters, Encoding? encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(delimiters);
        if (maxOutputCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maxOutputCharacters));
        if (!delimiters.IsValid) throw new ArgumentException("Invalid delimiters.", nameof(delimiters));
        Hl7Parser.Check(text.Length, maxOutputCharacters, nameof(maxOutputCharacters));
        encoding = Hl7ParseOptions.Strict(encoding ?? new UTF8Encoding(false, true));
        var size = MeasureEncoded(text, delimiters, encoding);
        Hl7Parser.Check(size.Characters, maxOutputCharacters, nameof(maxOutputCharacters));
        if (text == "\"\"")
            return $"{delimiters.Escape}X{Convert.ToHexString(encoding.GetBytes(text))}{delimiters.Escape}";
        var result = new StringBuilder((int)size.Characters);
        foreach (var c in text)
        {
            var code = EscapeCode(c, delimiters);
            if (code == '\0') result.Append(c);
            else result.Append(delimiters.Escape).Append(code).Append(delimiters.Escape);
        }
        return result.ToString();
    }

    internal static (long Characters, long Bytes) MeasureEncoded(string text, Hl7Delimiters delimiters, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!delimiters.IsValid) throw new ArgumentException("Invalid delimiters.", nameof(delimiters));
        if (text == "\"\"")
        {
            if (delimiters.Escape is null) throw new InvalidOperationException("Literal quotes require an escape character; use SetNull for explicit null.");
            var characters = 3L + 2L * encoding.GetByteCount(text);
            return (characters, characters * CharacterBytes('X', encoding));
        }
        long length = text.Length, bytes = encoding.GetByteCount(text);
        foreach (var c in text)
        {
            if (c is '\r' or '\n') throw new ArgumentException("Text edits cannot contain segment terminators.", nameof(text));
            var code = EscapeCode(c, delimiters);
            if (code == '\0') continue;
            if (delimiters.Escape is null) throw new InvalidOperationException("Literal structural delimiters require a declared escape character.");
            length += 2;
            bytes += 2L * CharacterBytes(delimiters.Escape.Value, encoding) + CharacterBytes(code, encoding) - CharacterBytes(c, encoding);
        }
        return (length, bytes);
    }

    internal static int CharacterBytes(char c, Encoding encoding) => encoding.GetByteCount(stackalloc char[] { c });

    private static char EscapeCode(char c, Hl7Delimiters delimiters) =>
        c == delimiters.Field ? 'F' : c == delimiters.Component ? 'S' : c == delimiters.Repetition ? 'R' :
        c == delimiters.Subcomponent ? 'T' : c == delimiters.Escape ? 'E' : c == delimiters.Truncation ? 'P' : '\0';
}
