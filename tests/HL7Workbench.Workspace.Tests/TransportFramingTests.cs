using System.Text;
using HL7Workbench.Transport;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class TransportFramingTests
{
    [Fact]
    public void ExactWireEnvelopeAndByteByByteFragmentation()
    {
        var payload = Encoding.UTF8.GetBytes("MSH*%~\\&*SYN\rNTE*1**SYN-é\r\n"); var wire = MllpFrame.Encode(payload);
        Assert.Equal(0x0B, wire[0]); Assert.Equal(0x1C, wire[^2]); Assert.Equal(0x0D, wire[^1]); Assert.Equal(payload, wire[1..^2]);
        using var decoder = new MllpDecoder(); foreach (var b in wire) decoder.Feed([b]); decoder.Complete(); Assert.Equal(payload, decoder.Take()); Assert.Null(decoder.Take());
    }
    [Fact]
    public void CoalescedFramesAndSplitTerminatorAreIndependentOfReads()
    {
        using var decoder = new MllpDecoder(); var a = MllpFrame.Encode([65, 13]); var b = MllpFrame.Encode([66]);
        decoder.Feed(a.Concat(b[..^1]).ToArray()); Assert.Equal(new byte[] { 65, 13 }, decoder.Take()); Assert.Null(decoder.Take());
        decoder.Feed(b[^1..]); Assert.Equal(new byte[] { 66 }, decoder.Take()); decoder.Complete();
    }
    [Theory]
    [InlineData(new byte[] { 1 }, FramingFailure.UnexpectedData)]
    [InlineData(new byte[] { 11, 11 }, FramingFailure.UnexpectedData)]
    [InlineData(new byte[] { 11, 65, 28, 10 }, FramingFailure.InvalidTerminator)]
    public void MalformedInputIsNotSilentlyResynchronized(byte[] bytes, FramingFailure expected)
    { using var decoder = new MllpDecoder(); Assert.Equal(expected, Assert.Throws<MllpException>(() => decoder.Feed(bytes)).Failure); }
    [Theory]
    [InlineData(new byte[] { 11 })] [InlineData(new byte[] { 11, 65 })] [InlineData(new byte[] { 11, 65, 28 })]
    public void DisconnectMidFrameIsTruncated(byte[] bytes)
    { using var decoder = new MllpDecoder(); decoder.Feed(bytes); Assert.Equal(FramingFailure.Truncated, Assert.Throws<MllpException>(decoder.Complete).Failure); }
    [Fact]
    public void PayloadAndQueuedFrameBoundsAreEnforcedBeforeGrowth()
    {
        using var decoder = new MllpDecoder(2, 1); decoder.Feed([11, 65, 66]); Assert.Equal(FramingFailure.Oversized, Assert.Throws<MllpException>(() => decoder.Feed([67])).Failure);
        using var queue = new MllpDecoder(2, 1); queue.Feed(MllpFrame.Encode([65])); Assert.Equal(FramingFailure.QueueFull, Assert.Throws<MllpException>(() => queue.Feed(MllpFrame.Encode([66]))).Failure);
    }
    [Theory]
    [InlineData(new byte[0])] [InlineData(new byte[] { 11 })] [InlineData(new byte[] { 28 })]
    public void OutboundCannotInjectFraming(byte[] bytes) => Assert.Throws<InvalidDataException>(() => MllpFrame.Encode(bytes));
    [Fact]
    public void OutboundSizeIsBounded() => Assert.Throws<InvalidDataException>(() => MllpFrame.Encode(new byte[MllpFrame.MaximumPayloadBytes + 1]));
}
