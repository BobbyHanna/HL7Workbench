using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class SyntheticDemographicTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static SyntheticScenarioBatch Generate(SyntheticScenarioOptions? options = null) => SyntheticScenarioGenerator.Generate(options ?? new(), Token);
    private static Hl7Message[] Parse(SyntheticScenarioBatch batch) => batch.Messages.Select(text => Hl7Parser.Parse(text)).ToArray();
    private static string Text(Hl7Message message, string path) => message.Get(path).Raw ?? "";

    [Theory]
    [InlineData(SyntheticScenarioType.AdmissionDischarge, false)] [InlineData(SyntheticScenarioType.AdmissionDischarge, true)]
    [InlineData(SyntheticScenarioType.OrderResult, false)] [InlineData(SyntheticScenarioType.OrderResult, true)]
    [InlineData(SyntheticScenarioType.CombinedEncounter, false)] [InlineData(SyntheticScenarioType.CombinedEncounter, true)]
    public void NewScenarioDefaultsHavePlausibleStablePatientDemographicsAcrossEveryWorkflow(SyntheticScenarioType scenario, bool outpatient)
    {
        var options = new SyntheticScenarioOptions { Scenario = scenario, EncounterType = outpatient ? SyntheticEncounterType.Outpatient : SyntheticEncounterType.Inpatient,
            Patients = 12, EncountersPerPatient = 2, IncludeTransfer = !outpatient && scenario != SyntheticScenarioType.OrderResult };
        Assert.Equal(SyntheticDemographicStyle.RealisticSynthetic, options.DemographicStyle); Assert.Equal("Realistic Synthetic", options.DemographicStyleLabel);
        var batch = Generate(options); var messages = Parse(batch); Assert.Equal(options.EstimateMessageCount(), messages.Length);
        var patients = messages.GroupBy(m => Text(m, "PID-3.1")).ToArray(); Assert.Equal(12, patients.Length);
        foreach (var patient in patients)
        {
            var first = patient.First();
            foreach (var field in new[] { "PID-3", "PID-5", "PID-7", "PID-8", "PID-11", "PID-13" }) Assert.Single(patient.Select(m => Text(m, field)).Distinct());
            Assert.Matches("^[A-Z][A-Z '-]+$", Text(first, "PID-5.1")); Assert.Matches("^[A-Z]+$", Text(first, "PID-5.2"));
            Assert.DoesNotContain("SYNTHETIC", Text(first, "PID-5")); Assert.DoesNotContain("EXAMPLE", Text(first, "PID-5"));
            Assert.Equal("L", Text(first, "PID-5.7")); Assert.True(Text(first, "PID-5.3").Length <= 7);
            Assert.Contains(Text(first, "PID-8"), new[] { "F", "M", "O", "U" });
            var birth = DateOnly.ParseExact(Text(first, "PID-7"), "yyyyMMdd", CultureInfo.InvariantCulture);
            var start = DateOnly.ParseExact(Text(first, "PV1-44")[..8], "yyyyMMdd", CultureInfo.InvariantCulture);
            var age = start.Year - birth.Year - (start < birth.AddYears(start.Year - birth.Year) ? 1 : 0);
            Assert.InRange(age, 18, 89); Assert.True(birth < start);
            Assert.Matches("^[1-9][0-9]{2,3} [A-Z]+ (STREET|AVENUE|LANE|DRIVE|COURT|ROAD)$", Text(first, "PID-11.1"));
            Assert.Equal("SYNTHETIC TEST ADDRESS", Text(first, "PID-11.2")); Assert.Matches("^[A-Z ]+$", Text(first, "PID-11.3"));
            Assert.Contains(Text(first, "PID-11.4"), new[] { "MI", "OH", "CO", "NC", "VA", "OR", "NH", "MA" });
            Assert.Matches("^[0-9]{5}$", Text(first, "PID-11.5")); Assert.Equal("USA", Text(first, "PID-11.6")); Assert.Equal("H", Text(first, "PID-11.7"));
        }
        Assert.True(patients.Select(p => Text(p.First(), "PID-5")).Distinct().Count() > 8);
        Assert.True(patients.Select(p => Text(p.First(), "PID-7")).Distinct().Count() > 8);
        Assert.All(messages, m => { Assert.Empty(m.Diagnostics); Assert.Empty(Hl7Validator.Validate(m)); Assert.Equal(m.RawText, Hl7Serializer.SerializeText(m)); });
    }

    [Fact]
    public void ProfilesIncludeVariedSexMiddleNamesAndOnlyReservedStructuredTelephones()
    {
        var patients = Parse(Generate(new() { Patients = 100, Scenario = SyntheticScenarioType.AdmissionDischarge })).Where(m => Text(m, "MSH-9.2") == "A01").ToArray();
        Assert.Equal(new[] { "F", "M", "O", "U" }, patients.Select(m => Text(m, "PID-8")).Distinct().Order());
        Assert.Contains(patients, m => Text(m, "PID-5.3").Length == 0); Assert.Contains(patients, m => Text(m, "PID-5.3").Length == 1);
        Assert.Contains(patients, m => Text(m, "PID-5.3").Length > 1); Assert.Contains(patients, m => Text(m, "PID-13").Length == 0);
        var phones = patients.Where(m => Text(m, "PID-13").Length > 0).ToArray(); Assert.True(phones.Length > 50);
        foreach (var m in phones)
        {
            Assert.Equal("", Text(m, "PID-13.1")); Assert.Equal("PRN", Text(m, "PID-13.2")); Assert.Equal("PH", Text(m, "PID-13.3"));
            Assert.Equal("1", Text(m, "PID-13.5")); Assert.Matches("^[2-9][0-9]{2}$", Text(m, "PID-13.6"));
            Assert.Matches("^55501[0-9]{2}$", Text(m, "PID-13.7")); Assert.InRange(int.Parse(Text(m, "PID-13.7")[3..], CultureInfo.InvariantCulture), 100, 199);
            Assert.Equal("", Text(m, "PID-19")); // No SSN is invented.
        }
        Assert.Contains(patients, m => Text(m, "PID-11.5").StartsWith('0'));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CareLocationsProvidersFacilityCodesAndOrderingNamesRemainCoherent(bool outpatient)
    {
        var options = new SyntheticScenarioOptions { Patients = 3, EncountersPerPatient = 2, OrdersPerEncounter = 2, ResultsPerOrder = 2,
            IncludeTransfer = !outpatient, EncounterType = outpatient ? SyntheticEncounterType.Outpatient : SyntheticEncounterType.Inpatient };
        var messages = Parse(Generate(options)); var facility = options.Normalize().SendingFacility;
        Assert.StartsWith("SYN-", facility); Assert.DoesNotContain("SYN-FACILITY", facility);
        foreach (var m in messages)
        {
            Assert.Equal("EHR", Text(m, "MSH-3")); Assert.Equal(facility, Text(m, "MSH-4")); Assert.Equal("LAB-INTERFACE", Text(m, "MSH-5")); Assert.Equal(options.ReceivingFacility, Text(m, "MSH-6"));
            Assert.Equal(facility, Text(m, "PID-3.4")); Assert.Equal("MR", Text(m, "PID-3.5")); Assert.Equal(facility, Text(m, "PV1-19.4")); Assert.Equal("VN", Text(m, "PV1-19.5"));
            Assert.Equal(facility, Text(m, "PV1-3.4")); Assert.Equal(outpatient ? "OUTPATIENT" : "MEDSURG", Text(m, "PV1-3.1"));
            Assert.StartsWith("SYN-PRV-", Text(m, "PV1-7.1")); Assert.Matches("^[A-Z][A-Z '-]+$", Text(m, "PV1-7.2")); Assert.Matches("^[A-Z]+$", Text(m, "PV1-7.3"));
            Assert.Equal("SYN-PROVIDER", Text(m, "PV1-7.9")); Assert.Equal("PRN", Text(m, "PV1-7.13"));
            Assert.DoesNotContain("SYNTHETIC", Text(m, "PV1-7"));
            if (Text(m, "MSH-9.1") != "ADT")
            {
                Assert.StartsWith("SYN-PRV-", Text(m, "ORC-12.1")); Assert.EndsWith("-OR", Text(m, "ORC-12.1"));
                Assert.Equal(Text(m, "ORC-12"), Text(m, "OBR-16")); Assert.NotEqual(Text(m, "PV1-7.1"), Text(m, "ORC-12.1"));
                Assert.Equal(options.SendingFacilityName, m.GetDecoded("ORC-21.1").Text); Assert.Equal(facility, Text(m, "ORC-21.10"));
                Assert.Equal("SYN-FACILITY", Text(m, "ORC-21.6"));
            }
        }
        foreach (var visit in messages.GroupBy(m => Text(m, "PV1-19.1")))
        {
            Assert.Single(visit.Select(m => Text(m, "PV1-7")).Distinct());
            Assert.Single(visit.Where(m => Text(m, "MSH-9.1") != "ADT").Select(m => Text(m, "ORC-12")).Distinct());
            var initial = Text(visit.First(), "PV1-3");
            if (outpatient) Assert.All(visit, m => Assert.Equal(initial, Text(m, "PV1-3")));
            else
            {
                var transfer = visit.Single(m => Text(m, "MSH-9.2") == "A02"); Assert.Equal(initial, Text(transfer, "PV1-6"));
                Assert.Equal("A", Text(visit.First(), "PV1-3.3")); Assert.Equal("B", Text(transfer, "PV1-3.3"));
                Assert.All(visit.Skip(1), m => Assert.Equal(Text(transfer, "PV1-3"), Text(m, "PV1-3")));
            }
        }
    }

    [Fact]
    public void IdentifierDomainsAreDistinctAndDoNotUseMessageControlIdsAsPatientOrVisitKeys()
    {
        var options = new SyntheticScenarioOptions { Patients = 3, EncountersPerPatient = 2, OrdersPerEncounter = 2 };
        var messages = Parse(Generate(options)); var domains = new Dictionary<string, HashSet<string>>();
        foreach (var path in new[] { "PID-3.1", "PV1-19.1", "MSH-10", "ORC-2.1", "ORC-3.1", "PV1-7.1", "ORC-12.1", "MSH-4" })
            domains[path] = messages.Select(m => Text(m, path)).Where(s => s.Length > 0).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(3, domains["PID-3.1"].Count); Assert.Equal(6, domains["PV1-19.1"].Count); Assert.Equal(messages.Length, domains["MSH-10"].Count);
        Assert.Equal(12, domains["ORC-2.1"].Count); Assert.Equal(12, domains["ORC-3.1"].Count);
        foreach (var a in domains.Keys) foreach (var b in domains.Keys.Where(b => string.CompareOrdinal(a, b) < 0)) Assert.False(domains[a].Overlaps(domains[b]));
        foreach (var m in messages)
        {
            Assert.Matches("^SYN-SYN-[0-9A-F]{8}-MR[0-9]{6}$", Text(m, "PID-3.1")); Assert.Matches("^SYN-SYN-[0-9A-F]{8}-VN[0-9]{6}$", Text(m, "PV1-19.1"));
            Assert.Matches("^SYN2-[0-9A-F]{8}-[0-9]{4}$", Text(m, "MSH-10")); Assert.InRange(Text(m, "MSH-10").Length, 1, 20);
            Assert.DoesNotContain(Text(m, "MSH-10"), Text(m, "PID-3.1")); Assert.DoesNotContain(Text(m, "MSH-10"), Text(m, "PV1-19.1"));
        }
        var orderOnly = Parse(Generate(options with { Scenario = SyntheticScenarioType.OrderResult }));
        Assert.Equal(domains["PID-3.1"], orderOnly.Select(m => Text(m, "PID-3.1")).ToHashSet());
        Assert.Equal(domains["PV1-19.1"], orderOnly.Select(m => Text(m, "PV1-19.1")).ToHashSet());
    }

    [Fact]
    public void RealisticOutputHasPinnedCultureIndependentSeededBytesAndVariesWithSeed()
    {
        var options = new SyntheticScenarioOptions { Patients = 2, EncountersPerPatient = 2, IncludeTransfer = true };
        var bytes = Generate(options).GetUtf8Bytes(); Assert.Equal(bytes, Generate(options).GetUtf8Bytes());
        var culture = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA"); Assert.Equal(bytes, Generate(options).GetUtf8Bytes()); }
        finally { CultureInfo.CurrentCulture = culture; }
        Assert.Equal(bytes, Generate(options with { BaseTimestamp = options.BaseTimestamp.ToOffset(TimeSpan.FromHours(-4)).AddTicks(90) }).GetUtf8Bytes());
        Assert.NotEqual(bytes, Generate(options with { Seed = 43 }).GetUtf8Bytes()); Assert.NotEqual(bytes, Generate(options with { BaseTimestamp = options.BaseTimestamp.AddDays(1) }).GetUtf8Bytes());
        var changed = Parse(Generate(options with { Seed = 43 })); var original = Parse(Generate(options));
        Assert.NotEqual(Text(original[0], "PID-5"), Text(changed[0], "PID-5"));
        Assert.Equal("3C761B5A64DBA7689526C74D01ED0D63400529EEC440CA5F22CC27B231A7EF68", Convert.ToHexString(SHA256.HashData(Generate().GetUtf8Bytes())));
    }

    [Theory]
    [InlineData(SyntheticDemographicStyle.RealisticSynthetic)] [InlineData(SyntheticDemographicStyle.ObviousPlaceholders)]
    public void ExplicitOverridesIncludingLegacyDefaultsAlwaysWin(SyntheticDemographicStyle style)
    {
        var options = new SyntheticScenarioOptions { DemographicStyle = style, Patients = 2, OrdersPerEncounter = 2, ResultsPerOrder = 2,
            SendingApplication = "syn-generator", SendingFacility = "syn-facility", ReceivingApplication = "syn-receiver", ReceivingFacility = "syn-dest",
            Overrides = new() { PatientIdentifierPrefix = "demo", OrderCode = "CUSTOM", ResultStatus = "P", ObservationValue = 101.25m } };
        foreach (var m in Parse(Generate(options)))
        {
            Assert.Equal("SYN-GENERATOR", Text(m, "MSH-3")); Assert.Equal("SYN-FACILITY", Text(m, "MSH-4")); Assert.Equal("SYN-RECEIVER", Text(m, "MSH-5")); Assert.Equal("SYN-DEST", Text(m, "MSH-6"));
            Assert.Equal("SYN-FACILITY", Text(m, "PID-3.4")); Assert.Equal("SYN-FACILITY", Text(m, "PV1-19.4")); Assert.Equal("SYN-FACILITY", Text(m, "PV1-3.4"));
            Assert.StartsWith("SYN-DEMO-", Text(m, "PID-3.1"));
            if (Text(m, "MSH-9.1") != "ADT")
            { Assert.Equal("CUSTOM", Text(m, "OBR-4.1")); if (style == SyntheticDemographicStyle.RealisticSynthetic) Assert.Equal("SYN-FACILITY", Text(m, "ORC-21.1")); }
            if (Text(m, "MSH-9.1") == "ORU") { Assert.Equal("101.25", Text(m, "OBX-5")); Assert.Equal("P", Text(m, "OBX-11")); }
        }
        var normalized = options.Normalize(); Assert.Equal(normalized, normalized.Normalize());
        Assert.Throws<ArgumentException>(() => Generate(options with { SendingFacility = "BAD^FAC" }));
        Assert.Throws<ArgumentException>(() => Generate(options with { DemographicStyle = (SyntheticDemographicStyle)99 }));
    }

    [Fact]
    public void FacilityCatalogueNamesEscapeReservedDelimitersWithoutChangingFieldCoordinates()
    {
        var escaped = false; var codes = new HashSet<string>();
        for (uint seed = 0; seed < 32; seed++)
        {
            var options = new SyntheticScenarioOptions { Seed = seed }; codes.Add(options.SendingFacility);
            var order = Parse(Generate(options)).Single(m => Text(m, "MSH-9.1") == "ORM");
            Assert.Equal(options.SendingFacilityName, order.GetDecoded("ORC-21.1").Text); Assert.Equal(options.SendingFacility, Text(order, "ORC-21.10"));
            Assert.Empty(order.Diagnostics);
            if (options.SendingFacilityName.Contains('&')) { escaped = true; Assert.Contains("\\T\\", Text(order, "ORC-21.1")); }
        }
        Assert.True(escaped); Assert.True(codes.Count > 3);
    }

    [Theory]
    [InlineData(SyntheticClinicalExample.Chemistry)] [InlineData(SyntheticClinicalExample.BloodCount)]
    public void SwitchingStyleLeavesBothCuratedPanelsClinicalValuesAndRangesUnchanged(SyntheticClinicalExample panel)
    {
        var options = new SyntheticScenarioOptions { ClinicalExample = panel, Patients = 2, OrdersPerEncounter = 3, ResultsPerOrder = 2 };
        var realistic = Parse(Generate(options)); var placeholders = Parse(Generate(options with { DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders }));
        Assert.Equal(realistic.Length, placeholders.Length);
        for (var n = 0; n < realistic.Length; n++)
            foreach (var path in new[] { "MSH-7", "MSH-9", "OBR-4", "OBR-7", "OBR-15", "OBR-25", "OBX-3", "OBX-5", "OBX-6", "OBX-7", "OBX-8", "OBX-11", "OBX[2]-3", "OBX[2]-5", "OBX[2]-6", "OBX[2]-7", "OBX[2]-8" })
                Assert.Equal(Text(realistic[n], path), Text(placeholders[n], path));
    }

    [Fact]
    public void PlaceholderGoldenAndStandaloneVersionOneGoldenRemainUnchanged()
    {
        var placeholder = Generate(new() { DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders });
        Assert.Equal("C6D6324905DEF434F1CF0DEF7FAE3CE2BACAD90DD41CDD4444F9E2BB4B4220E9", Convert.ToHexString(SHA256.HashData(placeholder.GetUtf8Bytes())));
        Assert.Contains("SYNTHETIC0001^EXAMPLE", placeholder.Messages[0]); Assert.Contains("SYN-WARD", placeholder.Messages[0]);
        Assert.Equal("Obvious Placeholders", placeholder.Options.DemographicStyleLabel);
        var old = SyntheticMessageGenerator.Generate(new() { MessageType = SyntheticMessageType.OruR01, Count = 2 }, Token);
        Assert.Equal(SyntheticMessageGenerator.Version, 1); Assert.Contains("SYNTHETIC", old.Messages[0]);
        // Existing SyntheticGenerationTests retain the original permanent v1 hash
        // and all five standalone types; no scenario mode is added to that API.
        Assert.Equal(old.GetUtf8Bytes(), SyntheticMessageGenerator.Generate(old.Options, Token).GetUtf8Bytes());
    }

    [Theory]
    [InlineData(1900)] [InlineData(2028)] [InlineData(9998)]
    public void AdultDatesAreValidAtSupportedBoundariesAndLeapDay(int year)
    {
        var date = year == 2028 ? new DateTimeOffset(year, 2, 29, 9, 30, 0, TimeSpan.Zero) : new(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var options = new SyntheticScenarioOptions { BaseTimestamp = date, Patients = 20, Scenario = SyntheticScenarioType.AdmissionDischarge };
        var messages = Parse(Generate(options)); Assert.All(messages, m => Assert.True(DateOnly.ParseExact(Text(m, "PID-7"), "yyyyMMdd", CultureInfo.InvariantCulture) < DateOnly.FromDateTime(date.DateTime).AddDays(20)));
    }

    [Theory]
    [InlineData(SyntheticDemographicStyle.RealisticSynthetic)] [InlineData(SyntheticDemographicStyle.ObviousPlaceholders)]
    public void MaximumBatchHasBoundedAllocationsAndUnchangedCountCeilings(SyntheticDemographicStyle style)
    {
        var options = new SyntheticScenarioOptions { DemographicStyle = style, Patients = 250 };
        var before = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew(); var batch = Generate(options);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1000, batch.Messages.Count); Assert.InRange(batch.TotalCharacters, 1, SyntheticMessageGenerator.MaximumCharacters);
        Assert.InRange(allocated, 1, 512L * 1024 * 1024);
        TestContext.Current.TestOutputHelper!.WriteLine($"{style}: 1,000 messages, {batch.TotalCharacters} characters, {allocated} allocated bytes, {watch.Elapsed.TotalMilliseconds:F1} ms.");
        Assert.Throws<ArgumentException>(() => Generate(options with { Patients = 251 }));
    }
}
