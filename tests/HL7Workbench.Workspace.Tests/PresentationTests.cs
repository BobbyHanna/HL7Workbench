using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class PresentationTests
{
    public static IEnumerable<object[]> Structures()
    {
        var huge = new string('A', 17000);
        yield return ["OBX|1|ED|||" + huge + "|OBX6|OBX7|OBX8\rNTE|1||NOTE", "|OBX6|OBX7|OBX8\rNTE|1||NOTE", 1];
        yield return ["NTE|1||" + huge + "|NTE4", "|NTE4", 1];
        yield return ["ZXY|" + huge + "|NEXT", "|NEXT", 1];
        yield return ["OBX|1|TX|||" + huge + "^COMPONENT~REPETITION", "^COMPONENT~REPETITION", 1];
        yield return ["OBX|1|TX|||" + huge + "&SUB^COMP~REP", "&SUB^COMP~REP", 1];
        yield return ["OBX|1|TX|||" + huge + "~" + huge + "~LAST", "~LAST", 2];
        yield return ["MSH*$%!@*SYN\rZXY*" + huge + "$COMP%REP!SUB*NEXT", "$COMP%REP!SUB*NEXT", 1];
        yield return ["opaque " + huge + "\rPID|NEXT", "\rPID|NEXT", 1];
        yield return ["OBX|1|TX|||\\Z" + huge + "|escaped^data\\|NEXT", "|NEXT", 1];
    }
    [Theory, MemberData(nameof(Structures))]
    public void SmallestValuesAreFoldedAndFollowingStructureIsRetained(string body, string following, int markers)
    {
        var raw = body.StartsWith("MSH", StringComparison.Ordinal) ? body : "MSH|^~\\&|SYN\r" + body;
        var source = new StringHl7TextSource(raw);
        var structure = Hl7StructureScanner.Scan(source, Encoding.UTF8, cancellationToken: TestContext.Current.CancellationToken);
        var presentation = MessagePresentation.Create(source, structure, TestContext.Current.CancellationToken);
        var display = presentation.Read(source, new(0, presentation.Length));
        Assert.Equal(markers, presentation.Fragments.Count(f => f.Marker is not null));
        Assert.Contains(following, display);
        Assert.True(display.Length < 2000);
        Assert.Equal(raw, Hl7Parser.Parse(raw).RawText);
        foreach (var fragment in presentation.Fragments)
        {
            if (fragment.Source is { } range)
            {
                Assert.Equal(range.Offset, presentation.SourceOffset(fragment.Display.Offset));
                Assert.Equal(fragment.Display.Offset, presentation.DisplayOffset(range.Offset));
                Assert.Equal(raw.Substring((int)range.Offset, (int)range.Length), display.Substring((int)fragment.Display.Offset, (int)fragment.Display.Length));
            }
            else
            {
                Assert.Null(presentation.SourceOffset(fragment.Display.Offset));
                Assert.Equal(fragment.Display.Offset, presentation.DisplayOffset(fragment.Omitted!.Value.Offset + 1000));
                Assert.False(presentation.IsContiguousSource(fragment.Display));
            }
        }
        var after = raw.IndexOf(following, StringComparison.Ordinal);
        Assert.Equal(after, presentation.SourceOffset(presentation.DisplayOffset(after)));
    }
    [Theory]
    [InlineData(16383, false)] [InlineData(16384, false)] [InlineData(16385, true)]
    public void ThresholdAndOrdinaryPresentationAreExact(int length, bool folded)
    {
        var raw = "MSH|^~\\&|SYN\rNTE|1||" + new string('B', length) + "|TAIL";
        var source = new StringHl7TextSource(raw);
        var p = MessagePresentation.Create(source, Hl7StructureScanner.Scan(source, Encoding.UTF8, cancellationToken: TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        Assert.Equal(folded, p.Fragments.Any(f => f.Marker is not null));
        Assert.True(p.Blocks.All(b => b.Display.Length <= MessagePresentation.BlockCharacters));
        if (!folded) Assert.Equal(raw, p.Read(source, new(0, p.Length)));
    }
    [Fact]
    public void ManySmallValuesUseBoundedBlocksWithoutLosingCharacters()
    {
        var raw = "MSH|^~\\&|SYN\rZXY|" + string.Join('|', Enumerable.Repeat(new string('X', 1000), 2000));
        var source = new StringHl7TextSource(raw);
        var p = MessagePresentation.Create(source, Hl7StructureScanner.Scan(source, Encoding.UTF8, cancellationToken: TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        Assert.True(p.RequiresVirtualization); Assert.DoesNotContain(p.Fragments, f => f.Marker is not null);
        Assert.Equal(raw, string.Concat(p.Blocks.Select(b => p.Read(source, b.Display))));
    }
    [Fact]
    public async Task UnicodeAbbreviationHasExactByteRangesAndNoBrokenSurrogates()
    {
        var raw = "MSH|^~\\&|SYN\rNTE|1||" + string.Concat(Enumerable.Repeat("😀", 10000)) + "|TAIL";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        using var manager = new DocumentManager();
        var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
        try
        {
            var p = doc.Presentation!;
            var display = await doc.ReadAsync(s => p.Read(s, new(0, p.Length)), TestContext.Current.CancellationToken);
            Assert.NotNull(display); new UTF8Encoding(false, true).GetBytes(display);
            var marker = Assert.Single(p.Fragments, f => f.Marker is not null);
            Assert.Equal(40000, marker.OriginalBytes!.Value.Length);
        }
        finally { manager.Close(doc.Id); await doc.Disposal; }
    }
}
