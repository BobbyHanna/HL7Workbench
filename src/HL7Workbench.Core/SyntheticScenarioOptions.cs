using System.Globalization;

namespace HL7Workbench.Core;

public enum SyntheticScenarioType { AdmissionDischarge, OrderResult, CombinedEncounter }
public enum SyntheticEncounterType { Inpatient, Outpatient }
public enum SyntheticClinicalExample { Chemistry, BloodCount }
public enum SyntheticDemographicStyle { RealisticSynthetic, ObviousPlaceholders }

/// <summary>Bounded shared customization, applied before rendering any related message.</summary>
public sealed record SyntheticScenarioOverrides
{
    public string? PatientIdentifierPrefix { get; init; }
    public string? OrderCode { get; init; }
    /// <summary>F or P for the last report; earlier reports are preliminary updates of the same observations.</summary>
    public string? ResultStatus { get; init; }
    /// <summary>Replaces the first numeric analyte in the selected synthetic panel, across report updates.</summary>
    public decimal? ObservationValue { get; init; }

    /// <summary>Accepts only these shared targets. Individual patient/visit/order/control IDs and raw replacement are unsupported.</summary>
    public static SyntheticScenarioOverrides FromFields(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count > 5) throw new ArgumentException("At most five supported override targets are accepted.");
        var result = new SyntheticScenarioOverrides();
        foreach (var (path, value) in fields)
        {
            if (value is null || value.Length > 32) throw new ArgumentException("Override values must be bounded literals.");
            result = path switch
            {
                "PatientIdentifierPrefix" => result with { PatientIdentifierPrefix = value },
                "OBR-4.1" => result with { OrderCode = value },
                "OBR-25" or "OBX-11" => result.ResultStatus is { } status && status != value
                    ? throw new ArgumentException("OBR-25 and OBX-11 must specify the same result status.") : result with { ResultStatus = value },
                "OBX-5" => decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
                    ? result with { ObservationValue = number } : throw new ArgumentException("OBX-5 must be an invariant decimal literal."),
                _ => throw new ArgumentException("Unsupported override target. Use PatientIdentifierPrefix, OBR-4.1, OBR-25, OBX-11 or OBX-5.")
            };
        }
        return result.Normalize();
    }

    internal SyntheticScenarioOverrides Normalize()
    {
        var prefix = PatientIdentifierPrefix is null ? null : SyntheticScenarioOptions.Identifier(PatientIdentifierPrefix, 12);
        var code = OrderCode is null ? null : SyntheticScenarioOptions.Identifier(OrderCode, 24);
        if (ResultStatus is not (null or "F" or "P")) throw new ArgumentException("Result status must be F or P.");
        if (ObservationValue is < -100000 or > 100000) throw new ArgumentException("Synthetic numeric override must be between -100,000 and 100,000.");
        return this with { PatientIdentifierPrefix = prefix, OrderCode = code };
    }
}

/// <summary>Immutable, local-only scenario inputs. Normalization never reads a clock or environment.</summary>
public sealed record SyntheticScenarioOptions
{
    public SyntheticScenarioType Scenario { get; init; } = SyntheticScenarioType.CombinedEncounter;
    public int Patients { get; init; } = 1;
    public int EncountersPerPatient { get; init; } = 1;
    public int OrdersPerEncounter { get; init; } = 1;
    /// <summary>Number of ORU report updates per order, each containing two curated numeric observations.</summary>
    public int ResultsPerOrder { get; init; } = 1;
    public bool IncludeTransfer { get; init; }
    public SyntheticEncounterType EncounterType { get; init; }
    public SyntheticClinicalExample ClinicalExample { get; init; }
    /// <summary>New scenarios use realistic fictional profiles. Placeholders retain the original scenario-v1 bytes.</summary>
    public SyntheticDemographicStyle DemographicStyle { get; init; } = SyntheticDemographicStyle.RealisticSynthetic;
    public uint Seed { get; init; } = SyntheticGenerationOptions.DefaultSeed;
    public DateTimeOffset BaseTimestamp { get; init; } = SyntheticGenerationOptions.DefaultTimestamp;
    public string Prefix { get; init; } = "SYN";
    // Unset defaults are distinct from explicitly configured values, even when an
    // override happens to equal an old default. Normalize captures resolved values.
    private string? sendingApplication, sendingFacility, receivingApplication, receivingFacility;
    public string SendingApplication { get => sendingApplication ?? Defaults.SendingApplication; init => sendingApplication = value ?? throw new ArgumentException("Sending application cannot be null."); }
    public string SendingFacility { get => sendingFacility ?? Defaults.SendingFacility; init => sendingFacility = value ?? throw new ArgumentException("Sending facility cannot be null."); }
    public string ReceivingApplication { get => receivingApplication ?? Defaults.ReceivingApplication; init => receivingApplication = value ?? throw new ArgumentException("Receiving application cannot be null."); }
    public string ReceivingFacility { get => receivingFacility ?? Defaults.ReceivingFacility; init => receivingFacility = value ?? throw new ArgumentException("Receiving facility cannot be null."); }
    private (string SendingApplication, string SendingFacility, string ReceivingApplication, string ReceivingFacility) Defaults =>
        SyntheticDemographics.DemographicDefaults(DemographicStyle, Seed);
    /// <summary>Bundled fictional catalogue label, or the explicit code itself for an unknown configured facility.</summary>
    public string SendingFacilityName => SyntheticDemographics.FacilityName(SendingFacility);
    public string ReceivingFacilityName => SyntheticDemographics.FacilityName(ReceivingFacility);
    public string Hl7Version { get; init; } = "2.5.1";
    public SyntheticScenarioOverrides Overrides { get; init; } = new();
    public bool HasOrders => Scenario != SyntheticScenarioType.AdmissionDischarge;
    public bool HasAdt => Scenario != SyntheticScenarioType.OrderResult;
    public string Label => Scenario switch
    {
        SyntheticScenarioType.AdmissionDischarge => "Admission to discharge",
        SyntheticScenarioType.OrderResult => "Order to result",
        SyntheticScenarioType.CombinedEncounter => "Combined encounter",
        _ => throw new ArgumentException("Choose a supported scenario.")
    };
    public string SuggestedFileName => "Synthetic " + Label + ".hl7";
    public string DemographicStyleLabel => DemographicStyle switch
    {
        SyntheticDemographicStyle.RealisticSynthetic => "Realistic Synthetic",
        SyntheticDemographicStyle.ObviousPlaceholders => "Obvious Placeholders",
        _ => throw new ArgumentException("Choose a supported demographic style.")
    };

