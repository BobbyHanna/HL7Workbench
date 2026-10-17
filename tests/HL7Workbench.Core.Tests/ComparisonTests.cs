using System.Diagnostics;
using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class ComparisonTests
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261016000000||ORU^R01|SYN-1|T|2.5\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Hl7ComparisonResult Compare(string a, string b, Hl7ComparisonOptions? options = null)
    {
        var left = new StringHl7TextSource(a); var right = new StringHl7TextSource(b);
        return Hl7Comparison.Compare(left, Hl7StructureScanner.Scan(left, Encoding.UTF8, cancellationToken: Token),
            right, Hl7StructureScanner.Scan(right, Encoding.UTF8, cancellationToken: Token), options, Token);
    }
    [Theory]
    [InlineData("")] [InlineData("PID")] [InlineData("PID|1||\"\"|A^B&C~D\r")]
    [InlineData("BAD malformed|text\r\n\rZ99|A\\unclosed^B\n")]
    public void IdenticalSourcesHaveNoDifferences(string body) => Assert.Empty(Compare(Header + body, Header + body).Differences);

    [Theory]
    [InlineData("PID|A", "PID|B", "PID[1]-1")]
    [InlineData("PID|A^B", "PID|A^C", "PID[1]-1[1].2")]
    [InlineData("PID|A&B^C", "PID|A&D^C", "PID[1]-1[1].1.2")]
    [InlineData("PID|A~B", "PID|A~C", "PID[1]-1[2]")]
    [InlineData("MSH|^~\\&|A", "MSH|^~\\&|B", "MSH[1]-3")]
    public void ChangesResolveToMeaningfulCoordinates(string before, string after, string path)
    {
        var d = Assert.Single(Compare(before, after).Differences);
        Assert.Equal(Hl7DifferenceKind.Modified, d.Kind); Assert.Equal(path, d.Path);
        Assert.Equal(d.BeforeValue, before.Substring((int)d.Before!.Range.Offset, (int)d.Before.Range.Length));
        Assert.Equal(d.AfterValue, after.Substring((int)d.After!.Range.Offset, (int)d.After.Range.Length));
    }
    [Theory]
    [InlineData("PID", "PID|", null, "")]
    [InlineData("PID|A", "PID|A|", null, "")]
    [InlineData("PID|", "PID|\"\"", "", "\"\"")]
    [InlineData("PID|A", "PID|A^", null, "")]
    public void MissingEmptyAndExplicitNullRemainDistinct(string a, string b, string? oldValue, string newValue)
    { var d = Assert.Single(Compare(a, b).Differences); Assert.Equal(oldValue, d.BeforeValue); Assert.Equal(newValue, d.AfterValue); }
    [Theory]
    [InlineData("PID malformed", "PID|A")]
    [InlineData("PID|A", "PID malformed")]
    public void OpaqueToStructuredChangesRetainBothWholeSourceRanges(string a, string b)
    {
        var diff = Assert.Single(Compare(Header + a, Header + b).Differences);
        Assert.Equal(Hl7DifferenceKind.Modified, diff.Kind); Assert.Equal(a, diff.BeforeValue); Assert.Equal(b, diff.AfterValue);
        Assert.Null(diff.Before!.Field); Assert.Null(diff.After!.Field);
    }

    [Theory]
    [InlineData("PID|A\rOBX|X\rOBX|Y\r", "PID|A\rOBX|X\rOBX|NEW\rOBX|Y\r", Hl7DifferenceKind.Added, 2)]
    [InlineData("OBX|X\rOBX|NEW\rOBX|Y\r", "OBX|X\rOBX|Y\r", Hl7DifferenceKind.Removed, 2)]
    [InlineData("OBX|X\rOBX|X\r", "OBX|X\rOBX|X\rOBX|X\r", Hl7DifferenceKind.Added, 3)]
    [InlineData("PID|A\rOBX|X\r", "PID|A\rPV1|I\rOBX|X\r", Hl7DifferenceKind.Added, 1)]
    [InlineData("PID|A\rPV1|I\rOBX|X\r", "PID|A\rOBX|X\r", Hl7DifferenceKind.Removed, 1)]
    public void OrderedAlignmentRecognizesInsertedAndDeletedSegments(string a, string b, Hl7DifferenceKind kind, int occurrence)
    {
        var d = Assert.Single(Compare(Header + a, Header + b).Differences); Assert.Equal(kind, d.Kind);
        Assert.Equal(occurrence, (d.Before ?? d.After)!.Occurrence);
    }
    [Fact]
    public void InsertedSegmentDoesNotShiftTheOccurrenceOfLaterModification()
    {
        var result = Compare(Header + "OBX|A\rOBX|B\rOBX|C\r", Header + "OBX|NEW\rOBX|A\rOBX|B\rOBX|changed\r");
        Assert.Equal(2, result.Differences.Count); var change = result.Differences[1];
        Assert.Equal(3, change.Before!.Occurrence); Assert.Equal(4, change.After!.Occurrence);
        Assert.Contains("OBX[4]", change.Summary); Assert.Equal("C", change.BeforeValue);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NameAnchorsRecognizeInsertionEvenWhenEveryFollowingSegmentIsModified(bool reverse)
    {
        var a = "PID|A\rPV1|I\r"; var b = "ZZZ|INSERTED\rPID|B\rPV1|O\r";
        var diffs = Compare(reverse ? b : a, reverse ? a : b).Differences;
        Assert.Equal(3, diffs.Count); Assert.Equal(reverse ? Hl7DifferenceKind.Removed : Hl7DifferenceKind.Added, diffs[0].Kind);
        Assert.All(diffs.Skip(1), d => Assert.Equal(Hl7DifferenceKind.Modified, d.Kind));
    }
    [Theory]
    [InlineData("PID|A~B", "PID|A~NEW~B", Hl7DifferenceKind.Added, 2)]
    [InlineData("PID|A~NEW~B", "PID|A~B", Hl7DifferenceKind.Removed, 2)]
    public void RepetitionAlignmentPreservesExplicitOccurrence(string a, string b, Hl7DifferenceKind kind, int repetition)
    { var d = Assert.Single(Compare(a, b).Differences); Assert.Equal(kind, d.Kind); Assert.Equal(repetition, (d.Before ?? d.After)!.Repetition); }

    [Fact]
    public void CustomDelimitersAreStructuralAndDeclarationsAreLiteral()
    {
        var a = "MSH|^~\\&|SYN\rPID|A^B&C~D\r";
        var b = "MSH*$%!@*SYN\rPID*A$B@C%D\r";
        var diffs = Compare(a, b).Differences; Assert.Equal(2, diffs.Count);
        Assert.All(diffs, d => Assert.Equal(Hl7DifferenceKind.DelimiterDefinition, d.Kind));
        Assert.Equal(new int?[] { 1, 2 }, diffs.Select(d => d.Before!.Field));
        Assert.Single(Compare(b, b.Replace("B@C", "B@E", StringComparison.Ordinal)).Differences);
    }
    [Theory]
    [InlineData("PID|A\\F\\B", "PID|A\\X7C\\B", Hl7DifferenceKind.SourceSpelling)]
    [InlineData("PID|\"\"", "PID|\\X2222\\", Hl7DifferenceKind.Modified)]
    [InlineData("PID|A\\Zlocal\\", "PID|A\\Zother\\", Hl7DifferenceKind.Modified)]
    public void SourceSpellingIsDistinctFromSemanticValueChanges(string a, string b, Hl7DifferenceKind expected) =>
        Assert.Equal(expected, Assert.Single(Compare(Header + a, Header + b).Differences).Kind);

    [Theory]
    [InlineData(false, false, 2)] [InlineData(true, false, 1)] [InlineData(false, true, 1)] [InlineData(true, true, 0)]
    public void MshIgnoreOptionsAffectOnlyTheirWholeFields(bool timestamp, bool control, int expected)
    {
        var changed = Header.Replace("20261016000000", "20261017000000", StringComparison.Ordinal).Replace("SYN-1", "SYN-2", StringComparison.Ordinal);
        var options = new Hl7ComparisonOptions(timestamp, control);
        Assert.Equal(expected, Compare(Header, changed, options).Differences.Count);
        Assert.Equal(expected + 1, Compare(Header + "PID|A", changed + "PID|B", options).Differences.Count);
    }
    [Theory]
    [InlineData("PID|A", "PID|A||", 2)] [InlineData("PID|A|B", "PID|A|B|", 1)]
    public void TrailingEmptiesAreOptInAndDoNotHideInteriorChanges(string a, string b, int count)
    {
        Assert.Equal(count, Compare(a, b).Differences.Count);
        Assert.Empty(Compare(a, b, new(IgnoreTrailingEmptyFields: true)).Differences);
        Assert.Single(Compare(a, b.Replace("A", "C", StringComparison.Ordinal), new(IgnoreTrailingEmptyFields: true)).Differences);
    }
    [Theory]
    [InlineData("\r", "\r\n")] [InlineData("\n", "\r")] [InlineData("", "\r")]
    public void TerminatorsAreSourceLevelDifferences(string a, string b)
    {
        var d = Assert.Single(Compare("PID|A" + a, "PID|A" + b).Differences);
        Assert.Equal(Hl7DifferenceKind.Terminator, d.Kind); Assert.Equal(a, d.BeforeValue); Assert.Equal(b, d.AfterValue);
        Assert.Empty(Compare("PID|A" + a, "PID|A" + b, new(IgnoreTerminators: true)).Differences);
    }
    [Fact]
    public void CombinedIgnoresExcludeCountsAndPreserveSource()
    {
        var a = Header + "PID|A\r"; var b = Header.Replace("SYN-1", "SYN-2", StringComparison.Ordinal).Replace("20261016000000", "0", StringComparison.Ordinal).Replace("\r", "\r\n", StringComparison.Ordinal) + "PID|A||\r\n";
        Assert.Empty(Compare(a, b, new(true, true, true, true)).Differences); Assert.NotEmpty(Compare(a, b).Differences);
        var parsed = Hl7Parser.Parse(Encoding.UTF8.GetBytes(a)); var bytes = Hl7Serializer.Serialize(parsed);
        Compare(parsed.RawText, b); Assert.Equal(bytes, Hl7Serializer.Serialize(parsed)); Assert.False(parsed.IsModified);
    }
    [Fact]
    public void LargeValuesUseExplicitBoundedPreviewsAndCancellableChunkReads()
    {
        var a = "OBX|" + new string('A', 2 * 1024 * 1024); var b = a[..^1] + "B";
        var diff = Assert.Single(Compare(a, b).Differences);
        Assert.True(diff.BeforeAbbreviated); Assert.True(diff.AfterAbbreviated); Assert.Equal(256, diff.BeforeValue!.Length);
        Assert.Equal(2 * 1024 * 1024, diff.Before!.Range.Length);
        var source = new StringHl7TextSource(a); var structure = Hl7StructureScanner.Scan(source, Encoding.UTF8, cancellationToken: Token);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => Hl7Comparison.Compare(source, structure, source, structure, cancellationToken: cancel.Token));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BoundedAlignmentStressIsDeterministic(bool worstCase)
    {
        var a = Header + string.Concat(Enumerable.Range(0, 6000).Select(i => $"OBX|{(worstCase ? "A" : i.ToString(System.Globalization.CultureInfo.InvariantCulture))}\r"));
        var b = worstCase ? a.Replace("OBX|A", "OBX|B", StringComparison.Ordinal) : a.Insert(a.IndexOf("OBX|3000\r", StringComparison.Ordinal), "OBX|INSERTED\r");
        var clock = Stopwatch.StartNew(); var one = Compare(a, b); var two = Compare(a, b);
        Assert.Equal(one.Differences, two.Differences); Assert.Equal(worstCase ? 6000 : 1, one.Differences.Count);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30));
    }
    [Fact]
    public void ResultLimitFailsExplicitlyWithoutPartialResults()
    {
        var a = string.Concat(Enumerable.Repeat("OBX|A\r", Hl7Comparison.MaximumDifferences + 1));
        Assert.Throws<Hl7LimitException>(() => Compare(a, ""));
    }
}
