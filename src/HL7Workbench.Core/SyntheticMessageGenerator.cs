using System.Globalization;
using System.Text;

namespace HL7Workbench.Core;

/// <summary>Versioned, bounded synthetic HL7 v2.5.1 examples. This is not clinical or implementation-guide conformance validation.</summary>
public static partial class SyntheticMessageGenerator
{
    /// <summary>Original placeholder algorithm, retained byte-for-byte for explicit legacy style.</summary>
    public const int Version = 1;
    /// <summary>Shared realistic demographic projection of the unchanged standalone templates.</summary>
    public const int RealisticVersion = 2;
    public const int MaximumCount = 1000;
    public const int MaximumCharacters = 4 * 1024 * 1024;
    public const int MaximumMessageCharacters = 1024 * 1024;
    public const int MaximumLineCharacters = 16384;

    /// <summary>Generates the entire immutable batch or throws; cancellation never returns a partial batch.</summary>
    public static SyntheticMessageBatch Generate(SyntheticGenerationOptions options, CancellationToken token = default, IProgress<int>? progress = null) =>
        GenerateWithinBudget(options, new(MaximumCharacters, MaximumMessageCharacters, MaximumLineCharacters), token, progress);

    internal readonly record struct Budget(int Total, int Message, int Line);
    internal static SyntheticMessageBatch GenerateWithinBudget(SyntheticGenerationOptions options, Budget budget, CancellationToken token, IProgress<int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(options); options = options.Normalize(); token.ThrowIfCancellationRequested();
        var random = new StableRandom(options.Seed); var messages = new string[options.Count]; var total = 0;
        for (var i = 0; i < messages.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            var text = options.DemographicStyle == SyntheticDemographicStyle.RealisticSynthetic
                ? RealisticMessage(options, i + 1, ref random) : Message(options, i + 1, ref random);
            if (text.Length > Math.Min(budget.Message, MaximumMessageCharacters)) throw new Hl7LimitException("GeneratedMessageCharacters");
            var line = 0;
            foreach (var character in text)
            {
                if (character == '\r') line = 0;
                else if (++line > Math.Min(budget.Line, MaximumLineCharacters)) throw new Hl7LimitException("GeneratedLineCharacters");
            }
            total = checked(total + text.Length);
            if (total > Math.Min(budget.Total, MaximumCharacters)) throw new Hl7LimitException("GeneratedContentCharacters");
            messages[i] = text; progress?.Report(i + 1);
        }
        token.ThrowIfCancellationRequested(); return new(options, messages, total);
    }

