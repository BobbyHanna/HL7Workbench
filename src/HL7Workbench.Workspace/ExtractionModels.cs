using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public enum ExtractionScope { CurrentMessage, WholeDocument }
public enum ExtractionMode { FirstMatch, ExpandMatches }
public enum ExtractionFilterOperator { None, Contains, Equals, Missing, Empty, ExplicitNull, Unavailable, NumericGreaterThan, NumericLessThan, NumericEquals }

public sealed record ExtractionColumn(string Label, string Path)
{
    public override string ToString() => "Extraction column";
}

/// <summary>Configuration contains selectors/preferences only. Literal filter values and source data are never serialized here.</summary>
public sealed record ExtractionConfiguration(IReadOnlyList<ExtractionColumn> Columns, ExtractionMode Mode = ExtractionMode.FirstMatch,
    bool DecodeEscapes = true, int FilterColumn = 0, ExtractionFilterOperator FilterOperator = ExtractionFilterOperator.None,
    int SortColumn = -1, bool Descending = false, bool NumericSort = false)
{
    public ExtractionConfiguration Snapshot()
    {
        if (Columns is null || Columns.Count is < 1 or > 32 || !Enum.IsDefined(Mode) || !Enum.IsDefined(FilterOperator) ||
            FilterColumn < 0 || FilterColumn >= Columns.Count || SortColumn < -1 || SortColumn >= Columns.Count)
            throw new ArgumentException("Choose 1–32 extraction columns and supported preferences.");
        var columns = Columns.Select(c =>
        {
            if (c is null || string.IsNullOrWhiteSpace(c.Label) || c.Label.Length > 120 || c.Label.Any(char.IsControl))
                throw new ArgumentException("Use nonblank column labels of at most 120 characters without controls.");
            _ = new System.Text.UTF8Encoding(false, true).GetByteCount(c.Label);
            return c with { Label = c.Label.Trim(), Path = Hl7FieldSelection.Parse(c.Path).CanonicalPath };
        }).ToArray();
        if (columns.Select(c => c.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length)
            throw new ArgumentException("Use distinct column labels.");
        var headers = new[] { "Source Document", "Captured Revision", "Captured Message", "Message Identity", "Match" }
            .Concat(columns.SelectMany(c => new[] { c.Label, c.Label + " State", c.Label + " Status", c.Label + " Path" })).ToArray();
        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new ArgumentException("Column labels must not conflict with CSV metadata or companion headers.");
        return this with { Columns = Array.AsReadOnly(columns) };
    }
    public override string ToString() => "Extraction configuration";
}

public sealed record ExtractionOptions
{
    public int MaximumRows { get; init; } = 1_000_000;
    public int MaximumRowsPerMessage { get; init; } = 4096;
    public long MaximumJournalBytes { get; init; } = 1024L * 1024 * 1024;
    public int MaximumValueCharacters { get; init; } = 65_536;
    public int DistinctLimit { get; init; } = 1024;
    public int SortCacheBytes { get; init; } = 4 * 1024 * 1024;
    public long MaximumExportBytes { get; init; } = 4L * 1024 * 1024 * 1024;
    public string? StorageRoot { get; init; }
    public const int MaximumRowBytes = 16 * 1024 * 1024;
    public const int PageSize = 100;
    public const int PreviewCharacters = 256;
    internal void Validate()
    {
        if (MaximumRows is < 1 or > 1_000_000 || MaximumRowsPerMessage is < 1 or > 4096 ||
            MaximumJournalBytes is < 1 or > 1024L * 1024 * 1024 || MaximumValueCharacters is < 1 or > 65_536 ||
            DistinctLimit is < 1 or > 1024 || SortCacheBytes is < 1024 or > 4 * 1024 * 1024 ||
            MaximumExportBytes is < 1 or > 4L * 1024 * 1024 * 1024) throw new ArgumentException("Unsupported extraction limits.");
    }
}

public sealed record ExtractionColumnSummary(string Label, long Missing, long Empty, long ExplicitNull, long Unavailable, int DistinctValues, bool DistinctLowerBound);
public sealed record ExtractionSummary(int TotalMessages, int ExaminedMessages, int MessagesWithMatches, int Rows,
    int UnreadableMessages, int ExpansionLimitedMessages, bool Truncated, string Limit, IReadOnlyList<ExtractionColumnSummary> Columns);
public sealed record ExtractionProgress(int Examined, int Total, int Rows);

/// <summary>Authoritative journal row. MessageKey is an opaque session identity, not an HL7 patient/control identifier.</summary>
public sealed record ExtractionRow(long MessageKey, int CapturedPosition, int Expansion, IReadOnlyList<Hl7ExtractedValue> Values)
{ public override string ToString() => "Extraction row"; }
public sealed record ExtractionPreviewCell(string Text, string State, string Status, string Path, bool Abbreviated)
{ public override string ToString() => "Extraction presentation cell"; }
public sealed record ExtractionPreviewRow(Guid ResultId, int RowIndex, long MessageKey, int CapturedPosition, int Expansion, IReadOnlyList<ExtractionPreviewCell> Values)
{ public override string ToString() => "Extraction presentation row"; }
public sealed record ExtractionFilter(int Column, ExtractionFilterOperator Operator, string Literal = "")
{ public override string ToString() => "Extraction result filter"; }
public sealed record ExtractionSort(int Column = -1, bool Descending = false, bool Numeric = false);
