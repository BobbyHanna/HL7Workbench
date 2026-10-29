using System.Globalization;
using static HL7Workbench.Core.SyntheticMessageGenerator;
using Patient = HL7Workbench.Core.SyntheticDemographics.Patient;

namespace HL7Workbench.Core;

public static partial class SyntheticScenarioGenerator
{
    private static Patient CreatePatient(SyntheticScenarioOptions options, int number, ref StableRandom clinicalRandom)
    {
        // Keep the old three PRNG draws in both styles so changing demographics
        // cannot change the curated clinical values or the placeholder golden bytes.
        var suffix = clinicalRandom.Below(1000); var age = 20 + clinicalRandom.Below(60); var days = clinicalRandom.Below(365);
        var prefix = options.Overrides.PatientIdentifierPrefix ?? options.Prefix;
        if (options.DemographicStyle == SyntheticDemographicStyle.ObviousPlaceholders)
            return new(string.Create(CultureInfo.InvariantCulture, $"SYN-{prefix}-{options.Seed:X8}-P{number:D4}"),
                string.Create(CultureInfo.InvariantCulture, $"SYNTHETIC{number:D4}^EXAMPLE{suffix:D3}^^^^L"),
                options.BaseTimestamp.AddYears(-age).AddDays(-days).ToString("yyyyMMdd", CultureInfo.InvariantCulture), "U",
                "100 SYNTHETIC TEST LANE^^EXAMPLEVILLE^ZZ^00000^USA", "");

        return SyntheticDemographics.CreatePatient(options.Seed, prefix, number,
            options.BaseTimestamp.AddDays((number - 1) * options.EncountersPerPatient));
    }

    private static (string Initial, string Transfer) CareLocations(SyntheticScenarioOptions options, int encounter)
    {
        if (options.DemographicStyle == SyntheticDemographicStyle.ObviousPlaceholders)
            return ((options.EncounterType == SyntheticEncounterType.Inpatient ? "SYN-WARD^101^1^" : "SYN-CLINIC^^^") + options.SendingFacility,
                "SYN-WARD^202^2^" + options.SendingFacility);
        return SyntheticDemographics.CareLocations(options.Seed, encounter,
            options.EncounterType == SyntheticEncounterType.Inpatient, options.SendingFacility);
    }
}