    // xorshift32 is specified here rather than System.Random's runtime-dependent
    // implementation. All arithmetic wraps as unsigned 32-bit values; zero has a fixed mapping.
    internal struct StableRandom(uint seed)
    {
        private uint state = (seed ^ 0x9E3779B9u) is 0 ? 0x6D2B79F5u : seed ^ 0x9E3779B9u;
        public uint Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        public int Below(int maximum) => (int)(Next() % (uint)maximum);
    }
    internal static string Timestamp(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyyMMddHHmmss'+0000'", CultureInfo.InvariantCulture);
    internal static string Segment(string name, params (int Field, string Value)[] fields)
    {
        var values = new string[fields.Max(f => f.Field) + 1]; values[0] = name;
        foreach (var (field, value) in fields) values[field] = value;
        return string.Join('|', values) + "\r";
    }
    private static string Message(SyntheticGenerationOptions options, int number, ref StableRandom random)
    {
        var at = options.BaseTimestamp.AddMinutes(number - 1); var timestamp = Timestamp(at);
        var id = options.ControlId(number); var type = options.TypeLabel;
        var structure = options.MessageType is SyntheticMessageType.OrmO01 ? "ORM_O01" : options.MessageType is SyntheticMessageType.OruR01 ? "ORU_R01" : "ADT_A01";
        var birth = at.AddYears(-30 - random.Below(50)).AddDays(-random.Below(365)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var patient = string.Create(CultureInfo.InvariantCulture, $"SYNTHETIC{number:D4}^EXAMPLE{random.Below(1000):D3}^^^^L");
        const string provider = "SYN-PROV^SYNTHETIC^CLINICIAN";
        var result = new StringBuilder();
        result.Append($"MSH|^~\\&|SYN-GENERATOR|SYN-FACILITY|SYN-RECEIVER|SYN-DEST|{timestamp}||{type}^{structure}|{id}|T|2.5.1\r");
        var adt = options.MessageType is SyntheticMessageType.AdtA01 or SyntheticMessageType.AdtA04 or SyntheticMessageType.AdtA08;
        if (adt) result.Append(Segment("EVN", (1, type[4..]), (2, timestamp), (5, "SYN-OP^SYNTHETIC^OPERATOR"), (6, timestamp)));
        var update = options.MessageType is SyntheticMessageType.AdtA08;
        result.Append(Segment("PID", (1, "1"), (3, id + "-MR^^^SYN-FAC^MR"), (5, patient), (7, birth), (8, "U"),
            (11, (update ? "200 SYNTHETIC UPDATE WAY" : "100 SYNTHETIC TEST LANE") + "^^EXAMPLEVILLE^ZZ^00000^USA"),
            (13, update ? string.Create(CultureInfo.InvariantCulture, $"^NET^Internet^synthetic.{number}@example.invalid") : "")));
        var inpatient = options.MessageType == SyntheticMessageType.AdtA01;
        var location = inpatient ? "SYN-WARD^101^1^SYN-FAC" : options.MessageType == SyntheticMessageType.AdtA04 ? "SYN-REG^^^SYN-FAC" : "SYN-CLINIC^^^SYN-FAC";
        var visitStart = Timestamp(update || !adt ? at.AddHours(-1) : at);
        result.Append(Segment("PV1", (1, "1"), (2, inpatient ? "I" : "O"), (3, location), (4, "R"), (7, provider),
            (19, id + "-VISIT^^^SYN-FAC^VN"), (44, visitStart)));
        if (adt) return result.ToString();
        var reports = options.MessageType == SyntheticMessageType.OruR01;
        var placer = id + "-P^SYN-PLACER"; var filler = id + "-F^SYN-FILLER";
        result.Append(Segment("ORC", (1, reports ? "RE" : "NW"), (2, placer), (3, filler), (5, reports ? "CM" : "SC"),
            (9, reports ? Timestamp(at.AddMinutes(-30)) : timestamp), (12, provider), (15, reports ? Timestamp(at.AddMinutes(-30)) : Timestamp(at.AddMinutes(30)))));
        result.Append(Segment("OBR", (1, "1"), (2, placer), (3, filler), (4, "SYN-PANEL^Synthetic chemistry panel^99SYN"),
            (6, reports ? "" : Timestamp(at.AddMinutes(30))), (7, reports ? Timestamp(at.AddMinutes(-20)) : ""),
            (14, reports ? Timestamp(at.AddMinutes(-15)) : ""), (16, provider), (22, reports ? Timestamp(at.AddMinutes(-5)) : ""),
            (24, "LAB"), (25, reports ? "F" : "")));
        if (reports)
        {
            var glucose = (75 + random.Below(31)).ToString(CultureInfo.InvariantCulture);
            var sodium = (137 + random.Below(7)).ToString(CultureInfo.InvariantCulture);
            result.Append(Observation(1, "NM", "SYN-GLU^Synthetic glucose^99SYN", glucose, "mg/dL^milligram per deciliter^UCUM", "70-110", at));
            result.Append(Observation(2, "NM", "SYN-NA^Synthetic sodium^99SYN", sodium, "mmol/L^millimole per liter^UCUM", "135-145", at));
            result.Append(Observation(3, "ST", "SYN-NOTE^Synthetic result comment^99SYN", "SYNTHETIC TEST RESULT - NOT FOR CLINICAL USE", "", "", at));
        }
        else result.Append(Segment("NTE", (1, "1"), (3, "SYNTHETIC TEST ORDER - NOT FOR CLINICAL USE")));
        return result.ToString();
    }
    private static string Observation(int number, string type, string identifier, string value, string units, string reference, DateTimeOffset at) =>
        Segment("OBX", (1, number.ToString(CultureInfo.InvariantCulture)), (2, type), (3, identifier), (5, value), (6, units),
            (7, reference), (8, type == "NM" ? "N" : ""), (11, "F"), (14, Timestamp(at.AddMinutes(-20))), (19, Timestamp(at.AddMinutes(-5))));
}

/// <summary>Complete immutable output. MessageId session identities are assigned only by document publication, not encoded in this content.</summary>
public sealed class SyntheticMessageBatch
{
    internal SyntheticMessageBatch(SyntheticGenerationOptions options, string[] messages, int total)
    { Options = options; Messages = Array.AsReadOnly(messages); TotalCharacters = total; }
    public SyntheticGenerationOptions Options { get; }
    public IReadOnlyList<string> Messages { get; }
    public int TotalCharacters { get; }
    /// <summary>Canonical UTF-8 without a BOM; every segment terminates with CR on every platform.</summary>
    public byte[] GetUtf8Bytes() => Encoding.UTF8.GetBytes(string.Concat(Messages));
}
