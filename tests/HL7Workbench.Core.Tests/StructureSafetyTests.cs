using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class StructureSafetyTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MessageAmbiguityIsIndependentOfUnsupportedEditEncoding(bool ambiguous)
    {
        var text = ambiguous ? "MSH|^~^&|SYN\rPID|1||SYN\r" : "MSH|^~\\&|SYN\rPID|1||SYN\r";
        var structure = Hl7StructureScanner.Scan(new StringHl7TextSource(text), new UTF32Encoding(false, true, true), cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(structure.CanEdit); Assert.Equal(!ambiguous, structure.HasUnambiguousMessageStructure);
    }
    [Fact]
    public void CappedDiagnosticsCannotChangeStructuralSafety()
    {
        var structure = Hl7StructureScanner.Scan(new StringHl7TextSource("MSH|^~^&|SYN\rBAD opaque\rPID|1\r"), Encoding.UTF8,
            new() { MaxDiagnostics = 1 }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(structure.Diagnostics); Assert.False(structure.HasUnambiguousMessageStructure); Assert.False(structure.CanEdit);
    }
}
