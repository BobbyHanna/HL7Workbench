using System.Text;

namespace HL7Workbench.Core;

/// <summary>Shared UTF-8/CRLF CSV spelling and spreadsheet-formula protection. Callers bound rows, cells and total output.</summary>
public static class CsvRecordEncoder
{
    public static byte[] Encode(IEnumerable<string?> values)
    {
        var text = string.Join(",", values.Select(value =>
        {
            var cell = value ?? ""; var start = 0;
            while (start < cell.Length && (char.IsWhiteSpace(cell[start]) || char.IsControl(cell[start]))) start++;
            if (start < cell.Length && cell[start] is '=' or '+' or '-' or '@' || cell.Length > 0 && cell[0] is '\t' or '\r' or '\n') cell = "'" + cell;
            return "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        })) + "\r\n";
        return new UTF8Encoding(false, true).GetBytes(text);
    }
}
