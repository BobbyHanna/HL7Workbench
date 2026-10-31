using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public enum BulkFieldOperation { UniqueValues, SearchReplace, Assign }
public enum BulkFieldScope { CurrentMessage, ActiveFile, NavigatorFilter }
public enum BulkFieldSort { Value, Frequency }

/// <summary>Complete result bytes could not be registered/presented. The attempted publication was rolled back.</summary>
public sealed class BulkFieldPublicationException : InvalidOperationException
{
    internal BulkFieldPublicationException() : base("Result publication failed; no result retained and source unchanged.") { }
}

/// <summary>One exact structural selector. An omitted repetition means the complete field, not ANY repetition.</summary>
public sealed record BulkFieldRequest(Hl7Path Path, BulkFieldOperation Operation, BulkFieldScope Scope,
    string Find = "", string Replacement = "", bool AllSegmentOccurrences = false)
{
    public override string ToString() => "Bulk field request";
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Path);
        if (!Enum.IsDefined(Operation) || !Enum.IsDefined(Scope)) throw new ArgumentException("Choose a supported operation and scope.");
        if (Find is null || Replacement is null || Find.Length > 65536) throw new ArgumentException("Find values are limited to 65,536 characters.");
        if (Operation != BulkFieldOperation.UniqueValues)
        {
            if (Path.Segment == "MSH" && Path.Field <= 2) throw new InvalidOperationException("MSH-1/MSH-2 cannot be changed by bulk operations.");
            if (StructuredRawValue.UnavailableReason(Replacement) is { } reason) throw new ArgumentException(reason);
        }
    }
}

/// <summary>Hard resource ceilings; reaching a ceiling fails the operation, never silently truncates results.</summary>
public sealed record BulkFieldOptions
{
    public int MaximumMessages { get; init; } = 100_000;
    public int MaximumNodes { get; init; } = 250_000;
    public int MaximumDistinctValues { get; init; } = 4096;
    public int MaximumValueCharacters { get; init; } = 65_536;
    public int MaximumRetainedValueCharacters { get; init; } = 2 * 1024 * 1024;
    public int MaximumOverlayCharacters { get; init; } = MessageDocument.MaximumRetainedEditCharacters;
    public long MaximumSourceBytes { get; init; } = 1024L * 1024 * 1024;
    public long MaximumOutputBytes { get; init; } = 1024L * 1024 * 1024;
    public string? StorageRoot { get; init; }
    public const int PageSize = 100;
    internal void Validate()
    {
        if (MaximumMessages is < 1 or > 100_000 || MaximumNodes is < 1 or > 250_000 ||
            MaximumDistinctValues is < 1 or > 4096 || MaximumValueCharacters is < 1 or > 65536 ||
            MaximumRetainedValueCharacters is < 1 or > 2 * 1024 * 1024 ||
            MaximumOverlayCharacters is < 1 or > MessageDocument.MaximumRetainedEditCharacters ||
            MaximumSourceBytes is < 1 or > 1024L * 1024 * 1024 || MaximumOutputBytes is < 1 or > 1024L * 1024 * 1024)
            throw new ArgumentException("Unsupported bulk operation limits.");
    }
}

public sealed record BulkFieldProgress(string Phase, int Completed, int Total);
public sealed record BulkFieldSummary(int ScannedMessages, int MessagesContainingPath, int Nodes, int Absent,
    int Skipped, int Unchanged, int Invalid, int Replacements, int ChangedMessages, int SelectedSlots);

/// <summary>Complete bounded raw value and counts. Display abbreviation never participates in grouping.</summary>
public sealed class BulkUniqueValue
{
    public string Raw { get; }
    public Hl7ValueState State { get; }
    public int Occurrences { get; internal set; }
    public int NodeCount => State == Hl7ValueState.Missing ? 0 : Occurrences;
    public int MessageCount => Messages.Count;
    internal Dictionary<MessageId, int> Messages { get; } = [];
    internal BulkUniqueValue(string raw, Hl7ValueState state) { Raw = raw; State = state; }
    public string Preview => State == Hl7ValueState.Missing ? "<absent>" : State == Hl7ValueState.Empty ? "<empty>" : BulkFieldPreview.Abbreviate(Raw);
    public override string ToString() => "Unique structural value";
}

/// <summary>One proposed or skipped address; numbers and identity refer to the captured document.</summary>
public sealed record BulkFieldChange(MessageId MessageId, int MessageNumber, Hl7Path Path, CharacterRange? Range,
    string Original, string Replacement, string Status)
{
    public string OriginalPreview => BulkFieldPreview.Abbreviate(Original);
    public string ReplacementPreview => BulkFieldPreview.Abbreviate(Replacement);
    public override string ToString() => "Bulk structural change";
}

internal static class BulkFieldPreview
{
    internal static string Abbreviate(string value)
    {
        var size = Math.Min(value.Length, 256);
        if (size > 0 && size < value.Length && char.IsHighSurrogate(value[size - 1])) size--;
        return new string(value.AsSpan(0, size).ToArray().Select(c => char.IsControl(c) ? ' ' : c).ToArray()) + (size < value.Length ? "…" : "");
    }
}