    /// <summary>Validates every factor before multiplication, temporal arithmetic or allocation.</summary>
    public SyntheticScenarioOptions Normalize()
    {
        if (!Enum.IsDefined(Scenario) || !Enum.IsDefined(EncounterType) || !Enum.IsDefined(ClinicalExample) || !Enum.IsDefined(DemographicStyle))
            throw new ArgumentException("Choose supported scenario, encounter, demographic style and clinical example options.");
        if (Patients is < 1 or > 1000 || EncountersPerPatient is < 1 or > 100 || OrdersPerEncounter is < 1 or > 20 || ResultsPerOrder is < 1 or > 10)
            throw new ArgumentException("Use 1–1,000 patients, 1–100 encounters, 1–20 orders and 1–10 report updates per order.");
        if (IncludeTransfer && (!HasAdt || EncounterType != SyntheticEncounterType.Inpatient))
            throw new ArgumentException("Transfer requires an inpatient encounter with ADT events.");
        if (!HasOrders && (OrdersPerEncounter != 1 || ResultsPerOrder != 1 || ClinicalExample != SyntheticClinicalExample.Chemistry))
            throw new ArgumentException("Order and clinical options apply only to scenarios containing orders.");
        if (Hl7Version != "2.5.1") throw new ArgumentException("Scenario templates support HL7 v2.5.1 only.");
        var overrides = (Overrides ?? throw new ArgumentException("Overrides cannot be null.")).Normalize();
        if (!HasOrders && (overrides.OrderCode is not null || overrides.ResultStatus is not null || overrides.ObservationValue is not null))
            throw new ArgumentException("Order/result overrides require a scenario containing orders.");
        var result = this with
        {
            Prefix = Identifier(Prefix, 12), Overrides = overrides,
            SendingApplication = Identifier(SendingApplication, 32), SendingFacility = Identifier(SendingFacility, 32),
            ReceivingApplication = Identifier(ReceivingApplication, 32), ReceivingFacility = Identifier(ReceivingFacility, 32),
            BaseTimestamp = new(BaseTimestamp.ToUniversalTime().Ticks - BaseTimestamp.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero)
        };
        if (result.BaseTimestamp.Year is < 1900 or > 9998) throw new ArgumentException("Base timestamp must be in years 1900–9998.");
        if (result.MessageCount > SyntheticMessageGenerator.MaximumCount)
            throw new ArgumentException("The complete scenario request exceeds 1,000 messages. Reduce the quantities.");
        // Encounters occupy successive days; all clinical events finish within one
        // day even at maximum order/report factors. Reserve the last discharge too.
        if ((DateTimeOffset.MaxValue - result.BaseTimestamp).TotalDays <= result.Patients * result.EncountersPerPatient)
            throw new ArgumentException("The complete scenario timeline exceeds the supported date range.");
        return result;
    }

    /// <summary>Exact message estimate; no message text, identifiers or PRNG state are constructed.</summary>
    public int EstimateMessageCount() => Normalize().MessageCount;
    private int MessageCount => checked(Patients * EncountersPerPatient *
        ((HasAdt ? 2 + (IncludeTransfer ? 1 : 0) : 0) + (HasOrders ? OrdersPerEncounter * (1 + ResultsPerOrder) : 0)));

    internal static string Identifier(string? value, int maximum)
    {
        if (value?.Any(char.IsControl) == true) throw new ArgumentException("Identifier control characters are not supported.");
        var text = (value ?? "").Trim();
        if (text.Length < 1 || text.Length > maximum)
            throw new ArgumentException($"Identifiers must contain 1–{maximum} ASCII letters, digits, hyphens or underscores.");
        if (text.Any(c => c is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')))
            throw new ArgumentException($"Identifiers must contain 1–{maximum} ASCII letters, digits, hyphens or underscores.");
        return text.ToUpperInvariant();
    }
}
