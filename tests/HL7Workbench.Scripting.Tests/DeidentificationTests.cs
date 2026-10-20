using System.Globalization;
using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Scripting.Tests;

public sealed class DeidentificationTests
{
    private const string Header = "MSH|^~\\&|SYN-PRIVATE-APP|SYN-PRIVATE-FAC|DEST|TEST|20300102120000+0000||ADT^A01|SYN-CONTROL|T|2.5.1\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Patient(string id = "SYN-PRIVATE-ID", string authority = "SYN-AUTH", string birth = "19800229", string name = "SYN-PRIVATE-FAMILY^SYN-PRIVATE-GIVEN") =>
        Header + $"PID|1||{id}^^^{authority}^MR||{name}||{birth}\r";
    private static ScriptMessage Facade(string text) => new(Hl7Parser.Parse(text), Token);
    private static Hl7Message Apply(DeidentificationWorkflow flow, string text)
    { var message = Facade(text); flow.Apply(message); return Hl7Parser.Parse(message.Serialize()); }
    [Fact]
    public void QualifiedPatientsRemainConsistentAndDistinctAcrossAuthoritiesWithoutUsingNames()
    {
        var flow = new DeidentificationWorkflow(new(), Token);
        var a = Apply(flow, Patient()); var again = Apply(flow, Patient(name: "SYN-CHANGED^NAME"));
        var b = Apply(flow, Patient(authority: "SYN-OTHER-AUTH"));
        Assert.Equal(a.Get("PID-3.1").Raw, again.Get("PID-3.1").Raw); Assert.Equal(a.Get("PID-5").Raw, again.Get("PID-5").Raw);
        Assert.NotEqual(a.Get("PID-3.1").Raw, b.Get("PID-3.1").Raw); Assert.NotEqual(a.Get("PID-5").Raw, b.Get("PID-5").Raw);
        Assert.Equal("MR", a.Get("PID-3.5").Raw); Assert.DoesNotContain("SYN-PRIVATE", a.RawText);
        Assert.Equal("Review required", flow.Summary.Status); Assert.Equal(3, flow.Summary.Messages); Assert.Equal(0, flow.Summary.AmbiguousIdentities);
    }
    [Fact]
    public void RepeatingAliasesCorrelateOnlyDeclaredPatientLinksAndContradictionsAreNotMerged()
    {
        var flow = new DeidentificationWorkflow(new(), Token);
        var aliases = "A^^^SYN-A^MR~B^^^SYN-B^PI";
        var both = Apply(flow, Patient().Replace("SYN-PRIVATE-ID^^^SYN-AUTH^MR", aliases, StringComparison.Ordinal));
        var onlyB = Apply(flow, Patient("B", "SYN-B").Replace("SYN-B^MR", "SYN-B^PI", StringComparison.Ordinal));
        Assert.Equal(both.Get("PID-5").Raw, onlyB.Get("PID-5").Raw); Assert.Equal(both.Get("PID-3[2].1").Raw, onlyB.Get("PID-3.1").Raw);
        var other = Apply(flow, Patient("C", "SYN-C"));
        var bridge = Apply(flow, Patient().Replace("SYN-PRIVATE-ID^^^SYN-AUTH^MR", "A^^^SYN-A^MR~C^^^SYN-C^MR", StringComparison.Ordinal));
        Assert.Equal(1, flow.Summary.AmbiguousIdentities); Assert.NotEqual(other.Get("PID-3.1").Raw, bridge.Get("PID-3[2].1").Raw);
        var changedBirth = Apply(flow, Patient("A", "SYN-A", "19900101"));
        Assert.Equal(2, flow.Summary.AmbiguousIdentities); Assert.NotEqual(both.Get("PID-3.1").Raw, changedBirth.Get("PID-3.1").Raw);
    }
    [Theory]
    [InlineData("")] [InlineData("\"\"")] [InlineData("&")]
    public void MissingOrEmptyAuthoritiesNeverGuessPatientCorrelation(string authority)
    {
        var flow = new DeidentificationWorkflow(new(), Token);
        var a = Apply(flow, Patient(authority: authority)); var b = Apply(flow, Patient(authority: authority));
        Assert.NotEqual(a.Get("PID-3.1").Raw, b.Get("PID-3.1").Raw); Assert.Equal(2, flow.Summary.MissingIdentities);
    }
    [Fact]
    public void MissingEmptyAndExplicitNullLeavesAndAllSeparatorsAreRetained()
    {
        var text = Header + "PID|1||SYN-ID^^^SYN-A^MR~\"\"||SYN-FAMILY^^\"\"&\r\nNK1|1||\"\"||\r";
        var before = Hl7Parser.Parse(text); var after = Apply(new(new(), Token), text);
        Assert.Equal(Hl7ValueState.Empty, after.Get("PID-5.2").State); Assert.Equal(Hl7ValueState.ExplicitNull, after.Get("PID-5.3.1").State);
        Assert.Equal(Hl7ValueState.Empty, after.Get("PID-5.3.2").State); Assert.Equal(Hl7ValueState.ExplicitNull, after.Get("PID-3[2]").State);
        Assert.Equal(Hl7ValueState.Missing, after.Get("PID-20").State); Assert.Equal(Hl7ValueState.ExplicitNull, after.Get("NK1-3").State);
        Assert.Equal(before.Delimiters, after.Delimiters); Assert.Equal(before.Segments.Select(s => s.Terminator), after.Segments.Select(s => s.Terminator));
        Assert.Equal(before.Segments.Select(s => s.Fields.Count), after.Segments.Select(s => s.Fields.Count));
        Assert.Empty(after.Diagnostics);
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(28591)]
    public void ControlledBatchMutationPreservesEncodingBomUntouchedBytesAndEscapedCustomDelimiters(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage); var text = "MSH*$%!@*SYN*TEST*DEST*TEST*20300102120000+0000**ADT$A01*SYN-ID*T*2.5.1\r\nPID*1**SYN!S!ID$$$SYN-A$MR**SYN-É$GIVEN\nOBX*1*NM*CODE**98.6*mg/dL$$UCUM*****F\r";
        var original = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        var facade = new ScriptMessage(Hl7Parser.Parse(original, new() { Encoding = encoding }), Token);
        new DeidentificationWorkflow(new(), Token).Apply(facade); var bytes = facade.Serialize(); var after = Hl7Parser.Parse(bytes, new() { Encoding = encoding });
        Assert.Equal(encoding.GetPreamble(), after.ByteOrderMark); Assert.Equal(codePage, after.Encoding.CodePage);
        Assert.Equal("98.6", after.Get("OBX-5").Raw); Assert.Equal("$%!@", after.Get("MSH-2").Raw); Assert.Matches("^10[0-9]{7}$", after.GetDecoded("PID-3.1").Text);
        Assert.Equal(new[] { "\r\n", "\n", "\r" }, after.Segments.Select(s => s.Terminator));
        Assert.Equal(original, Hl7Serializer.Serialize(Hl7Parser.Parse(original, new() { Encoding = encoding })));
    }
    [Theory]
    [InlineData("PD1")] [InlineData("NK1")] [InlineData("PV2")] [InlineData("IN1")] [InlineData("IN2")]
    [InlineData("GT1")] [InlineData("NTE")] [InlineData("DG1")] [InlineData("AL1")] [InlineData("ZPI")]
    public void UnsupportedSensitiveContentIsSuppressedWithoutFlatteningItsStructure(string segment)
    {
        var flow = new DeidentificationWorkflow(new(), Token);
        var after = Apply(flow, Patient() + segment + "|1|SYN-PRIVATE-A^SYN-PRIVATE-B&\"\"~SYN-PRIVATE-C|||||SYN-PRIVATE-FREE\r");
        Assert.DoesNotContain("SYN-PRIVATE", after.RawText); Assert.True(flow.Summary.SuppressedValues > 0);
        if (segment == "ZPI") Assert.Equal(1, flow.Summary.UnknownSegments);
    }
    [Theory]
    [InlineData("TX")] [InlineData("FT")] [InlineData("ST")] [InlineData("ED")] [InlineData("RP")] [InlineData("CWE")]
    public void NarrativeEncapsulatedAndUnsupportedObservationValuesAreHighRisk(string type)
    {
        var flow = new DeidentificationWorkflow(new(), Token);
        var after = Apply(flow, Patient() + $"OBX|1|{type}|SYN-CODE||SYN-PRIVATE-PAYLOAD^BASE64^SYN-PRIVATE-DATA~SYN-PRIVATE-MORE\rNTE|1||SYN-PRIVATE-NARRATIVE\r");
        Assert.DoesNotContain("SYN-PRIVATE", after.RawText); Assert.True(flow.Summary.HighRiskFields > 0);
        Assert.Equal(type, after.Get("OBX-2").Raw); Assert.Equal(2, after.Segments.Single(s => s.Identifier == "OBX").GetField(5)!.Repetitions.Count);
    }
    [Theory]
    [InlineData("20300228", "20300301")]
    [InlineData("2030022812", "2030030112")]
    [InlineData("203002281230", "203003011230")]
    [InlineData("20300228123045.1234-0500", "20300301123045.1234-0500")]
    [InlineData("20240229120000+1400", "20240301120000+1400")]
    public void TimestampShiftsKeepPrecisionFractionOffsetAndIntervals(string input, string expected)
    {
        var flow = new DeidentificationWorkflow(new() { Preset = DeidentificationPreset.TimestampShifter, ShiftDays = 1 }, Token);
        var after = Apply(flow, Header.Replace("20300102120000+0000", input, StringComparison.Ordinal) + "PID|1||SYN-ID\r");
        Assert.Equal(expected, after.Get("MSH-7").Raw); Assert.Equal("SYN-ID", after.Get("PID-3").Raw);
        Assert.Equal(1, flow.Summary.ShiftedTimestamps); Assert.Equal(0, flow.Summary.UnsupportedTimestamps);
    }
    [Theory]
    [InlineData("2030")] [InlineData("203001")] [InlineData("20300230")] [InlineData("20300101000000+1460")] [InlineData("2030010112.1")]
    [InlineData("20300101000060")] [InlineData("20300101000000-1230")]
    public void UnsupportedTimesAreSuppressedOrExplicitlyRetainedForReviewAccordingToPreset(string timestamp)
    {
        var input = Header.Replace("20300102120000+0000", timestamp, StringComparison.Ordinal);
        var broad = new DeidentificationWorkflow(new(), Token); var narrow = new DeidentificationWorkflow(new() { Preset = DeidentificationPreset.TimestampShifter }, Token);
        Assert.Equal(Hl7ValueState.Empty, Apply(broad, input).Get("MSH-7").State);
        Assert.Equal(timestamp, Apply(narrow, input).Get("MSH-7").Raw);
        Assert.Equal(1, broad.Summary.UnsupportedTimestamps); Assert.Equal(1, narrow.Summary.UnsupportedTimestamps);
    }
    [Fact]
    public void SameConfigurationAndInputAreDeterministicAcrossCultureAndDifferentSeedsDiffer()
    {
        var expected = Apply(new(new(), Token), Patient()).RawText; var old = CultureInfo.CurrentCulture;
        try { foreach (var culture in new[] { "en-US", "ar-SA", "tr-TR" }) { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); Assert.Equal(expected, Apply(new(new(), Token), Patient()).RawText); } }
        finally { CultureInfo.CurrentCulture = old; }
        Assert.NotEqual(expected, Apply(new(new() { Seed = 43 }, Token), Patient()).RawText);
        Assert.Equal(expected, Apply(new(new(), Token), Patient()).RawText);
    }
    [Theory]
    [InlineData("99991231", 1)] [InlineData("00010101", -1)]
    public void CalendarOverflowNeverGuessesOrPublishesADate(string date, int days)
    {
        var input = Header.Replace("20300102120000+0000", date, StringComparison.Ordinal);
        var broad = new DeidentificationWorkflow(new() { ShiftDays = days }, Token);
        var narrow = new DeidentificationWorkflow(new() { Preset = DeidentificationPreset.TimestampShifter, ShiftDays = days }, Token);
        Assert.Equal(Hl7ValueState.Empty, Apply(broad, input).Get("MSH-7").State);
        Assert.Equal(date, Apply(narrow, input).Get("MSH-7").Raw);
        Assert.Equal(1, broad.Summary.UnsupportedTimestamps); Assert.Equal(1, narrow.Summary.UnsupportedTimestamps);
    }
    [Fact]
    public void FullOrderAuthoritiesStayDistinctAndUnitDisplayCannotRetainNarrative()
    {
        var flow = new DeidentificationWorkflow(new(), Token);
        var a = Apply(flow, Patient() + "ORC|NW|SYN-ORDER^SYN-NS^1.2.3^ISO\rOBX|1|NM|SYN-CODE||98.6|mg/dL^SYN-PRIVATE-NARRATIVE^UCUM\r");
        var b = Apply(flow, Patient() + "OBR|1|SYN-ORDER^SYN-NS^1.2.3^ISO\r");
        var c = Apply(flow, Patient() + "OBR|1|SYN-ORDER^SYN-NS^1.2.4^ISO\r");
        Assert.Equal(a.Get("ORC-2").Raw, b.Get("OBR-2").Raw); Assert.NotEqual(b.Get("OBR-2").Raw, c.Get("OBR-2").Raw);
        Assert.Equal("mg/dL", a.Get("OBX-6.1").Raw); Assert.Equal("Synthetic unit", a.Get("OBX-6.2").Raw);
        Assert.Equal("UCUM", a.Get("OBX-6.3").Raw); Assert.DoesNotContain("SYN-PRIVATE", a.RawText);
    }
    [Fact]
    public void CancellationAndMappingBudgetFailureLeaveTheFacadeBytesUnchanged()
    {
        using var stop = new CancellationTokenSource(); stop.Cancel(); var input = Facade(Patient()); var before = input.Serialize();
        Assert.ThrowsAny<OperationCanceledException>(() => new DeidentificationWorkflow(new(), stop.Token).Apply(input)); Assert.Equal(before, input.Serialize());
        var flow = new DeidentificationWorkflow(new() { MaximumMappings = 1 }, Token);
        var error = Assert.Throws<ScriptRunException>(() => flow.Apply(input)); Assert.Equal(before, input.Serialize());
        Assert.DoesNotContain("SYN-PRIVATE", string.Join(" ", error.Diagnostics.Select(d => d.Description)));
    }
    [Fact]
    public void SummaryIsBoundedCountOnlyAndRelativeClinicalIntervalsRemainUnchanged()
    {
        var flow = new DeidentificationWorkflow(new() { ShiftDays = 31 }, Token);
        var result = Apply(flow, Patient() + "ORC|NW|SYN-ORDER^SYN-PLACER|SYN-FILL^SYN-FILLER||||||20300102110000+0000\rOBR|1|SYN-ORDER^SYN-PLACER|SYN-FILL^SYN-FILLER|SYN-SERVICE||20300102120000+0000|20300102130000+0000\r");
        Assert.Equal(result.Get("ORC-2").Raw, result.Get("OBR-2").Raw); Assert.Equal(result.Get("ORC-3").Raw, result.Get("OBR-3").Raw);
        Assert.Equal("20300202110000+0000", result.Get("ORC-9").Raw); Assert.Equal("20300202130000+0000", result.Get("OBR-7").Raw);
        Assert.Equal(DeidentificationSummary.Columns.Length, flow.Summary.Cells.Length); Assert.DoesNotContain("SYN-", string.Join(" ", flow.Summary.Cells));
    }
    [Theory]
    [InlineData(1)] [InlineData(100)] [InlineData(1000)]
    public void RepresentativeRunKeepsBoundedMapsAndOneParsePerControlledBatch(int count)
    {
        var flow = new DeidentificationWorkflow(new(), Token); var allocated = GC.GetAllocatedBytesForCurrentThread();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < count; i++)
        {
            var result = Apply(flow, Patient("SYN-" + i.ToString(CultureInfo.InvariantCulture)));
            Assert.Matches("^10[0-9]{7}$", result.Get("PID-3.1").Raw!);
        }
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(count, flow.Summary.Messages); Assert.True(bytes < count * 600_000L + 1024 * 1024, $"Allocated {bytes} bytes");
        TestContext.Current.TestOutputHelper!.WriteLine($"deidentification messages={count}; elapsedMs={clock.Elapsed.TotalMilliseconds:F3}; allocatedBytes={bytes}");
    }
}
