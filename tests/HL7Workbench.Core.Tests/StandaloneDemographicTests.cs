using System.Globalization;
using System.Text.Json;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class StandaloneDemographicTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static SyntheticGenerationOptions Options(SyntheticMessageType type, uint seed = 42) =>
        new() { MessageType = type, Seed = seed, DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic, Count = 100 };
    private static Hl7Message[] Parse(SyntheticMessageBatch batch) => batch.Messages.Select(text => Hl7Parser.Parse(text)).ToArray();
    private static string Text(Hl7Message message, string path) => message.Get(path).Raw ?? "";

    [Theory]
    [InlineData(SyntheticMessageType.AdtA01)] [InlineData(SyntheticMessageType.AdtA04)] [InlineData(SyntheticMessageType.AdtA08)]
    [InlineData(SyntheticMessageType.OrmO01)] [InlineData(SyntheticMessageType.OruR01)]
    public void AllFiveTypesHaveVariedWellFormedAdultProfilesAndDistinctConsistentIdentifierDomains(SyntheticMessageType type)
    {
        var options = Options(type); var batch = SyntheticMessageGenerator.Generate(options, Token); var messages = Parse(batch);
        Assert.Equal(2, batch.Options.GeneratorVersion); Assert.Equal("Realistic Synthetic", batch.Options.DemographicStyleLabel);
        Assert.Equal(100, messages.Select(m => Text(m, "MSH-10")).Distinct().Count());
        Assert.Equal(100, messages.Select(m => Text(m, "PID-3.1")).Distinct().Count());
        Assert.Equal(100, messages.Select(m => Text(m, "PV1-19.1")).Distinct().Count());
        Assert.True(messages.Select(m => Text(m, "PID-5")).Distinct().Count() > 80);
        Assert.True(messages.Select(m => Text(m, "PID-7")).Distinct().Count() > 80);
        Assert.Equal(new[] { "F", "M", "O", "U" }, messages.Select(m => Text(m, "PID-8")).Distinct().Order());
        Assert.Contains(messages, m => Text(m, "PID-5.3").Length == 0);
        Assert.Contains(messages, m => Text(m, "PID-5.3").Length == 1);
        Assert.Contains(messages, m => Text(m, "PID-5.3").Length > 1);
        Assert.Contains(messages, m => Text(m, "PID-13").Length == 0);
        foreach (var m in messages)
        {
            Assert.Empty(m.Diagnostics); Assert.Empty(Hl7Validator.Validate(m)); Assert.Equal(m.RawText, Hl7Serializer.SerializeText(m));
            Assert.Equal("T", Text(m, "MSH-11")); Assert.Equal("2.5.1", Text(m, "MSH-12"));
            Assert.InRange(Text(m, "MSH-10").Length, 1, 20); Assert.StartsWith("SYN2-", Text(m, "MSH-10"));
            Assert.Matches("^SYN-SYN-[0-9A-F]{8}-MR[0-9]{6}$", Text(m, "PID-3.1"));
            Assert.Matches("^SYN-SYN-[0-9A-F]{8}-VN[0-9]{6}$", Text(m, "PV1-19.1"));
            Assert.DoesNotContain(Text(m, "MSH-10"), Text(m, "PID-3.1"));
            Assert.Matches("^[A-Z][A-Z '-]+$", Text(m, "PID-5.1")); Assert.Matches("^[A-Z]+$", Text(m, "PID-5.2"));
            Assert.Equal("L", Text(m, "PID-5.7")); Assert.DoesNotContain("SYNTHETIC", Text(m, "PID-5"));
            var birth = DateOnly.ParseExact(Text(m, "PID-7"), "yyyyMMdd", CultureInfo.InvariantCulture);
            var at = DateOnly.ParseExact(Text(m, "MSH-7")[..8], "yyyyMMdd", CultureInfo.InvariantCulture);
            var age = at.Year - birth.Year - (at < birth.AddYears(at.Year - birth.Year) ? 1 : 0); Assert.InRange(age, 18, 89);
            Assert.Matches("^[1-9][0-9]{2,3} [A-Z]+ (STREET|AVENUE|LANE|DRIVE|COURT|ROAD)$", Text(m, "PID-11.1"));
            Assert.Equal("SYNTHETIC TEST ADDRESS", Text(m, "PID-11.2")); Assert.Matches("^[A-Z ]+$", Text(m, "PID-11.3"));
            Assert.Matches("^[A-Z]{2}$", Text(m, "PID-11.4")); Assert.Matches("^[0-9]{5}$", Text(m, "PID-11.5"));
            Assert.Equal("USA", Text(m, "PID-11.6")); Assert.Equal("H", Text(m, "PID-11.7"));
            if (Text(m, "PID-13").Length != 0)
            {
                Assert.Equal("PRN", Text(m, "PID-13.2")); Assert.Equal("PH", Text(m, "PID-13.3"));
                Assert.Equal("1", Text(m, "PID-13.5")); Assert.Matches("^[2-9][0-9]{2}$", Text(m, "PID-13.6"));
                Assert.Matches("^55501[0-9]{2}$", Text(m, "PID-13.7"));
            }
            Assert.Equal("", Text(m, "PID-19")); Assert.StartsWith("SYN-", options.SendingFacility);
            Assert.Equal(options.SendingFacility, Text(m, "MSH-4")); Assert.Equal(options.ReceivingFacility, Text(m, "MSH-6"));
            Assert.Equal(options.SendingFacility, Text(m, "PID-3.4")); Assert.Equal("MR", Text(m, "PID-3.5"));
            Assert.Equal(options.SendingFacility, Text(m, "PV1-19.4")); Assert.Equal("VN", Text(m, "PV1-19.5"));
            Assert.Equal(options.SendingFacility, Text(m, "PV1-3.4"));
            Assert.Equal(type == SyntheticMessageType.AdtA01 ? "MEDSURG" : "OUTPATIENT", Text(m, "PV1-3.1"));
            Assert.StartsWith("SYN-PRV-", Text(m, "PV1-7.1")); Assert.Equal("SYN-PROVIDER", Text(m, "PV1-7.9")); Assert.Equal("PRN", Text(m, "PV1-7.13"));
            if (type is SyntheticMessageType.OrmO01 or SyntheticMessageType.OruR01)
            {
                Assert.Contains("-PO", Text(m, "ORC-2.1")); Assert.Contains("-FO", Text(m, "ORC-3.1"));
                Assert.Equal(Text(m, "ORC-2"), Text(m, "OBR-2")); Assert.Equal(Text(m, "ORC-3"), Text(m, "OBR-3"));
                Assert.Equal(options.SendingFacility, Text(m, "ORC-2.2")); Assert.Equal(options.ReceivingFacility, Text(m, "ORC-3.2"));
                Assert.Equal(Text(m, "ORC-12"), Text(m, "OBR-16")); Assert.NotEqual(Text(m, "ORC-12.1"), Text(m, "PV1-7.1"));
                Assert.Equal("SYN-PROVIDER", Text(m, "ORC-12.9")); Assert.Equal("PRN", Text(m, "ORC-12.13"));
                Assert.Equal(7, new[] { "PID-3.1", "PV1-19.1", "MSH-10", "ORC-2.1", "ORC-3.1", "ORC-12.1", "PV1-7.1" }.Select(p => Text(m, p)).Distinct().Count());
            }
            else Assert.Equal(Text(m, "PV1-7"), Text(m, "EVN-5"));
        }
    }

    [Theory]
    [InlineData(SyntheticMessageType.AdtA01)] [InlineData(SyntheticMessageType.AdtA04)] [InlineData(SyntheticMessageType.AdtA08)]
    [InlineData(SyntheticMessageType.OrmO01)] [InlineData(SyntheticMessageType.OruR01)]
    public void RealisticProjectionRetainsEveryTemplateBoundaryAndAllNonDemographicClinicalFields(SyntheticMessageType type)
    {
        var options = Options(type) with { Count = 3 };
        var realistic = Parse(SyntheticMessageGenerator.Generate(options, Token));
        var legacy = Parse(SyntheticMessageGenerator.Generate(options with { DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders }, Token));
        var changed = new Dictionary<string, int[]> { ["MSH"] = [3, 4, 5, 6, 10], ["EVN"] = [5], ["PID"] = [3, 5, 7, 8, 11, 13], ["PV1"] = [3, 7, 19], ["ORC"] = [2, 3, 12], ["OBR"] = [2, 3, 16] };
        for (var i = 0; i < realistic.Length; i++)
        {
            Assert.Equal(legacy[i].Segments.Select(s => s.Identifier), realistic[i].Segments.Select(s => s.Identifier));
            for (var j = 0; j < legacy[i].Segments.Count; j++)
            {
                var oldSegment = legacy[i].Segments[j]; var newSegment = realistic[i].Segments[j];
                Assert.Equal(oldSegment.Terminator, newSegment.Terminator); Assert.Equal(oldSegment.Fields.Count, newSegment.Fields.Count);
                foreach (var field in oldSegment.Fields)
                    if (!changed.TryGetValue(oldSegment.Identifier, out var fields) || !fields.Contains(field.Number))
                        Assert.Equal(field.Raw, newSegment.GetField(field.Number)!.Raw);
            }
        }
    }

    [Theory]
    [InlineData(SyntheticMessageType.AdtA01, 0u)] [InlineData(SyntheticMessageType.AdtA04, 0u)] [InlineData(SyntheticMessageType.AdtA08, 0u)]
    [InlineData(SyntheticMessageType.OrmO01, 0u)] [InlineData(SyntheticMessageType.OruR01, 0u)]
    [InlineData(SyntheticMessageType.AdtA01, 42u)] [InlineData(SyntheticMessageType.AdtA04, 42u)] [InlineData(SyntheticMessageType.AdtA08, 42u)]
    [InlineData(SyntheticMessageType.OrmO01, 42u)] [InlineData(SyntheticMessageType.OruR01, 42u)]
    [InlineData(SyntheticMessageType.AdtA01, uint.MaxValue)] [InlineData(SyntheticMessageType.AdtA04, uint.MaxValue)] [InlineData(SyntheticMessageType.AdtA08, uint.MaxValue)]
    [InlineData(SyntheticMessageType.OrmO01, uint.MaxValue)] [InlineData(SyntheticMessageType.OruR01, uint.MaxValue)]
    public void BothStylesReplayEverySupportedSeedBoundaryExactlyAndOldConfigurationAbsenceMeansVersionOne(SyntheticMessageType type, uint seed)
    {
        var old = JsonSerializer.Deserialize<SyntheticGenerationOptions>($$"""{"MessageType":{{(int)type}},"Seed":{{seed}},"Count":3}""")!;
        Assert.Equal(SyntheticDemographicStyle.ObviousPlaceholders, old.DemographicStyle); Assert.Equal(1, old.GeneratorVersion);
        var original = SyntheticMessageGenerator.Generate(old, Token).GetUtf8Bytes();
        Assert.Equal(original, SyntheticMessageGenerator.Generate(old with { DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders }, Token).GetUtf8Bytes());
        var realistic = Options(type, seed) with { Count = 3 };
        var bytes = SyntheticMessageGenerator.Generate(realistic, Token).GetUtf8Bytes(); Assert.NotEqual(original, bytes);
        Assert.Equal(bytes, SyntheticMessageGenerator.Generate(JsonSerializer.Deserialize<SyntheticGenerationOptions>(JsonSerializer.Serialize(realistic))!, Token).GetUtf8Bytes());
        Assert.Equal(original, SyntheticMessageGenerator.Generate(JsonSerializer.Deserialize<SyntheticGenerationOptions>(JsonSerializer.Serialize(old))!, Token).GetUtf8Bytes());
    }

    [Fact]
    public void SharedEngineKeepsFirstStandaloneAndScenarioProfileProviderAndFacilityIdentical()
    {
        var standalone = Hl7Parser.Parse(SyntheticMessageGenerator.Generate(Options(SyntheticMessageType.OruR01) with { Count = 1 }, Token).Messages[0]);
        var scenario = Hl7Parser.Parse(SyntheticScenarioGenerator.Generate(new(), Token).Messages[0]);
        foreach (var field in new[] { "PID-3", "PID-5", "PID-7", "PID-8", "PID-11", "PID-13", "PV1-7", "MSH-3", "MSH-4", "MSH-5", "MSH-6" })
            Assert.Equal(Text(scenario, field), Text(standalone, field));
    }

    [Fact]
    public void ExplicitOverridesWinEvenWhenEqualToOldDefaultsAndSurviveSeedChanges()
    {
        var options = Options(SyntheticMessageType.OrmO01) with { Count = 1, SendingApplication = "SYN-GENERATOR", SendingFacility = "SYN-FACILITY",
            ReceivingApplication = "CUSTOM-APP", ReceivingFacility = "CUSTOM-LAB", PatientIdentifierPrefix = "PERSON" };
        foreach (var seed in new uint[] { 0, 42, 123456 })
        {
            var model = Parse(SyntheticMessageGenerator.Generate(options with { Seed = seed }, Token)).Single();
            Assert.Equal("SYN-GENERATOR", Text(model, "MSH-3")); Assert.Equal("SYN-FACILITY", Text(model, "MSH-4"));
            Assert.Equal("CUSTOM-APP", Text(model, "MSH-5")); Assert.Equal("CUSTOM-LAB", Text(model, "MSH-6"));
            Assert.Equal("SYN-FACILITY", Text(model, "PID-3.4")); Assert.Equal("SYN-FACILITY", Text(model, "PV1-3.4"));
            Assert.Equal("SYN-FACILITY", Text(model, "ORC-2.2")); Assert.Equal("CUSTOM-LAB", Text(model, "ORC-3.2"));
            Assert.StartsWith("SYN-PERSON-", Text(model, "PID-3.1"));
        }
    }

    [Theory]
    [InlineData("BAD|VALUE")] [InlineData("BAD^VALUE")] [InlineData("BAD&VALUE")] [InlineData("BAD\rVALUE")] [InlineData("")]
    public void InvalidOverridesFailBeforeAnyProgressOrBatch(string value)
    {
        var options = Options(SyntheticMessageType.AdtA01) with { SendingFacility = value };
        Assert.Throws<ArgumentException>(() => SyntheticMessageGenerator.Generate(options, Token));
        Assert.Throws<ArgumentException>(() => SyntheticMessageGenerator.Generate(Options(SyntheticMessageType.AdtA01) with { PatientIdentifierPrefix = value }, Token));
    }

    [Fact]
    public void PlaceholderOverridesAreRejectedRatherThanSilentlyIgnoredAndInvalidStyleIsRejected()
    {
        Assert.Throws<ArgumentException>(() => SyntheticMessageGenerator.Generate(new() { SendingFacility = "CUSTOM" }, Token));
        Assert.Throws<ArgumentException>(() => SyntheticMessageGenerator.Generate(new() { PatientIdentifierPrefix = "CUSTOM" }, Token));
        Assert.Throws<ArgumentException>(() => SyntheticMessageGenerator.Generate(new() { DemographicStyle = (SyntheticDemographicStyle)999 }, Token));
    }

    [Theory]
    [InlineData(SyntheticMessageType.AdtA01)] [InlineData(SyntheticMessageType.AdtA04)] [InlineData(SyntheticMessageType.AdtA08)]
    [InlineData(SyntheticMessageType.OrmO01)] [InlineData(SyntheticMessageType.OruR01)]
    public void MaximumRealisticBatchHonorsExistingBudgetsAndCancellationNeverReturnsPartialOutput(SyntheticMessageType type)
    {
        var options = Options(type) with { Count = SyntheticMessageGenerator.MaximumCount };
        var batch = SyntheticMessageGenerator.Generate(options, Token); Assert.Equal(1000, batch.Messages.Count);
        Assert.InRange(batch.TotalCharacters, 1, SyntheticMessageGenerator.MaximumCharacters);
        using var cancellation = new CancellationTokenSource(); var progress = new CancelProgress(cancellation);
        Assert.ThrowsAny<OperationCanceledException>(() => SyntheticMessageGenerator.Generate(options, cancellation.Token, progress));
        Assert.Equal(1, progress.Calls);
    }
    private sealed class CancelProgress(CancellationTokenSource cancellation) : IProgress<int>
    {
        public int Calls { get; private set; }
        public void Report(int value) { Calls++; cancellation.Cancel(); }
    }

    [Fact]
    public void RealisticOutputIsIndependentOfCurrentCultureAndRetainsEscapedLiteralComponentRules()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            byte[]? expected = null;
            foreach (var culture in new[] { "en-US", "fr-FR", "ar-SA" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var bytes = SyntheticMessageGenerator.Generate(Options(SyntheticMessageType.OruR01) with { Count = 3 }, Token).GetUtf8Bytes();
                if (expected is null) expected = bytes; else Assert.Equal(expected, bytes);
            }
            // The shared codec boundary also covers literal catalog components
            // containing delimiters, without treating them as HL7 structure.
            var composite = SyntheticDemographics.Components("CEDAR & PINE", "O'NEILL", "A^B|C~D\\E");
            var model = Hl7Parser.Parse("MSH|^~\\&|SYN\rPID|||||" + composite + "\r");
            Assert.Equal("CEDAR & PINE", model.GetDecoded("PID-5.1").Text);
            Assert.Equal("O'NEILL", model.GetDecoded("PID-5.2").Text); Assert.Equal("A^B|C~D\\E", model.GetDecoded("PID-5.3").Text);
            Assert.Empty(model.Diagnostics); Assert.Equal(composite, model.Get("PID-5").Raw);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
