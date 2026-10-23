using System.Globalization;

namespace HL7Workbench.Scripting;

public enum DeidentificationPreset { PatientDeidentifier, IdentifierPseudonymizer, TimestampShifter }

/// <summary>Run-local de-identification aid configuration, never a privacy/compliance certification.</summary>
public sealed record DeidentificationOptions
{
    public const int ImplementationVersion = 1;
    public const int PatientImplementationVersion = 2;
    public DeidentificationPreset Preset { get; init; } = DeidentificationPreset.PatientDeidentifier;
    public uint Seed { get; init; } = 42;
    /// <summary>One calendar-day displacement for the whole execution; null derives a nonzero shift from the seed.</summary>
    public int? ShiftDays { get; init; }
    /// <summary>Defensive run-local identity budget. No mapping is persisted; exhaustion fails the whole worker job.</summary>
    public int MaximumMappings { get; init; } = 100_000;
    internal void Validate()
    {
        if (!Enum.IsDefined(Preset) || ShiftDays is < -3660 or > 3660 || MaximumMappings is < 1 or > 100_000)
            throw new ArgumentException("Unsupported de-identification configuration.");
    }
}

/// <summary>Counts only. Contains no source values, identifiers, dates, filenames, excerpts or mapping keys.</summary>
public sealed record DeidentificationSummary(long Messages, long MissingIdentities, long AmbiguousIdentities,
    long ReplacedValues, long SuppressedValues, long ShiftedTimestamps, long UnsupportedTimestamps,
    long UnknownSegments, long HighRiskFields, long RetainedValues)
{
    public DeidentificationPreset Preset { get; init; }
    public int Version { get; init; } = DeidentificationOptions.ImplementationVersion;
    public IReadOnlyList<DeidentificationCategoryCount> Categories { get; init; } = [];
    public long RetainedValuesRequiringReview { get; init; }
    public long OmittedFindings { get; init; }
    public bool RequiresReview => MissingIdentities + AmbiguousIdentities + UnsupportedTimestamps + UnknownSegments + HighRiskFields + RetainedValuesRequiringReview > 0;
    public string Status => RequiresReview ? "Completed — review required" : "Completed — no flagged findings";
    /// <summary>Fixed count-only report schema suitable for one Complete row through the existing CSV journal.</summary>
    public static string[] Columns => ["Status", "Version", "Messages", "Missing identities", "Ambiguous identities", "Replaced values",
        "Suppressed values", "Shifted timestamps", "Unsupported timestamps", "Unknown segments", "High risk fields", "Retained values", "Unassessed scope", "Preset",
        .. Enum.GetValues<DeidentificationCategory>().SelectMany(c => new[] { c + " transformed", c + " suppressed" }),
        "Retained values requiring review", "Omitted findings"];
    public string[] Cells => [Status, Version.ToString(CultureInfo.InvariantCulture),
        Messages.ToString(CultureInfo.InvariantCulture), MissingIdentities.ToString(CultureInfo.InvariantCulture), AmbiguousIdentities.ToString(CultureInfo.InvariantCulture),
        ReplacedValues.ToString(CultureInfo.InvariantCulture), SuppressedValues.ToString(CultureInfo.InvariantCulture), ShiftedTimestamps.ToString(CultureInfo.InvariantCulture),
        UnsupportedTimestamps.ToString(CultureInfo.InvariantCulture), UnknownSegments.ToString(CultureInfo.InvariantCulture), HighRiskFields.ToString(CultureInfo.InvariantCulture), RetainedValues.ToString(CultureInfo.InvariantCulture),
        ScopeNotice, Preset.ToString(), .. Enum.GetValues<DeidentificationCategory>().SelectMany(c =>
        {
            var count = Categories.FirstOrDefault(v => v.Category == c);
            return new[] { (count?.Transformed ?? 0).ToString(CultureInfo.InvariantCulture), (count?.Suppressed ?? 0).ToString(CultureInfo.InvariantCulture) };
        }), RetainedValuesRequiringReview.ToString(CultureInfo.InvariantCulture), OmittedFindings.ToString(CultureInfo.InvariantCulture)];
    public const string ScopeNotice = "General limitation: container bytes, nested metadata and retained clinical values are not certified de-identified.";
}

public enum DeidentificationCategory { Names, Addresses, Contacts, Demographics, Identifiers, Providers, Narratives, Attachments, Other }
public sealed record DeidentificationCategoryCount(DeidentificationCategory Category, long Transformed, long Suppressed);
