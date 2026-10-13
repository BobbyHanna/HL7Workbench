using System.Text;

namespace HL7Workbench.Core;

public sealed record Hl7ParseOptions
{
    public Encoding? Encoding { get; init; }
    public int MaxMessageBytes { get; init; } = 16 * 1024 * 1024;
    public int MaxMessageCharacters { get; init; } = 8 * 1024 * 1024;
    public int MaxSegmentLength { get; init; } = 1024 * 1024;
    public int MaxSegments { get; init; } = 100_000;
    public int MaxElements { get; init; } = 250_000;
    public int MaxElementsPerContainer { get; init; } = 10_000;
    public int MaxEditExpansion { get; init; } = 1024;
    public int MaxDiagnostics { get; init; } = 1024;

    internal void Validate()
    {
        if (MaxMessageBytes < 1 || MaxMessageCharacters < 1 || MaxSegmentLength < 1 ||
            MaxSegments < 1 || MaxElements < 1 || MaxElementsPerContainer < 1 || MaxEditExpansion < 0 || MaxDiagnostics < 1)
            throw new ArgumentOutOfRangeException(nameof(Hl7ParseOptions), "Limits must be positive; edit expansion may be zero.");
    }

    internal static Encoding Strict(Encoding encoding)
    {
        var copy = (Encoding)encoding.Clone();
        copy.DecoderFallback = DecoderFallback.ExceptionFallback;
        copy.EncoderFallback = EncoderFallback.ExceptionFallback;
        return copy;
    }
}

public sealed class Hl7LimitException(string limit) : Exception($"HL7 resource limit exceeded: {limit}.")
{
    public string Limit { get; } = limit;
}
