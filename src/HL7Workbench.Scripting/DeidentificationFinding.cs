using System.Globalization;

namespace HL7Workbench.Scripting;

// Segment names are a fixed vocabulary, never untrusted identifiers from the source.
public enum DeidentificationSegment { Unknown, MSH, EVN, PID, PD1, NK1, PV1, PV2, IN1, IN2, GT1, ORC, OBR, OBX, NTE, DG1, AL1 }
public enum DeidentificationReason
{
    MissingIdentity, AmbiguousIdentity, UnknownSegment, UnqualifiedIdentifier, UnsupportedIdentifier,
    UnsupportedTimestamp, SensitiveContentSuppressed, RetainedClinicalValue, RetainedOutsidePolicy, InvalidSyntheticAge
}

/// <summary>Bounded structural metadata only: no original values, excerpts or identity keys.</summary>
public sealed record DeidentificationFinding(int MessageNumber, int SegmentNumber, DeidentificationSegment Segment,
    int SegmentOccurrence, int FieldNumber, DeidentificationReason Reason)
{
    public const int MaximumFindings = 1000;
    public static string[] Columns => ["Message", "Segment position", "Segment", "Occurrence", "Field", "Reason"];
    public string[] Cells => [MessageNumber.ToString(CultureInfo.InvariantCulture), SegmentNumber.ToString(CultureInfo.InvariantCulture),
        Segment.ToString(), SegmentOccurrence.ToString(CultureInfo.InvariantCulture), FieldNumber.ToString(CultureInfo.InvariantCulture), Reason.ToString()];
    public string Path => SegmentNumber == 0 ? "PID-3 (message identity)" : Segment == DeidentificationSegment.Unknown ?
        $"Unknown segment at position {SegmentNumber}" + (FieldNumber > 0 ? $" / field {FieldNumber}" : "") :
        $"{Segment}[{SegmentOccurrence}]" + (FieldNumber > 0 ? $"-{FieldNumber}" : "");
    public string Description => Reason switch
    {
        DeidentificationReason.MissingIdentity => "No uniquely qualified patient identity; cross-message association was not inferred.",
        DeidentificationReason.AmbiguousIdentity => "Conflicting patient identities or birth evidence; associations were not merged.",
        DeidentificationReason.UnknownSegment => "Unknown segment; its schema and associations could not be assessed.",
        DeidentificationReason.UnqualifiedIdentifier => "Missing assigning authority or unresolved association; replacement does not establish cross-message identity consistency.",
        DeidentificationReason.UnsupportedIdentifier => "Identifier could not be resolved; present content was suppressed.",
        DeidentificationReason.UnsupportedTimestamp => "Unsupported timestamp precision, format or range; not shifted. Patient mode suppresses it; narrow modes can retain it.",
        DeidentificationReason.SensitiveContentSuppressed => "Unsupported or potentially sensitive content was suppressed; inspect structure and context before disclosure.",
        DeidentificationReason.RetainedClinicalValue => "Clinical/event code, numeric result or unit retained by policy; clinical patterns can remain identifying.",
        DeidentificationReason.RetainedOutsidePolicy => "Content retained outside this narrow preset's transformation policy; may include identifiers.",
        _ => "Synthetic age could not be represented for the reference date; inspect the output."
    };
}
