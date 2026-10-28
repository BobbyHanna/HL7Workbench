using System.Globalization;
using System.Text;
using HL7Workbench.Querying;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

public sealed partial class SqlQueryViewModel
{
    public HashSet<(int Row, int Column)> Selection { get; } = [];
    private (int PageRow, int Column)? anchor;
    public string FullValue { get; private set; } = "";
    public string FullValueKind { get; private set; } = "Select a cell, then Inspect full value.";
    public bool HasSelectedCell => Selection.Count == 1;
    public bool CanNavigate => CanManage && Selection.Select(s => s.Row).Distinct().Take(2).Count() == 1 &&
        Results?.SourceForRow(Selection.First().Row) is { } source && documents.Documents.Any(d => d.Id == source.DocumentId && d.SearchRevision == source.Revision);
    public void ClearSelection() { Selection.Clear(); anchor = null; Notify(); }
    /// <summary>Page-local rectangle and additive selection; journal indices remain authoritative after presentation sorting.</summary>
    public void SelectCell(int pageRow, int column, bool extend = false, bool additive = false)
    {
        if (!CanManage || Results is null || pageRow < 0 || pageRow >= Page.Count || column < -1 || column >= Results.Columns.Count) return;
        if (!additive) Selection.Clear();
        var start = extend && anchor is { } a ? a : (pageRow, Math.Max(0, column));
        for (var r = Math.Min(start.Item1, pageRow); r <= Math.Max(start.Item1, pageRow); r++)
        for (var c = column == -1 ? 0 : Math.Min(start.Item2, column); c <= (column == -1 ? Results.Columns.Count - 1 : Math.Max(start.Item2, column)); c++)
            Selection.Add((Page[r].RowNumber - 1, c));
        if (!extend) anchor = (pageRow, Math.Max(0, column));
        FullValue = ""; FullValueKind = "Select one cell, then Inspect full value."; Notify();
    }
    public string? CopySelectionText()
    {
        if (!CanManage || Results is null || Selection.Count == 0) return null;
        var columns = Selection.Select(s => s.Column).Distinct().Order().ToArray();
        var rows = Page.Select(p => p.RowNumber - 1).Where(r => Selection.Any(s => s.Row == r)).ToArray();
        var builder = new StringBuilder();
        static string Quote(string text) => "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        foreach (var index in rows)
        {
            var row = Results.ReadRow(index);
            var text = string.Join('\t', columns.Select(c => !Selection.Contains((index, c)) ? "" :
                row.Cells[c].Kind == QueryValueKind.Null ? "(SQL NULL)" : Quote(CellText(row.Cells[c]))));
            if ((long)builder.Length + text.Length + 2 > 1024 * 1024)
            { Status = "Copy exceeds the 1 Mi-character clipboard limit. Select fewer cells or export all rows to CSV."; Notify(); return null; }
            builder.Append(text).Append("\r\n");
        }
        return builder.ToString();
    }
    public Task InspectAsync() => CanManage && Results is { } result && Selection.Count == 1 ? OperateAsync("Reading the complete bounded value…", async token =>
    {
        var selected = Selection.Single(); var row = await Task.Run(() => result.ReadRow(selected.Row), token); var cell = row.Cells[selected.Column];
        FullValue = CellText(cell); FullValueKind = cell.Kind == QueryValueKind.Null ? "SQL NULL (no text value)" :
            $"{cell.Kind} · {FullValue.Length:N0} displayed characters · complete value within materialization limits";
        Status = "Complete selected value loaded. No source attachment decoding is performed.";
    }) : Task.CompletedTask;
    private static string CellText(QueryCell c) => c.Kind switch
    {
        QueryValueKind.Null => "", QueryValueKind.Text => c.Text!, QueryValueKind.Integer => c.Integer.ToString(CultureInfo.InvariantCulture),
        QueryValueKind.Real => c.Real.ToString("R", CultureInfo.InvariantCulture), QueryValueKind.Blob => Convert.ToHexString(c.Blob!), _ => ""
    };
    public Task NavigateAsync() => CanNavigate && Results is { } result ? OperateAsync("Opening the original source message…", async token =>
    {
        var source = result.SourceForRow(Selection.First().Row);
        var document = source is null ? null : documents.Documents.FirstOrDefault(d => d.Id == source.DocumentId && d.SearchRevision == source.Revision);
        if (source is null || document is null) { Status = "Original source is closed or changed. Run the query again; no identity is inferred."; return; }
        // MessageId constructors are Workspace-owned. Verify the frozen position against
        // that existing identity; the displayed query row number never participates.
        var position = source.MessageNumber;
        if (position <= 0 || position > document.MessageCount || document.GetMessageId(position).Value != source.MessageId)
        { Status = "Captured source message is no longer available."; return; }
        await document.NavigateAsync(position, token); token.ThrowIfCancellationRequested();
        if (!documents.Documents.Contains(document) || document.SearchRevision != source.Revision)
        { Status = "Source changed during navigation; run the query again."; return; }
        OpenDocumentRequested?.Invoke(document.Id); Status = "Opened the authoritative original message without changing source content.";
    }) : Task.CompletedTask;
}
