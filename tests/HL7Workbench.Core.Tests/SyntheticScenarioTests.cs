using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class SyntheticScenarioTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static SyntheticScenarioBatch Generate(SyntheticScenarioOptions? options = null) => SyntheticScenarioGenerator.Generate(options ?? new(), Token);
    private static Hl7Message[] Parse(SyntheticScenarioBatch batch) => batch.Messages.Select(m => Hl7Parser.Parse(m)).ToArray();
    private static string? Value(Hl7Message message, string path) => message.Get(path).Raw;
    private static DateTimeOffset Time(Hl7Message message, string path) => DateTimeOffset.ParseExact(Value(message, path)!, "yyyyMMddHHmmsszzz", CultureInfo.InvariantCulture);
    private sealed class InlineProgress(Action<int> report) : IProgress<int> { public void Report(int value) => report(value); }

    [Theory]
    [InlineData(SyntheticScenarioType.AdmissionDischarge, SyntheticEncounterType.Inpatient, false, "ADT^A01,ADT^A03")]
    [InlineData(SyntheticScenarioType.AdmissionDischarge, SyntheticEncounterType.Inpatient, true, "ADT^A01,ADT^A02,ADT^A03")]
    [InlineData(SyntheticScenarioType.AdmissionDischarge, SyntheticEncounterType.Outpatient, false, "ADT^A04,ADT^A03")]
    [InlineData(SyntheticScenarioType.OrderResult, SyntheticEncounterType.Outpatient, false, "ORM^O01,ORU^R01")]
    [InlineData(SyntheticScenarioType.CombinedEncounter, SyntheticEncounterType.Inpatient, true, "ADT^A01,ADT^A02,ORM^O01,ORU^R01,ADT^A03")]
    [InlineData(SyntheticScenarioType.CombinedEncounter, SyntheticEncounterType.Outpatient, false, "ADT^A04,ORM^O01,ORU^R01,ADT^A03")]
    public void WorkflowsUseExactStructuresHeadersSegmentOrderAndLosslessSerialization(SyntheticScenarioType scenario, SyntheticEncounterType encounter, bool transfer, string expected)
    {
        var options = new SyntheticScenarioOptions { Scenario = scenario, EncounterType = encounter, IncludeTransfer = transfer, DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders };
        var batch = Generate(options); var messages = Parse(batch);
        Assert.Equal(expected.Split(','), messages.Select(m => Value(m, "MSH-9.1") + "^" + Value(m, "MSH-9.2")));
        Assert.Equal(options.EstimateMessageCount(), messages.Length); Assert.Equal(batch.TotalCharacters, batch.Messages.Sum(m => m.Length));
        Assert.Equal(Encoding.UTF8.GetBytes(string.Concat(batch.Messages)), batch.GetUtf8Bytes());
        for (var i = 0; i < messages.Length; i++)
        {
            var m = messages[i]; var type = Value(m, "MSH-9.1"); var trigger = Value(m, "MSH-9.2");
            Assert.Empty(m.Diagnostics); Assert.Empty(Hl7Validator.Validate(m)); Assert.True(m.CanEdit);
            Assert.Equal(batch.Messages[i], Hl7Serializer.SerializeText(m)); Assert.All(m.Segments, s => Assert.Equal("\r", s.Terminator));
            Assert.DoesNotContain('\n', batch.Messages[i]); Assert.All(batch.Messages[i], c => Assert.InRange((int)c, 0, 127));
            Assert.Equal("|", Value(m, "MSH-1")); Assert.Equal("^~\\&", Value(m, "MSH-2"));
            Assert.Equal("T", Value(m, "MSH-11")); Assert.Equal("2.5.1", Value(m, "MSH-12"));
            Assert.InRange(Value(m, "MSH-10")!.Length, 1, 20);
            Assert.Equal("SYN-GENERATOR", Value(m, "MSH-3")); Assert.Equal("SYN-FACILITY", Value(m, "MSH-4"));
            Assert.Equal("SYN-RECEIVER", Value(m, "MSH-5")); Assert.Equal("SYN-DEST", Value(m, "MSH-6"));
            Assert.Equal(trigger is "A02" ? "ADT_A02" : trigger is "A03" ? "ADT_A03" : type == "ADT" ? "ADT_A01" : type + "_" + trigger, Value(m, "MSH-9.3"));
            Assert.Equal(type == "ADT" ? new[] { "MSH", "EVN", "PID", "PV1" } : type == "ORM"
                ? new[] { "MSH", "PID", "PV1", "ORC", "OBR", "NTE" }
                : new[] { "MSH", "PID", "PV1", "ORC", "OBR", "OBX", "OBX", "NTE" }, m.Segments.Select(s => s.Identifier));
            Assert.Equal(encounter == SyntheticEncounterType.Inpatient ? "I" : "O", Value(m, "PV1-2"));
            if (trigger == "A02") { Assert.Equal("101", Value(m, "PV1-6.2")); Assert.Equal("202", Value(m, "PV1-3.2")); }
            if (trigger == "A03") { Assert.Equal(Value(m, "MSH-7"), Value(m, "PV1-45")); Assert.Equal("01", Value(m, "PV1-36")); }
            else Assert.True(string.IsNullOrEmpty(Value(m, "PV1-45")));
        }
        if (transfer) Assert.All(messages.Skip(1), m => Assert.Equal("202", Value(m, "PV1-3.2")));
    }

    [Theory]
    [InlineData(SyntheticClinicalExample.Chemistry)] [InlineData(SyntheticClinicalExample.BloodCount)]
    public void MultiplePatientsEncountersAndOrdersHaveIndependentSharedIdentityAndOrderedClinicalTimes(SyntheticClinicalExample clinical)
    {
        var options = new SyntheticScenarioOptions { Patients = 3, EncountersPerPatient = 2, OrdersPerEncounter = 3, ResultsPerOrder = 3, IncludeTransfer = true, ClinicalExample = clinical, DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders };
        var batch = Generate(options); var messages = Parse(batch);
        Assert.Equal(messages.Length, messages.Select(m => Value(m, "MSH-10")).Distinct().Count());
        Assert.Equal(3, batch.Summary.Patients); Assert.Equal(6, batch.Summary.Encounters); Assert.Equal(18, batch.Summary.Orders);
        Assert.Equal(54, batch.Summary.Results); Assert.Equal(108, batch.Summary.Observations);
        Assert.Equal(batch.Messages.Count, batch.Summary.MessageTypes.Values.Sum());
        Assert.Equal(messages.Select(m => Time(m, "MSH-7")).Order(), messages.Select(m => Time(m, "MSH-7")));
        var patients = messages.GroupBy(m => Value(m, "PID-3")).ToArray(); Assert.Equal(3, patients.Length);
        foreach (var patient in patients)
        {
            foreach (var path in new[] { "PID-5", "PID-7", "PID-11" }) Assert.Single(patient.Select(m => Value(m, path)).Distinct());
            Assert.StartsWith("SYN-", patient.First().Get("PID-3.1").Raw); Assert.StartsWith("SYNTHETIC", Value(patient.First(), "PID-5.1"));
            Assert.True(string.CompareOrdinal(Value(patient.First(), "PID-7"), Value(patient.First(), "PV1-44")![..8]) < 0);
            var visits = patient.GroupBy(m => Value(m, "PV1-19")).ToArray(); Assert.Equal(2, visits.Length);
            foreach (var visit in visits)
            {
                Assert.Single(visit.Select(m => Value(m, "PV1-7")).Distinct()); Assert.Single(visit.Select(m => Value(m, "PV1-44")).Distinct());
                Assert.True(Time(visit.First(), "MSH-7") < Time(visit.Last(), "MSH-7"));
                Assert.Equal("A01", Value(visit.First(), "MSH-9.2")); Assert.Equal("A03", Value(visit.Last(), "MSH-9.2"));
                var orders = visit.Where(m => Value(m, "MSH-9.1") != "ADT").GroupBy(m => Value(m, "ORC-2")).ToArray(); Assert.Equal(3, orders.Length);
                Assert.Equal(3, orders.Select(g => Value(g.First(), "ORC-3")).Distinct().Count());
                foreach (var order in orders)
                {
                    var placed = order.First(); Assert.Equal("ORM", Value(placed, "MSH-9.1")); Assert.Equal("NW", Value(placed, "ORC-1"));
                    Assert.Equal(4, order.Count()); Assert.Equal(new[] { "P", "P", "F" }, order.Skip(1).Select(m => Value(m, "OBR-25")));
                    foreach (var m in order)
                    {
                        Assert.Equal(Value(placed, "ORC-2"), Value(m, "OBR-2")); Assert.Equal(Value(placed, "ORC-3"), Value(m, "OBR-3"));
                        Assert.Equal(Value(placed, "OBR-4"), Value(m, "OBR-4")); Assert.Equal(Value(placed, "ORC-12"), Value(m, "OBR-16"));
                        Assert.True(Time(placed, "MSH-7") <= Time(m, "MSH-7"));
                    }
                    foreach (var result in order.Skip(1))
                    {
                        Assert.True(Time(placed, "MSH-7") < Time(result, "OBR-7")); Assert.True(Time(result, "OBR-7") < Time(result, "OBR-22"));
                        Assert.Equal(Value(result, "MSH-7"), Value(result, "OBR-22")); Assert.Equal(Value(result, "OBR-25"), Value(result, "OBX-11"));
                        Assert.Equal(Value(result, "OBR-7"), Value(result, "OBX-14")); Assert.Equal(Value(result, "OBR-22"), Value(result, "OBX-19"));
                        Assert.Equal(Value(order.Skip(1).First(), "OBX-5"), Value(result, "OBX-5"));
                        Assert.Equal(clinical == SyntheticClinicalExample.Chemistry ? "SYN-GLU" : "SYN-HGB", Value(result, "OBX-3.1"));
                        Assert.Equal(clinical == SyntheticClinicalExample.Chemistry ? "SYN-NA" : "SYN-WBC", Value(result, "OBX[2]-3.1"));
                        Assert.Equal("99SYN", Value(result, "OBR-4.3")); Assert.Equal("UCUM", Value(result, "OBX-6.3"));
                        Assert.Equal("N", Value(result, "OBX-8")); Assert.Contains("NOT FOR CLINICAL USE", Value(result, "NTE-3"));
                        Assert.Equal(clinical == SyntheticClinicalExample.Chemistry ? "SYN-SER" : "SYN-BLD", Value(result, "OBR-15.1.1"));
                    }
                }
            }
        }
        Assert.Equal(6, messages.Select(m => Value(m, "PV1-19")).Distinct().Count());
        Assert.Equal(18, messages.Where(m => Value(m, "MSH-9.1") == "ORM").Select(m => Value(m, "ORC-2")).Distinct().Count());
    }

    [Fact]
    public void SeedDateCultureAndVersionAreExplicitAndNormalizedOutputIsStable()
    {
        var options = new SyntheticScenarioOptions { Patients = 2, EncountersPerPatient = 2, OrdersPerEncounter = 2, ResultsPerOrder = 2, IncludeTransfer = true };
        var bytes = Generate(options).GetUtf8Bytes();
        Assert.Equal(bytes, Generate(options).GetUtf8Bytes()); Assert.NotEqual(bytes, Generate(options with { Seed = 43 }).GetUtf8Bytes());
        var culture = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA"); Assert.Equal(bytes, Generate(options).GetUtf8Bytes()); }
        finally { CultureInfo.CurrentCulture = culture; }
        Assert.Equal(bytes, Generate(options with { BaseTimestamp = options.BaseTimestamp.ToOffset(TimeSpan.FromHours(5)).AddTicks(1234), Prefix = " syn " }).GetUtf8Bytes());
        Assert.Equal(2, SyntheticScenarioGenerator.Version);
        Assert.Equal(SyntheticGenerationOptions.DefaultTimestamp, Time(Parse(Generate())[0], "MSH-7"));
        Assert.NotEqual(bytes, Generate(options with { BaseTimestamp = options.BaseTimestamp.AddDays(1) }).GetUtf8Bytes());
        // Golden bytes pin field order, PRNG consumption, timestamps and encoding.
        Assert.Equal("C6D6324905DEF434F1CF0DEF7FAE3CE2BACAD90DD41CDD4444F9E2BB4B4220E9", Convert.ToHexString(SHA256.HashData(Generate(new() { DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders }).GetUtf8Bytes())));
    }

    [Fact]
    public void StructuredOverridesPropagateToEveryRelatedContextWithoutReplacingIdentity()
    {
        var overrides = SyntheticScenarioOverrides.FromFields(new Dictionary<string, string>
        { ["PatientIdentifierPrefix"] = "test", ["OBR-4.1"] = "LOCAL-CHEM", ["OBR-25"] = "P", ["OBX-11"] = "P", ["OBX-5"] = "150.25" });
        var options = new SyntheticScenarioOptions { ResultsPerOrder = 3, OrdersPerEncounter = 2, Overrides = overrides,
            SendingApplication = "test-app", SendingFacility = "test-fac", ReceivingApplication = "test-rec", ReceivingFacility = "test-dest" };
        var messages = Parse(Generate(options));
        foreach (var m in messages)
        {
            Assert.StartsWith("SYN-TEST-", Value(m, "PID-3.1")); Assert.Equal("TEST-FAC", Value(m, "PID-3.4")); Assert.Equal("TEST-FAC", Value(m, "PV1-19.4"));
            Assert.Equal("TEST-APP", Value(m, "MSH-3")); Assert.Equal("TEST-FAC", Value(m, "MSH-4")); Assert.Equal("TEST-REC", Value(m, "MSH-5")); Assert.Equal("TEST-DEST", Value(m, "MSH-6"));
            if (Value(m, "MSH-9.1") != "ADT") Assert.Equal("LOCAL-CHEM", Value(m, "OBR-4.1"));
            if (Value(m, "MSH-9.1") == "ORU")
            { Assert.Equal("150.25", Value(m, "OBX-5")); Assert.Equal("H", Value(m, "OBX-8")); Assert.Equal("P", Value(m, "OBX-11")); Assert.Equal("A", Value(m, "ORC-5")); }
        }
        Assert.Single(messages.Select(m => Value(m, "PID-3")).Distinct());
        Assert.Throws<ArgumentException>(() => SyntheticScenarioOverrides.FromFields(new Dictionary<string, string> { ["OBR-25"] = "P", ["OBX-11"] = "F" }));
    }

    [Theory]
    [InlineData("PID-3.1", "EXACT-ID")] [InlineData("PV1-19", "VISIT")] [InlineData("MSH-10", "DUPLICATE")]
    [InlineData("OBR-4.1", "A|B")] [InlineData("OBX-11", "C")] [InlineData("OBX-5", "1,000")]
    [InlineData("OBX-5", "NaN")] [InlineData("OBX-5", "100001")] [InlineData("PatientIdentifierPrefix", "é")]
    [InlineData("PatientIdentifierPrefix", "ſ")] [InlineData("OBR-4.1", "TEST\r")]
    public void UnsupportedOrUnsafeOverridesFailClearly(string path, string value) =>
        Assert.Throws<ArgumentException>(() => SyntheticScenarioOverrides.FromFields(new Dictionary<string, string> { [path] = value }));

    [Theory]
    [InlineData(0, 1, 1, 1)] [InlineData(1001, 1, 1, 1)] [InlineData(1, 0, 1, 1)] [InlineData(1, 101, 1, 1)]
    [InlineData(1, 1, 0, 1)] [InlineData(1, 1, 21, 1)] [InlineData(1, 1, 1, 0)] [InlineData(1, 1, 1, 11)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue)] [InlineData(1000, 100, 20, 10)] [InlineData(251, 1, 1, 1)]
    public void FactorAndMultipliedLimitsRejectBeforeAllocation(int patients, int encounters, int orders, int results) =>
        Assert.Throws<ArgumentException>(() => Generate(new() { Patients = patients, EncountersPerPatient = encounters, OrdersPerEncounter = orders, ResultsPerOrder = results }));

    [Fact]
    public void UnsupportedOptionsAndConflictsAreNeverSilentlyIgnored()
    {
        var options = new SyntheticScenarioOptions();
        foreach (var invalid in new[]
        {
            options with { Scenario = (SyntheticScenarioType)99 }, options with { EncounterType = (SyntheticEncounterType)99 }, options with { ClinicalExample = (SyntheticClinicalExample)99 },
            options with { Hl7Version = "2.6" }, options with { IncludeTransfer = true, EncounterType = SyntheticEncounterType.Outpatient },
            options with { IncludeTransfer = true, Scenario = SyntheticScenarioType.OrderResult }, options with { SendingApplication = "APP\rMSH" },
            options with { SendingFacility = new string('A', 33) }, options with { ReceivingApplication = "A^B" }, options with { ReceivingFacility = "" },
            options with { Prefix = "" }, options with { Overrides = null! }, options with { BaseTimestamp = DateTimeOffset.MinValue },
            options with { BaseTimestamp = new(9998, 12, 31, 0, 0, 0, TimeSpan.Zero), Patients = 500, Scenario = SyntheticScenarioType.AdmissionDischarge },
            options with { Scenario = SyntheticScenarioType.AdmissionDischarge, OrdersPerEncounter = 2 },
            options with { Scenario = SyntheticScenarioType.AdmissionDischarge, Overrides = new() { OrderCode = "TEST" } }
        }) Assert.Throws<ArgumentException>(() => invalid.Normalize());
    }

    [Theory]
    [InlineData(1, int.MaxValue, int.MaxValue, "GeneratedContentCharacters")]
    [InlineData(int.MaxValue, 1, int.MaxValue, "GeneratedMessageCharacters")]
    [InlineData(int.MaxValue, int.MaxValue, 1, "GeneratedLineCharacters")]
    public void EveryOutputBudgetRejectsWholeBatch(int total, int message, int line, string limit)
    {
        var error = Assert.Throws<Hl7LimitException>(() => SyntheticScenarioGenerator.GenerateWithinBudget(new(), new(total, message, line), Token));
        Assert.Equal(limit, error.Limit);
    }

    [Theory]
    [InlineData(0)] [InlineData(37)] [InlineData(1000)]
    public void CancellationNeverReturnsPartialOutputAndRetryHasNoRetainedState(int after)
    {
        using var cancellation = new CancellationTokenSource(); if (after == 0) cancellation.Cancel();
        var options = new SyntheticScenarioOptions { Patients = 250 };
        Assert.ThrowsAny<OperationCanceledException>(() => SyntheticScenarioGenerator.Generate(options, cancellation.Token, new InlineProgress(i => { if (i == after) cancellation.Cancel(); })));
        Assert.Equal(Generate().GetUtf8Bytes(), Generate().GetUtf8Bytes());
    }

    [Fact]
    public void LargestBatchRemainsWithinOutputAndAllocationBudgetsAndHasImmutableCounts()
    {
        var before = GC.GetAllocatedBytesForCurrentThread(); var batch = Generate(new() { Patients = 250 });
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1000, batch.Messages.Count); Assert.InRange(batch.TotalCharacters, 1, SyntheticMessageGenerator.MaximumCharacters);
        Assert.InRange(allocated, 1, 512L * 1024 * 1024);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)batch.Messages)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)batch.Summary.MessageTypes)["ADT^A01"] = 0);
        TestContext.Current.TestOutputHelper!.WriteLine($"1,000 scenario messages: {batch.TotalCharacters} characters, {allocated} allocated bytes; one parsed model at a time.");
    }

    [Theory]
    [InlineData(SyntheticScenarioType.AdmissionDischarge, 500)] [InlineData(SyntheticScenarioType.OrderResult, 500)] [InlineData(SyntheticScenarioType.CombinedEncounter, 250)]
    public void ExactMessageCeilingIsAcceptedAndNextPatientRejected(SyntheticScenarioType scenario, int patients)
    {
        var options = new SyntheticScenarioOptions { Scenario = scenario, Patients = patients, Prefix = "TWELVECHARS1" };
        Assert.Equal(1000, options.EstimateMessageCount());
        var messages = Generate(options).Messages;
        Assert.Equal(1000, messages.Count); Assert.All(messages, m => Assert.InRange(Value(Hl7Parser.Parse(m), "MSH-10")!.Length, 1, 20));
        Assert.Throws<ArgumentException>(() => (options with { Patients = patients + 1 }).EstimateMessageCount());
    }

    [Fact]
    public void LargestSingleEncounterCompletesBeforeDischargeAndSupportsDateBoundaries()
    {
        foreach (var year in new[] { 1900, 9998 })
        {
            var batch = Generate(new() { OrdersPerEncounter = 20, ResultsPerOrder = 10, IncludeTransfer = true, BaseTimestamp = new(year, 1, 1, 0, 0, 0, TimeSpan.Zero) });
            var messages = Parse(batch); Assert.Equal(223, messages.Length);
            Assert.Equal("A03", Value(messages[^1], "MSH-9.2")); Assert.Equal("F", Value(messages[^2], "OBR-25"));
            Assert.True(Time(messages[^2], "MSH-7") < Time(messages[^1], "PV1-45"));
        }
    }
}
