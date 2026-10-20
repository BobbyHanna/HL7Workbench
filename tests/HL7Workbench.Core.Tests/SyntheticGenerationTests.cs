using System.Globalization;
using System.Security.Cryptography;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class SyntheticGenerationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static SyntheticMessageBatch Generate(SyntheticGenerationOptions options) => SyntheticMessageGenerator.Generate(options, Token);
    private sealed class InlineProgress(Action<int> report) : IProgress<int> { public void Report(int value) => report(value); }
    [Theory]
    [InlineData(SyntheticMessageType.AdtA01, "ADT^A01^ADT_A01", "I")]
    [InlineData(SyntheticMessageType.AdtA04, "ADT^A04^ADT_A01", "O")]
    [InlineData(SyntheticMessageType.AdtA08, "ADT^A08^ADT_A01", "O")]
    [InlineData(SyntheticMessageType.OrmO01, "ORM^O01^ORM_O01", "O")]
    [InlineData(SyntheticMessageType.OruR01, "ORU^R01^ORU_R01", "O")]
    public void EveryTypeHasCanonicalParseableTestHeaderAndFictionalIdentity(SyntheticMessageType type, string msh9, string patientClass)
    {
        var options = new SyntheticGenerationOptions { MessageType = type, Count = 3 };
        var batch = Generate(options);
        Assert.Equal(3, batch.Messages.Count); Assert.Equal(batch.Messages.Sum(m => m.Length), batch.TotalCharacters);
        for (var i = 0; i < batch.Messages.Count; i++)
        {
            var text = batch.Messages[i]; var message = Hl7Parser.Parse(text);
            Assert.Empty(message.Diagnostics); Assert.Equal(text, Hl7Serializer.SerializeText(message));
            Assert.All(message.Segments, s => Assert.Equal("\r", s.Terminator));
            Assert.All(text, c => Assert.InRange((int)c, 0, 127)); Assert.DoesNotContain('\n', text);
            Assert.Equal("|", message.Get("MSH-1").Raw); Assert.Equal("^~\\&", message.Get("MSH-2").Raw);
            Assert.Equal(msh9, message.Get("MSH-9").Raw); Assert.Equal(options.ControlId(i + 1), message.Get("MSH-10").Raw);
            Assert.Equal("T", message.Get("MSH-11").Raw); Assert.Equal("2.5.1", message.Get("MSH-12").Raw);
            Assert.Equal(options.BaseTimestamp.AddMinutes(i).ToString("yyyyMMddHHmmss'+0000'", CultureInfo.InvariantCulture), message.Get("MSH-7").Raw);
            Assert.Equal("SYN-FACILITY", message.Get("MSH-4").Raw); Assert.Equal("SYN-DEST", message.Get("MSH-6").Raw);
            Assert.StartsWith("SYNTHETIC", message.Get("PID-5.1").Raw); Assert.StartsWith("EXAMPLE", message.Get("PID-5.2").Raw);
            Assert.Equal("SYNTHETIC", message.Get("PV1-7.2").Raw); Assert.Equal(patientClass, message.Get("PV1-2").Raw);
            Assert.Equal("EXAMPLEVILLE", message.Get("PID-11.3").Raw); Assert.Equal("00000", message.Get("PID-11.5").Raw);
            Assert.Equal(options.ControlId(i + 1) + "-MR", message.Get("PID-3.1").Raw);
        }
    }
    [Fact]
    public void AdtEventsHaveAdmissionRegistrationAndUpdatedDemographicDifferences()
    {
        var messages = new[] { SyntheticMessageType.AdtA01, SyntheticMessageType.AdtA04, SyntheticMessageType.AdtA08 }
            .Select(t => Hl7Parser.Parse(Generate(new() { MessageType = t }).Messages[0])).ToArray();
        Assert.Equal(new[] { "A01", "A04", "A08" }, messages.Select(m => m.Get("EVN-1").Raw));
        Assert.All(messages, m => Assert.Equal(m.Get("MSH-7").Raw, m.Get("EVN-2").Raw));
        Assert.Equal("SYN-WARD^101^1^SYN-FAC", messages[0].Get("PV1-3").Raw);
        Assert.Equal("SYN-REG^^^SYN-FAC", messages[1].Get("PV1-3").Raw);
        Assert.Equal("SYN-CLINIC^^^SYN-FAC", messages[2].Get("PV1-3").Raw);
        Assert.NotEqual(messages[1].Get("PID-11").Raw, messages[2].Get("PID-11").Raw);
        Assert.Equal("synthetic.1@example.invalid", messages[2].Get("PID-13.4").Raw);
        Assert.Equal("20291231230000+0000", messages[2].Get("PV1-44").Raw);
    }
    [Theory]
    [InlineData(SyntheticMessageType.OrmO01, "NW", "SC")]
    [InlineData(SyntheticMessageType.OruR01, "RE", "CM")]
    public void OrdersAndObservationsHaveConsistentIdentifiersTypesUnitsAndTime(SyntheticMessageType type, string control, string status)
    {
        var m = Hl7Parser.Parse(Generate(new() { MessageType = type }).Messages[0]);
        Assert.Equal(control, m.Get("ORC-1").Raw); Assert.Equal(status, m.Get("ORC-5").Raw);
        Assert.Equal(m.Get("ORC-2").Raw, m.Get("OBR-2").Raw); Assert.Equal(m.Get("ORC-3").Raw, m.Get("OBR-3").Raw);
        Assert.Equal("SYN-PANEL", m.Get("OBR-4.1").Raw);
        if (type == SyntheticMessageType.OrmO01)
        {
            Assert.DoesNotContain(m.Segments, s => s.Identifier == "OBX");
            Assert.Equal("20300101003000+0000", m.Get("OBR-6").Raw); Assert.Equal("", m.Get("OBR-25").Raw);
            return;
        }
        Assert.Equal("F", m.Get("OBR-25").Raw); Assert.Equal("20291231235500+0000", m.Get("OBR-22").Raw);
        Assert.Equal(3, m.Segments.Count(s => s.Identifier == "OBX"));
        Assert.Equal("NM", m.Get("OBX-2").Raw); Assert.Equal("mg/dL", m.Get("OBX-6.1").Raw);
        Assert.InRange(int.Parse(m.Get("OBX-5").Raw!, CultureInfo.InvariantCulture), 75, 105);
        Assert.Equal("NM", m.Get("OBX[2]-2").Raw); Assert.Equal("mmol/L", m.Get("OBX[2]-6.1").Raw);
        Assert.InRange(int.Parse(m.Get("OBX[2]-5").Raw!, CultureInfo.InvariantCulture), 137, 143);
        Assert.Equal("ST", m.Get("OBX[3]-2").Raw); Assert.Contains("SYNTHETIC", m.Get("OBX[3]-5").Raw);
        for (var i = 1; i <= 3; i++)
        {
            Assert.Equal("F", m.Get($"OBX[{i}]-11").Raw);
            Assert.Equal(m.Get("OBR-7").Raw, m.Get($"OBX[{i}]-14").Raw);
            Assert.Equal(m.Get("OBR-22").Raw, m.Get($"OBX[{i}]-19").Raw);
        }
    }
    [Theory]
    [InlineData(1)] [InlineData(100)] [InlineData(1000)]
    public void DeterministicMaximumBatchHasUniquePredictableIdsAndBoundedContent(int count)
    {
        var config = new SyntheticGenerationOptions { Count = count, MessageType = SyntheticMessageType.OruR01 };
        var batch = Generate(config); var repeated = Generate(config);
        Assert.Equal(batch.GetUtf8Bytes(), repeated.GetUtf8Bytes()); Assert.InRange(batch.TotalCharacters, 1, SyntheticMessageGenerator.MaximumCharacters);
        var ids = batch.Messages.Select(t => Hl7Parser.Parse(t).Get("MSH-10").Raw).ToArray();
        Assert.Equal(count, ids.Distinct().Count()); Assert.Equal(config.ControlId(count), ids[^1]);
        Assert.NotEqual(batch.GetUtf8Bytes(), Generate(config with { Seed = 43 }).GetUtf8Bytes());
    }
    [Fact]
    public void NormalizedConfigurationIsCultureAndClockIndependentWithVersionedGoldenBytes()
    {
        var a = new SyntheticGenerationOptions { Prefix = " syn ", BaseTimestamp = new(2029, 12, 31, 19, 0, 0, TimeSpan.FromHours(-5)) };
        var b = new SyntheticGenerationOptions(); var expected = Generate(b).GetUtf8Bytes();
        var culture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "en-US", "tr-TR", "ar-SA", "fr-FR" })
            { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name); Assert.Equal(expected, Generate(a).GetUtf8Bytes()); }
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Assert.Equal(1, SyntheticMessageGenerator.Version);
        Assert.Equal("C04DC83D53C12CB3B23B028F94A29D55A4D5C7D51F37B2FC3A675C33161862CA", Convert.ToHexString(SHA256.HashData(expected)));
        Assert.Contains("20300101000000+0000", System.Text.Encoding.UTF8.GetString(expected));
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(1001)]
    public void InvalidCountsAreRejectedWithoutTruncation(int count) => Assert.Throws<ArgumentException>(() => Generate(new() { Count = count }));
    [Theory]
    [InlineData("")] [InlineData("A|B")] [InlineData("é")] [InlineData("1234567890123")] [InlineData("A\rMSH")]
    public void PrefixCannotInjectHl7OrUnboundedIdentity(string prefix) => Assert.Throws<ArgumentException>(() => Generate(new() { Prefix = prefix }));
    [Fact]
    public void EnumTimestampAndFractionalSecondNormalizationAreExplicit()
    {
        Assert.Throws<ArgumentException>(() => Generate(new() { MessageType = (SyntheticMessageType)100 }));
        Assert.Throws<ArgumentException>(() => Generate(new() { BaseTimestamp = DateTimeOffset.MinValue }));
        var config = new SyntheticGenerationOptions { BaseTimestamp = SyntheticGenerationOptions.DefaultTimestamp.AddTicks(9999999) };
        Assert.Equal(Generate(new()).GetUtf8Bytes(), Generate(config).GetUtf8Bytes());
        Assert.Throws<ArgumentOutOfRangeException>(() => config.ControlId(0));
    }
    [Theory]
    [InlineData(1, int.MaxValue, int.MaxValue, "GeneratedContentCharacters")]
    [InlineData(int.MaxValue, 1, int.MaxValue, "GeneratedMessageCharacters")]
    [InlineData(int.MaxValue, int.MaxValue, 1, "GeneratedLineCharacters")]
    public void EveryOutputQuotaRejectsWholeBatch(int total, int message, int line, string limit)
    {
        var error = Assert.Throws<Hl7LimitException>(() => SyntheticMessageGenerator.GenerateWithinBudget(new() { Count = 2 }, new(total, message, line), Token));
        Assert.Contains(limit, error.Message);
    }
    [Theory]
    [InlineData(0)] [InlineData(37)] [InlineData(1000)]
    public void CancellationNeverReturnsPartialBatchAndRetryIsDeterministic(int after)
    {
        var config = new SyntheticGenerationOptions { Count = 1000 }; using var cancellation = new CancellationTokenSource();
        if (after == 0) cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => SyntheticMessageGenerator.Generate(config, cancellation.Token, new InlineProgress(i => { if (i == after) cancellation.Cancel(); })));
        Assert.Equal(Generate(config).GetUtf8Bytes(), Generate(config).GetUtf8Bytes());
    }
}
