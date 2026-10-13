using System.Text;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class ResourceLimitTests
{
    [Fact]
    public void ByteAndCharacterLimitsAreCheckedBeforeDecodingOrParsing()
    {
        Assert.Throws<Hl7LimitException>(() => Hl7Parser.Parse(new byte[5], new() { MaxMessageBytes = 4 }));
        Assert.Throws<Hl7LimitException>(() => Hl7Parser.Parse("PID|x", new() { MaxMessageCharacters = 4 }));
        Assert.Throws<Hl7LimitException>(() => Hl7Parser.Parse("PID|é", new() { MaxMessageBytes = 5 }));
        Assert.Throws<Hl7LimitException>(() => Hl7Parser.Parse(Encoding.UTF8.GetBytes("PID|x"), new() { MaxMessageCharacters = 4 }));
    }

    [Fact]
    public void LimitsIncludeBomBytes()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, 0x41 };
        Assert.Throws<Hl7LimitException>(() => Hl7Parser.Parse(bytes, new() { MaxMessageBytes = 3 }));
    }

    [Fact]
    public void SegmentLengthAndCountAreBounded()
    {
        Assert.Throws<Hl7LimitException>(() => Hl7Parser.Parse("PID|xxxxx", new() { MaxSegmentLength = 8 }));
        Assert.Throws<Hl7LimitException>(() => Hl7Parser.Parse("PID\rOBX", new() { MaxSegments = 1 }));
        Assert.Throws<Hl7LimitException>(() => Hl7Parser.Parse("\r\r", new() { MaxSegments = 1 }));
    }

    [Theory]
    [InlineData("PID|a|b|c")]
    [InlineData("PID|a~b~c")]
    [InlineData("PID|a^b^c")]
    [InlineData("PID|a&b&c")]
    public void EachContainerHasAnElementLimit(string raw) =>
        Assert.Throws<Hl7LimitException>(() => Hl7Parser.Parse(raw, new() { MaxElementsPerContainer = 2 }));

    [Fact]
    public void TotalElementLimitIncludesEmptyElementsAtEveryLevel()
    {
        Assert.Throws<Hl7LimitException>(() => Hl7Parser.Parse("PID|", new() { MaxElements = 3 }));
        Assert.Equal(Hl7ValueState.Empty, Hl7Parser.Parse("PID|", new() { MaxElements = 4 }).Get("PID-1").State);
    }

    [Fact]
    public void ExactlyAtLimitsIsAllowed()
    {
        var message = Hl7Parser.Parse("PID|", new()
        {
            MaxMessageBytes = 4, MaxMessageCharacters = 4, MaxSegmentLength = 4,
            MaxSegments = 1, MaxElements = 4, MaxElementsPerContainer = 1
        });
        Assert.Equal("PID|", message.RawText);
    }

    [Theory]
    [InlineData("PID-2147483647")]
    [InlineData("PID-1[2147483647]")]
    [InlineData("PID-1.2147483647")]
    [InlineData("PID-1.1.2147483647")]
    public void PathologicalEditIndicesFailWithoutChangingMessage(string path)
    {
        var message = Hl7Parser.Parse("PID|a");
        Assert.Throws<Hl7LimitException>(() => message.SetRaw(path, "x"));
        Assert.Equal("PID|a", message.RawText);
        Assert.False(message.IsModified);
        Assert.Equal(Hl7ValueState.Missing, message.Get(path).State);
    }

    [Fact]
    public void ExpansionBudgetAppliesAcrossNestedLevels()
    {
        var message = Hl7Parser.Parse("PID|a", new() { MaxEditExpansion = 3 });
        Assert.Throws<Hl7LimitException>(() => message.SetRaw("PID-2[2].2.2", "x"));
        Assert.Equal("PID|a", message.RawText);
        message.SetRaw("PID-4", "x");
        Assert.Equal("PID|a|||x", message.RawText);
    }

    [Fact]
    public void ZeroExpansionAllowsExistingValueEdits()
    {
        var message = Hl7Parser.Parse("PID|a", new() { MaxEditExpansion = 0 });
        message.SetRaw("PID-1", "b");
        Assert.Equal("b", message.Get("PID-1").Raw);
        Assert.Throws<Hl7LimitException>(() => message.SetRaw("PID-2", "x"));
    }

    [Fact]
    public void HugeConfiguredContainerLimitStillHonorsSegmentLimitBeforePaddingAllocation()
    {
        var message = Hl7Parser.Parse("PID|a", new()
        {
            MaxElementsPerContainer = int.MaxValue, MaxEditExpansion = int.MaxValue, MaxSegmentLength = 8
        });
        Assert.Throws<Hl7LimitException>(() => message.SetRaw("PID-2147483647", "x"));
        Assert.Equal("PID|a", message.RawText);
        Assert.Throws<Hl7LimitException>(() => message.SetRaw("PID-1[2147483647]", "x"));
        Assert.Throws<Hl7LimitException>(() => message.SetRaw("PID-1.2147483647", "x"));
        Assert.Throws<Hl7LimitException>(() => message.SetRaw("PID-1.1.2147483647", "x"));
    }

    [Fact]
    public void ReplacementCannotBypassTotalElementLimit()
    {
        var message = Hl7Parser.Parse("PID|a", new() { MaxElements = 4 });
        Assert.Throws<Hl7LimitException>(() => message.SetRaw("PID-1", "a~b"));
        Assert.Equal("PID|a", message.RawText);
    }

    [Fact]
    public void ReplacementSizeLimitsAreTransactional()
    {
        var message = Hl7Parser.Parse("PID|a", new() { MaxMessageCharacters = 6, MaxSegmentLength = 6, MaxMessageBytes = 6 });
        Assert.Throws<Hl7LimitException>(() => message.SetRaw("PID-1", "abc"));
        Assert.Throws<Hl7LimitException>(() => message.SetRaw("PID-1", "éé"));
        Assert.Throws<Hl7LimitException>(() => message.SetText("PID-1", "||||"));
        Assert.Equal("PID|a", message.RawText);
        Assert.False(message.IsModified);
    }

    [Fact]
    public void DiagnosticsHaveGlobalAndCodecLimits()
    {
        Assert.Single(Hl7Parser.Parse("PID|\\Q\\\\Q\\", new() { MaxDiagnostics = 1 }).Diagnostics,
            diagnostic => diagnostic.Code == "DiagnosticsSuppressed");
        Assert.Equal(2, Hl7Parser.Parse("bad\rbad", new() { MaxDiagnostics = 2 }).Diagnostics.Count);
        Assert.Single(Hl7EscapeCodec.Decode("\\Q\\\\Q\\", Hl7Delimiters.Default, maxDiagnostics: 1).Diagnostics,
            diagnostic => diagnostic.Code == "DiagnosticsSuppressed");
    }

    [Fact]
    public void EscapeOutputIsBoundedBeforeExpansion()
    {
        Assert.Throws<Hl7LimitException>(() => Hl7EscapeCodec.Encode("||", Hl7Delimiters.Default, 5));
        Assert.Throws<Hl7LimitException>(() => Hl7EscapeCodec.Decode("abcdef", Hl7Delimiters.Default, maxOutputCharacters: 5));
        Assert.Throws<ArgumentException>(() => Hl7EscapeCodec.Encode("a\nb", Hl7Delimiters.Default));
    }

    [Fact]
    public void InvalidLimitConfigurationsAreRejected()
    {
        var options = new Hl7ParseOptions[]
        {
            new() { MaxMessageBytes = 0 }, new() { MaxMessageCharacters = 0 },
            new() { MaxSegmentLength = 0 }, new() { MaxSegments = 0 },
            new() { MaxElements = 0 }, new() { MaxElementsPerContainer = 0 },
            new() { MaxEditExpansion = -1 }, new() { MaxDiagnostics = 0 }
        };
        foreach (var option in options)
            Assert.Throws<ArgumentOutOfRangeException>(() => Hl7Parser.Parse("", option));
    }
}
