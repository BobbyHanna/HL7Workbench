using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class StructureScannerTests
{
    [Fact]
    public void LogicalRangesBeyondIntMaxDoNotMaterializeValues()
    {
        var source = new VirtualAttachment(int.MaxValue + 128L);
        var structure = Hl7StructureScanner.Scan(source, Encoding.UTF8, cancellationToken: TestContext.Current.CancellationToken);
        var segment = Assert.Single(structure.Segments);
        Assert.Equal(source.AttachmentLength, segment.Fields[0].Range.Length);
        Assert.Equal("END", segment.Fields[1].Preview);
        Assert.True(segment.Fields[1].Range.Offset > int.MaxValue);
        Assert.True(source.LargestRead <= 100);
        Assert.Equal(source.AttachmentLength, segment.Fields[0].ByteRange!.Value.Length);
    }
    [Theory]
    [InlineData("MSH|^~\\&|SYN\rPID||\"\"|A^B&C~D", "PID", 3)]
    [InlineData("MSH*$%!@*SYN\rZ99*A$B@C%D", "Z99", 1)]
    [InlineData("PID|A\\broken^B|END", "PID", 1)]
    public void MaterializationAndRangesShareExactHierarchy(string text, string id, int field)
    {
        var message = Hl7Parser.Parse(text);
        var structure = Hl7StructureScanner.Scan(new StringHl7TextSource(text), message.Encoding, cancellationToken: TestContext.Current.CancellationToken);
        var materialized = message.Segments.First(s => s.Identifier == id).GetField(field)!;
        var ranged = structure.Segments.First(s => s.Identifier == id).Fields.First(f => f.Number == field);
        Assert.Equal(materialized.Raw, text.Substring((int)ranged.Range.Offset, (int)ranged.Range.Length));
        Assert.Equal(materialized.Repetitions.Count, ranged.Children.Count);
        Assert.Equal(materialized.Value.State, ranged.State);
        Assert.Equal(message.Diagnostics.Select(d => d.Code), structure.Diagnostics.Select(d => d.Code));
        Assert.Equal(Hl7Validator.Validate(message), Hl7Validator.Validate(structure));
    }
    [Fact]
    public void EscapeAndDelimiterBudgetsFailExplicitlyWithoutPartialModel()
    {
        Assert.Throws<Hl7LimitException>(() => Hl7StructureScanner.Scan(new StringHl7TextSource("PID|\\Z" + new string('X', 10000) + "\\"), Encoding.UTF8,
            new() { MaxEscapeTokenLength = 100 }, TestContext.Current.CancellationToken));
        Assert.Throws<Hl7LimitException>(() => Hl7StructureScanner.Scan(new StringHl7TextSource("PID|" + new string('~', 10000)), Encoding.UTF8,
            new() { MaxElements = 100 }, TestContext.Current.CancellationToken));
        Assert.Throws<Hl7LimitException>(() => Hl7StructureScanner.Scan(new StringHl7TextSource("PID|\\Q\\\\Q\\"), Encoding.UTF8,
            new() { MaxEscapeTokens = 1 }, TestContext.Current.CancellationToken));
    }
    private sealed class VirtualAttachment(long length) : IHl7TextSource
    {
        public long AttachmentLength => length;
        public long Length => length + 8;
        public long LargestRead { get; private set; }
        public char this[long offset] => offset < 4 ? "PID|"[(int)offset] : offset < length + 4 ? 'A' : "|END"[(int)(offset - length - 4)];
        public string Read(CharacterRange range)
        { LargestRead = Math.Max(LargestRead, range.Length); return new string(Enumerable.Range(0, checked((int)range.Length)).Select(i => this[range.Offset + i]).ToArray()); }
        public long IndexOfAny(ReadOnlySpan<char> chars, long start, long count)
        {
            foreach (var position in new long[] { 0, 1, 2, 3, length + 4, length + 5, length + 6, length + 7 })
                if (position >= start && position < start + count && chars.Contains(this[position])) return position;
            return chars.Contains('A') && Math.Max(start, 4) < Math.Min(start + count, length + 4) ? Math.Max(start, 4) : -1;
        }
        public ByteRange? Bytes(CharacterRange range) => new(range.Offset, range.Length);
    }
}
