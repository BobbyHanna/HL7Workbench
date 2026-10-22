using System.Globalization;
using System.Text;

namespace HL7Workbench.Core;

public enum AckRequest { NE, AL, SU, ER }
public enum TestAckMode { None, Positive, ApplicationError, ApplicationReject, CommitError, CommitReject }
public enum AckValidationFailure { InvalidEnvelope, MissingFields, Mismatch }
public sealed class Hl7AckException(AckValidationFailure failure) : IOException("Acknowledgment validation failed: " + failure)
{ public AckValidationFailure Failure { get; } = failure; }

/// <summary>Transport eligibility is header/structure validation, not clinical validation.</summary>
public sealed record Hl7TransportHeader(Hl7Message Message, string ControlId, string Type, string Trigger,
    AckRequest Accept, AckRequest Application, bool Original)
{
    public static Hl7TransportHeader Read(ReadOnlySpan<byte> payload, Encoding encoding)
    {
        var message = Hl7Parser.Parse(payload, new() { Encoding = encoding, MaxMessageBytes = 1024 * 1024, MaxMessageCharacters = 1024 * 1024 });
        if (!message.HasDecodedText || message.Segments.Count == 0 || message.Segments[0].Identifier != "MSH" ||
            message.Segments.Count(s => s.Identifier == "MSH") != 1 || message.Diagnostics.Any(d => d.Severity == Hl7DiagnosticSeverity.Error))
            throw new InvalidDataException("A single structurally readable HL7 message is required.");
        string Value(string path) => message.Get(path).Raw ?? "";
        string Decoded(string path) => Value(path).Length == 0 ? "" : message.GetDecoded(path).Text;
        var id = Value("MSH-10"); var type = Decoded("MSH-9.1");
        if (id.Length is 0 or > 199 || id == "\"\"" || type is "" or "\"\"" || Value("MSH-7") is "" or "\"\"" ||
            Value("MSH-11") is "" or "\"\"" || Value("MSH-12") is "" or "\"\"" ||
            id.Any(char.IsControl)) throw new InvalidDataException("Required transport header fields are missing or invalid.");
        // Correlation uses the exact encoded identifier. Copying it with the same
        // delimiter alphabet preserves escapes without interpreting them as facts.
        var accept = Decoded("MSH-15"); var application = Decoded("MSH-16");
        if (accept == "\"\"") accept = "";
        if (application == "\"\"") application = "";
        var original = accept.Length == 0 && application.Length == 0;
        if (!original && (!Enum.TryParse<AckRequest>(accept, out _) || !Enum.TryParse<AckRequest>(application, out _) ||
            !Enum.GetNames<AckRequest>().Contains(accept) || !Enum.GetNames<AckRequest>().Contains(application)))
            throw new InvalidDataException("Enhanced acknowledgment mode requires AL, NE, SU or ER in both MSH-15 and MSH-16.");
        return new(message, id, type, Value("MSH-9.2"), original ? AckRequest.NE : Enum.Parse<AckRequest>(accept),
            original ? AckRequest.AL : Enum.Parse<AckRequest>(application), original);
    }

    public string ReadAck(ReadOnlySpan<byte> payload, Encoding encoding)
    {
        Hl7TransportHeader reply;
        try { reply = Read(payload, encoding); }
        catch (InvalidDataException) { throw new Hl7AckException(AckValidationFailure.MissingFields); }
        if (reply.Type != "ACK" || reply.Message.Segments.Count(s => s.Identifier == "MSA") != 1)
            throw new Hl7AckException(AckValidationFailure.InvalidEnvelope);
        if (reply.Message.Get("MSA-1").Raw is null or "" || reply.Message.Get("MSA-2").Raw is null or "")
            throw new Hl7AckException(AckValidationFailure.MissingFields);
        if (!Equivalent(Message, "MSH-10", reply.Message, "MSA-2") || Equivalent(Message, "MSH-10", reply.Message, "MSH-10") ||
            reply.Message.Get("MSH-12").Raw != Message.Get("MSH-12").Raw)
            throw new Hl7AckException(AckValidationFailure.Mismatch);
        foreach (var component in new[] { 1, 2 })
            if (!Equivalent(Message, $"MSH-11.{component}", reply.Message, $"MSH-11.{component}"))
                throw new Hl7AckException(AckValidationFailure.Mismatch);
        // Where routing identifiers were supplied, require their mirrored reply.
        foreach (var pair in new[] { (3, 5), (4, 6), (5, 3), (6, 4) })
            if (Message.Get($"MSH-{pair.Item1}").Raw is { Length: > 0 })
                foreach (var component in new[] { 1, 2, 3 })
                    if (!Equivalent(Message, $"MSH-{pair.Item1}.{component}", reply.Message, $"MSH-{pair.Item2}.{component}"))
                        throw new Hl7AckException(AckValidationFailure.Mismatch);
        var code = reply.Message.GetDecoded("MSA-1").Text;
        if (code is not ("AA" or "AE" or "AR" or "CA" or "CE" or "CR")) throw new InvalidDataException("Unsupported acknowledgment code.");
        return code;
    }

    private static bool Equivalent(Hl7Message first, string firstPath, Hl7Message second, string secondPath)
    {
        var a = first.Get(firstPath).Raw ?? ""; var b = second.Get(secondPath).Raw ?? "";
        if (a == b && first.Delimiters == second.Delimiters) return true;
        // Alternate delimiter alphabets can encode the same identifier differently.
        // Unknown/local escapes never become guessed correlation evidence.
        var left = Hl7EscapeCodec.Decode(a, first.Delimiters, first.Encoding);
        var right = Hl7EscapeCodec.Decode(b, second.Delimiters, second.Encoding);
        return left.Diagnostics.Count == 0 && right.Diagnostics.Count == 0 && left.Text == right.Text;
    }

    /// <summary>Builds only protocol envelopes, honoring requested success/error phases. Never ACKs an ACK.</summary>
    public IReadOnlyList<byte[]> CreateTestAcks(TestAckMode mode, Encoding encoding)
    {
        if (mode == TestAckMode.None || Type == "ACK") return [];
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var commit = mode switch { TestAckMode.CommitError => "CE", TestAckMode.CommitReject => "CR", _ => "CA" };
        var app = mode switch { TestAckMode.ApplicationError => "AE", TestAckMode.ApplicationReject => "AR", _ => "AA" };
        if (Original && commit != "CA") app = commit == "CE" ? "AE" : "AR";
        var codes = new List<string>();
        if (!Original && Wants(Accept, commit == "CA")) codes.Add(commit);
        if ((Original || commit == "CA") && Wants(Application, app == "AA")) codes.Add(app);
        return codes.Select(Build).ToArray();

        byte[] Build(string code)
        {
            var f = Message.Delimiters.Field; var c = Message.Delimiters.Component;
            string V(int n) => Message.Get($"MSH-{n}").Raw ?? "";
            string Literal(string value) => Hl7EscapeCodec.Encode(value, Message.Delimiters, 4096, encoding);
            // Twenty characters fit legacy MSH-10 too. Generated metadata uses the
            // declared alphabet, including a custom delimiter inside a UTC offset.
            var fields = new[] { "MSH", V(2), V(5), V(6), V(3), V(4), Literal(DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmsszzz", CultureInfo.InvariantCulture).Replace(":", "")),
                "", Literal("ACK") + c + Trigger + c + Literal("ACK"), Literal(Guid.NewGuid().ToString("N")[..20]), V(11), V(12), "", "", Literal("NE"), Literal("NE"), "",
                Literal(encoding.CodePage == 65001 ? "UNICODE UTF-8" : "ASCII") };
            return encoding.GetBytes(string.Join(f, fields) + "\rMSA" + f + Literal(code) + f + ControlId + "\r");
        }
    }
    public static bool Wants(AckRequest request, bool success) => request == AckRequest.AL || request == (success ? AckRequest.SU : AckRequest.ER);
}
