using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class TransportAcknowledgmentTests
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    public static byte[] Request(string accept = "", string application = "", string id = "SYN-001", char field = '|', char component = '^') =>
        Utf8.GetBytes($"MSH{field}{component}~\\&{field}SYN-SEND{field}SYN-FAC{field}SYN-RECV{field}SYN-DEST{field}20300101120000{field}{field}ADT{component}A01{field}{id}{field}T{field}2.5.1{field}{field}{field}{accept}{field}{application}\rPID{field}1\r");
    [Fact]
    public void OriginalPolicyIsApplicationOnlyAndAckHasNewEnvelopeAndCorrelatedMsa()
    {
        var h = Hl7TransportHeader.Read(Request(), Utf8); Assert.True(h.Original); Assert.Equal(AckRequest.NE, h.Accept); Assert.Equal(AckRequest.AL, h.Application);
        var ack = Assert.Single(h.CreateTestAcks(TestAckMode.Positive, Utf8)); var parsed = Hl7Parser.Parse(ack);
        Assert.Equal("AA", h.ReadAck(ack, Utf8)); Assert.Equal("SYN-001", parsed.Get("MSA-2").Raw);
        Assert.NotEqual(h.ControlId, parsed.Get("MSH-10").Raw); Assert.Equal("SYN-RECV", parsed.Get("MSH-3").Raw);
        Assert.Equal(20, parsed.GetDecoded("MSH-10").Text.Length);
        Assert.Equal("SYN-SEND", parsed.Get("MSH-5").Raw); Assert.Equal("NE", parsed.Get("MSH-16").Raw);
        Assert.DoesNotContain("PID", parsed.RawText);
    }
    [Theory]
    [InlineData(TestAckMode.Positive, "AA")]
    [InlineData(TestAckMode.ApplicationError, "AE")]
    [InlineData(TestAckMode.ApplicationReject, "AR")]
    [InlineData(TestAckMode.CommitError, "AE")]
    [InlineData(TestAckMode.CommitReject, "AR")]
    public void OriginalTestModesCorrelate(TestAckMode mode, string expected)
    { var h = Hl7TransportHeader.Read(Request(), Utf8); Assert.Equal(expected, h.ReadAck(Assert.Single(h.CreateTestAcks(mode, Utf8)), Utf8)); }
    [Theory]
    [InlineData("AL", "AL", TestAckMode.Positive, "CA,AA")]
    [InlineData("AL", "AL", TestAckMode.ApplicationError, "CA,AE")]
    [InlineData("AL", "AL", TestAckMode.ApplicationReject, "CA,AR")]
    [InlineData("AL", "AL", TestAckMode.CommitError, "CE")]
    [InlineData("AL", "AL", TestAckMode.CommitReject, "CR")]
    [InlineData("NE", "NE", TestAckMode.Positive, "")]
    [InlineData("ER", "AL", TestAckMode.Positive, "AA")]
    [InlineData("SU", "NE", TestAckMode.Positive, "CA")]
    [InlineData("ER", "ER", TestAckMode.Positive, "")]
    [InlineData("NE", "ER", TestAckMode.ApplicationError, "AE")]
    [InlineData("SU", "SU", TestAckMode.CommitError, "")]
    public void EnhancedChoreographyHonorsConditionalAndNegativePhases(string accept, string app, TestAckMode mode, string expected)
    {
        var h = Hl7TransportHeader.Read(Request(accept, app), Utf8); Assert.False(h.Original);
        Assert.Equal(expected, string.Join(",", h.CreateTestAcks(mode, Utf8).Select(a => h.ReadAck(a, Utf8))));
    }
    [Theory]
    [InlineData("AL", "")] [InlineData("", "AL")] [InlineData("al", "NE")] [InlineData("0", "NE")] [InlineData("BAD", "AL")]
    public void IncompleteOrInvalidEnhancedPolicyIsExplicitlyUnsupported(string accept, string app) =>
        Assert.Throws<InvalidDataException>(() => Hl7TransportHeader.Read(Request(accept, app), Utf8));
    [Theory]
    [InlineData("", "SYN-001", AckValidationFailure.MissingFields)]
    [InlineData("AA", "", AckValidationFailure.MissingFields)]
    [InlineData("AA", "SYN-OTHER", AckValidationFailure.Mismatch)]
    public void MissingMsaFieldsAndMismatchedControlIdsAreNotAccepted(string code, string id, AckValidationFailure failure)
    {
        var h = Hl7TransportHeader.Read(Request(), Utf8); var ack = Utf8.GetString(h.CreateTestAcks(TestAckMode.Positive, Utf8)[0]);
        ack = ack[..ack.IndexOf("MSA", StringComparison.Ordinal)] + $"MSA|{code}|{id}\r";
        Assert.Equal(failure, Assert.Throws<Hl7AckException>(() => h.ReadAck(Utf8.GetBytes(ack), Utf8)).Failure);
    }
    [Theory]
    [InlineData("PID|1\rMSA|AA|SYN-001\r")]
    [InlineData("MSH|^~\\&|||||||ACK||T|2.5.1\rMSA|AA|SYN-001\r")]
    public void AnMsaSubstringDoesNotMakeAValidAck(string text)
    { var h = Hl7TransportHeader.Read(Request(), Utf8); Assert.Throws<Hl7AckException>(() => h.ReadAck(Utf8.GetBytes(text), Utf8)); }
    [Fact]
    public void CustomDelimitersAndEscapedIdentifierArePreserved()
    {
        var h = Hl7TransportHeader.Read(Request(id: "SYN\\F\\01", field: '*', component: '%'), Utf8);
        var ack = Assert.Single(h.CreateTestAcks(TestAckMode.Positive, Utf8)); Assert.Equal("AA", h.ReadAck(ack, Utf8));
        Assert.Contains("MSA*AA*SYN\\F\\01\r", Utf8.GetString(ack));
    }
    [Fact]
    public void CorrelationUnderstandsEquivalentEscapedIdentifiersWithDifferentDelimiterAlphabets()
    {
        var h = Hl7TransportHeader.Read(Request(id: "SYN\\F\\01", field: '*', component: '%'), Utf8);
        var ack = Hl7Parser.Parse(Assert.Single(h.CreateTestAcks(TestAckMode.Positive, Utf8))).RawText;
        ack = ack.Replace("*", "|").Replace("%", "^").Replace("MSA|AA|SYN\\F\\01", "MSA|AA|SYN*01");
        Assert.Equal("AA", h.ReadAck(Utf8.GetBytes(ack), Utf8));
    }
    [Fact]
    public void IdenticalEscapeSpellingWithDifferentMeaningDoesNotCorrelate()
    {
        var h = Hl7TransportHeader.Read(Request(id: "SYN\\F\\01", field: '*', component: '%'), Utf8);
        var ack = Utf8.GetString(Assert.Single(h.CreateTestAcks(TestAckMode.Positive, Utf8))).Replace('*', '|').Replace('%', '^');
        Assert.Equal(AckValidationFailure.Mismatch, Assert.Throws<Hl7AckException>(() => h.ReadAck(Utf8.GetBytes(ack), Utf8)).Failure);
    }
    [Fact]
    public void AckDoesNotGenerateAnAckLoopAndNoneModeDoesNotRespond()
    {
        var h = Hl7TransportHeader.Read(Request(), Utf8); Assert.Empty(h.CreateTestAcks(TestAckMode.None, Utf8));
        var ack = Hl7TransportHeader.Read(h.CreateTestAcks(TestAckMode.Positive, Utf8)[0], Utf8); Assert.Empty(ack.CreateTestAcks(TestAckMode.Positive, Utf8));
    }
    [Fact]
    public void BothExplicitHl7NullAckFieldsSelectOriginalMode()
    { var h = Hl7TransportHeader.Read(Request("\"\"", "\"\""), Utf8); Assert.True(h.Original); }
    [Theory]
    [InlineData(false, "UNICODE UTF-8")] [InlineData(true, "ASCII")]
    public void GeneratedAckDeclaresItsActualSelectedEncoding(bool ascii, string expected)
    {
        var encoding = ascii ? Encoding.ASCII : Utf8; var h = Hl7TransportHeader.Read(Request(), encoding);
        var ack = Hl7Parser.Parse(Assert.Single(h.CreateTestAcks(TestAckMode.Positive, encoding)), new() { Encoding = encoding });
        Assert.Equal(expected, ack.GetDecoded("MSH-18").Text); Assert.Equal(2, ack.Segments.Count);
    }
    [Theory]
    [InlineData("MSH-7", "", AckValidationFailure.MissingFields)]
    [InlineData("MSH-11", "P", AckValidationFailure.Mismatch)]
    [InlineData("MSH-12", "2.4", AckValidationFailure.Mismatch)]
    public void RequiredReplyTimestampProcessingAndVersionAreValidated(string path, string value, AckValidationFailure expected)
    {
        var h = Hl7TransportHeader.Read(Request(), Utf8); var ack = Hl7Parser.Parse(Assert.Single(h.CreateTestAcks(TestAckMode.Positive, Utf8)));
        ack.SetText(path, value); Assert.Equal(expected, Assert.Throws<Hl7AckException>(() => h.ReadAck(Hl7Serializer.Serialize(ack), Utf8)).Failure);
    }
    [Fact]
    public void GeneratedMetadataEscapesACustomFieldDelimiterInsideTimestamp()
    {
        var h = Hl7TransportHeader.Read(Request(field: '+', component: '%'), Utf8); var ack = Assert.Single(h.CreateTestAcks(TestAckMode.Positive, Utf8));
        Assert.Equal("AA", h.ReadAck(ack, Utf8)); var parsed = Hl7Parser.Parse(ack); Assert.EndsWith("+0000", parsed.GetDecoded("MSH-7").Text); Assert.Equal("T", parsed.Get("MSH-11").Raw);
    }
    [Theory]
    [InlineData("MSH|^~\\&|SYN||||||ADT^A01||T|2.5.1\r")]
    [InlineData("MSH|^~\\&|SYN||||||ADT^A01|\"\"|T|2.5.1\r")]
    [InlineData("not HL7")]
    public void MalformedOrMissingIdentityCannotReceivePositiveAck(string text) => Assert.Throws<InvalidDataException>(() => Hl7TransportHeader.Read(Utf8.GetBytes(text), Utf8));
}
