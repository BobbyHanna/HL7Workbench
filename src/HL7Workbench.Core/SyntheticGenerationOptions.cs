using System.Globalization;

namespace HL7Workbench.Core;

public enum SyntheticMessageType { AdtA01, AdtA04, AdtA08, OrmO01, OruR01 }

/// <summary>Local synthetic configuration. Normalization never reads a clock, source message or environment.</summary>
public sealed record SyntheticGenerationOptions
{
    public const uint DefaultSeed = 42;
    public static DateTimeOffset DefaultTimestamp => new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public SyntheticMessageType MessageType { get; init; } = SyntheticMessageType.AdtA01;
    public int Count { get; init; } = 1;
    public uint Seed { get; init; } = DefaultSeed;
    public DateTimeOffset BaseTimestamp { get; init; } = DefaultTimestamp;
    public string Prefix { get; init; } = "SYN";

    /// <summary>Absence of this property retains the original v1 contract, including old serialized inputs.
    /// New interactive configurations explicitly select RealisticSynthetic.</summary>
    public SyntheticDemographicStyle DemographicStyle { get; init; } = SyntheticDemographicStyle.ObviousPlaceholders;
    public int GeneratorVersion => DemographicStyle == SyntheticDemographicStyle.RealisticSynthetic ? SyntheticMessageGenerator.RealisticVersion : SyntheticMessageGenerator.Version;
    public string DemographicStyleLabel => DemographicStyle switch
    {
        SyntheticDemographicStyle.RealisticSynthetic => "Realistic Synthetic",
        SyntheticDemographicStyle.ObviousPlaceholders => "Obvious Placeholders",
        _ => throw new ArgumentException("Choose a supported demographic style.")
    };
    // Preserve unset defaults until the operation's actual seed is resolved.
    // A normalized configuration captures those defaults for reproducible replay.
    private string? sendingApplication, sendingFacility, receivingApplication, receivingFacility;
    public string SendingApplication { get => sendingApplication ?? Defaults.SendingApplication; init => sendingApplication = value ?? throw new ArgumentException("Sending application cannot be null."); }
    public string SendingFacility { get => sendingFacility ?? Defaults.SendingFacility; init => sendingFacility = value ?? throw new ArgumentException("Sending facility cannot be null."); }
    public string ReceivingApplication { get => receivingApplication ?? Defaults.ReceivingApplication; init => receivingApplication = value ?? throw new ArgumentException("Receiving application cannot be null."); }
    public string ReceivingFacility { get => receivingFacility ?? Defaults.ReceivingFacility; init => receivingFacility = value ?? throw new ArgumentException("Receiving facility cannot be null."); }
    private (string SendingApplication, string SendingFacility, string ReceivingApplication, string ReceivingFacility) Defaults =>
        SyntheticDemographics.DemographicDefaults(DemographicStyle, Seed);
    public string SendingFacilityName => SyntheticDemographics.FacilityName(SendingFacility);
    public string ReceivingFacilityName => SyntheticDemographics.FacilityName(ReceivingFacility);
    /// <summary>Optional patient namespace for realistic output; the synthetic MR domain is always retained.</summary>
    public string? PatientIdentifierPrefix { get; init; }

    /// <summary>Returns canonical uppercase ASCII identifiers and a UTC timestamp at whole-second precision.</summary>
    public SyntheticGenerationOptions Normalize()
    {
        if (!Enum.IsDefined(MessageType)) throw new ArgumentException("Choose one of the five supported message types.");
        if (!Enum.IsDefined(DemographicStyle)) throw new ArgumentException("Choose a supported demographic style.");
        if (Count is < 1 or > SyntheticMessageGenerator.MaximumCount) throw new ArgumentException("Generate from 1 to 1,000 messages per request.");
        var prefix = (Prefix ?? "").Trim().ToUpperInvariant();
        if (prefix.Length is < 1 or > 12 || prefix.Any(c => c is not (>= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')))
            throw new ArgumentException("Identifier prefix must contain 1–12 ASCII letters, digits, hyphens or underscores.");
        var utc = BaseTimestamp.ToUniversalTime();
        if (utc.Year is < 1900 or > 9998) throw new ArgumentException("Base timestamp must be in years 1900–9998 with an explicit offset.");
        utc = new(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
        var patientPrefix = PatientIdentifierPrefix is null ? null : SyntheticScenarioOptions.Identifier(PatientIdentifierPrefix, 12);
        var transport = Defaults;
        var sendApp = SyntheticScenarioOptions.Identifier(SendingApplication, 32); var sendFacility = SyntheticScenarioOptions.Identifier(SendingFacility, 32);
        var receiveApp = SyntheticScenarioOptions.Identifier(ReceivingApplication, 32); var receiveFacility = SyntheticScenarioOptions.Identifier(ReceivingFacility, 32);
        if (DemographicStyle == SyntheticDemographicStyle.ObviousPlaceholders &&
            (patientPrefix is not null || sendApp != transport.SendingApplication || sendFacility != transport.SendingFacility ||
             receiveApp != transport.ReceivingApplication || receiveFacility != transport.ReceivingFacility))
            throw new ArgumentException("Standalone application, facility and patient-prefix overrides require Realistic Synthetic. Clear overrides to reproduce v1 placeholders.");
        return this with { Prefix = prefix, BaseTimestamp = utc, PatientIdentifierPrefix = patientPrefix,
            SendingApplication = sendApp, SendingFacility = sendFacility, ReceivingApplication = receiveApp, ReceivingFacility = receiveFacility };
    }
    public string TypeLabel => MessageType switch
    {
        SyntheticMessageType.AdtA01 => "ADT^A01", SyntheticMessageType.AdtA04 => "ADT^A04", SyntheticMessageType.AdtA08 => "ADT^A08",
        SyntheticMessageType.OrmO01 => "ORM^O01", SyntheticMessageType.OruR01 => "ORU^R01", _ => throw new ArgumentException("Unsupported message type.")
    };
    public string SuggestedFileName => "Synthetic " + TypeLabel.Replace('^', ' ') + ".hl7";
    /// <summary>Predicts a content identifier without generating any message or consuming PRNG state.</summary>
    public string ControlId(int oneBasedIndex)
    {
        var normalized = Normalize();
        if (oneBasedIndex < 1 || oneBasedIndex > normalized.Count) throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));
        return normalized.DemographicStyle == SyntheticDemographicStyle.RealisticSynthetic
            ? string.Create(CultureInfo.InvariantCulture, $"SYN2-{normalized.Seed:X8}-{oneBasedIndex:D4}")
            : string.Create(CultureInfo.InvariantCulture, $"{normalized.Prefix}-{normalized.Seed:X8}-{normalized.TypeLabel[4..]}-{oneBasedIndex:D4}");
    }
}
