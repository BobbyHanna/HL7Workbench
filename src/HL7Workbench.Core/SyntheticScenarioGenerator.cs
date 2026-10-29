using System.Collections.ObjectModel;
using System.Globalization;
using static HL7Workbench.Core.SyntheticDemographics;
using Patient = HL7Workbench.Core.SyntheticDemographics.Patient;
using System.Text;
using static HL7Workbench.Core.SyntheticMessageGenerator;

namespace HL7Workbench.Core;

/// <summary>Counts only; no patient/provider/order identifiers or values are retained.</summary>
public sealed record SyntheticScenarioSummary(int Patients, int Encounters, int Orders, int Results, int Observations,
    IReadOnlyDictionary<string, int> MessageTypes);

/// <summary>Complete, bounded, parser-validated scenario output. Publication assigns unrelated session identities.</summary>
public sealed class SyntheticScenarioBatch
{
    internal SyntheticScenarioBatch(SyntheticScenarioOptions options, List<string> messages, SyntheticScenarioSummary summary)
    { Options = options; Messages = messages.AsReadOnly(); Summary = summary; TotalCharacters = messages.Sum(m => m.Length); }
    public SyntheticScenarioOptions Options { get; }
    public IReadOnlyList<string> Messages { get; }
    public SyntheticScenarioSummary Summary { get; }
    public int TotalCharacters { get; }
    public byte[] GetUtf8Bytes() => new UTF8Encoding(false, true).GetBytes(string.Concat(Messages));
}

/// <summary>Curated fictional workflows, independent of UI, files, networks and source records.</summary>
public static partial class SyntheticScenarioGenerator
{
    public const int Version = 2;
    private sealed record Encounter(Patient Patient, string Id, DateTimeOffset Start, string Attending, string Ordering, bool Inpatient, string InitialLocation, string TransferLocation);
    private sealed record Order(string Placer, string Filler, string Service, DateTimeOffset Placed, DateTimeOffset Collected, decimal First, decimal Second);

    /// <summary>Identical normalized inputs and version yield identical bytes. Throws on cancellation; never returns partial output.</summary>
    public static SyntheticScenarioBatch Generate(SyntheticScenarioOptions options, CancellationToken token = default, IProgress<int>? progress = null) =>
        GenerateWithinBudget(options, new(MaximumCharacters, MaximumMessageCharacters, MaximumLineCharacters), token, progress);

    internal static SyntheticScenarioBatch GenerateWithinBudget(SyntheticScenarioOptions options, Budget budget, CancellationToken token, IProgress<int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(options); token.ThrowIfCancellationRequested(); options = options.Normalize();
        var count = options.EstimateMessageCount(); var messages = new List<string>(count);
        var types = new Dictionary<string, int>(StringComparer.Ordinal); var ids = new HashSet<string>(StringComparer.Ordinal);
        var random = new StableRandom(options.Seed); var total = 0; DateTimeOffset? previous = null;
        var root = string.Create(CultureInfo.InvariantCulture, $"SYN-{options.Prefix}-{options.Seed:X8}");
        var realistic = options.DemographicStyle == SyntheticDemographicStyle.RealisticSynthetic;
        for (var p = 1; p <= options.Patients; p++)
        {
            token.ThrowIfCancellationRequested();
            var patient = CreatePatient(options, p, ref random);
            for (var e = 1; e <= options.EncountersPerPatient; e++)
            {
                var encounterRoot = string.Create(CultureInfo.InvariantCulture, $"{root}-P{p:D4}-E{e:D3}");
                var encounterNumber = (p - 1) * options.EncountersPerPatient + e;
                var inpatient = options.EncounterType == SyntheticEncounterType.Inpatient;
                var locations = CareLocations(options, encounterNumber);
                var encounter = new Encounter(patient, realistic ? string.Create(CultureInfo.InvariantCulture, $"{root}-VN{encounterNumber:D6}") : encounterRoot + "-VISIT",
                    options.BaseTimestamp.AddDays(encounterNumber - 1),
                    realistic ? Provider(options.Seed, encounterNumber, false) : encounterRoot + "-ATT^SYNTHETIC^ATTENDING",
                    realistic ? Provider(options.Seed, encounterNumber, true) : encounterRoot + "-ORD^SYNTHETIC^ORDERING", inpatient, locations.Initial, locations.Transfer);
                var location = encounter.InitialLocation;
                if (options.HasAdt) Emit(encounter.Inpatient ? "ADT^A01" : "ADT^A04", encounter.Start);
                if (options.IncludeTransfer) { location = encounter.TransferLocation; Emit("ADT^A02", encounter.Start.AddMinutes(15)); }
                if (options.HasOrders)
                {
                    for (var o = 1; o <= options.OrdersPerEncounter; o++)
                    {
                        token.ThrowIfCancellationRequested();
                        // Each order owns a report-update sequence. All reports repeat
                        // that order's collected observations, progressing P → F once.
                        var placed = encounter.Start.AddMinutes(30 + (o - 1) * (options.ResultsPerOrder + 2) * 5);
                        var orderRoot = string.Create(CultureInfo.InvariantCulture, $"{encounterRoot}-O{o:D2}");
                        var chemistry = options.ClinicalExample == SyntheticClinicalExample.Chemistry;
                        var orderNumber = (encounterNumber - 1) * options.OrdersPerEncounter + o;
                        var placer = realistic ? string.Create(CultureInfo.InvariantCulture, $"{root}-PO{orderNumber:D6}^SYN-PLACER") : orderRoot + "-P^SYN-PLACER";
                        var filler = realistic ? string.Create(CultureInfo.InvariantCulture, $"{root}-FO{orderNumber:D6}^SYN-FILLER") : orderRoot + "-F^SYN-FILLER";
                        var order = new Order(placer, filler,
                            (options.Overrides.OrderCode ?? (chemistry ? "SYN-CHEM" : "SYN-CBC")) +
                            (chemistry ? "^Synthetic chemistry panel^99SYN" : "^Synthetic blood count^99SYN"), placed, placed.AddMinutes(5),
                            options.Overrides.ObservationValue ?? (chemistry ? 75 + random.Below(31) : (120 + random.Below(41)) / 10m),
                            chemistry ? 137 + random.Below(7) : (45 + random.Below(46)) / 10m);
                        Emit("ORM^O01", placed, order);
                        for (var r = 1; r <= options.ResultsPerOrder; r++)
                            Emit("ORU^R01", placed.AddMinutes((r + 1) * 5), order, r == options.ResultsPerOrder ? options.Overrides.ResultStatus ?? "F" : "P");
                    }
                }
                if (options.HasAdt) Emit("ADT^A03", encounter.Start.AddHours(23));

                void Emit(string type, DateTimeOffset at, Order? order = null, string? status = null)
                {
                    token.ThrowIfCancellationRequested();
                    if (previous is { } before && at < before || at < encounter.Start || order is not null && at < order.Placed)
                        throw new InvalidOperationException("Synthetic workflow timeline invariant failed.");
                    // MSH-10 is limited to 20 characters in v2.5.1. Patient/order
                    // prefixes must not make this transport identifier overlong.
                    var control = string.Create(CultureInfo.InvariantCulture, $"SYN{(realistic ? 2 : 1)}-{options.Seed:X8}-{messages.Count + 1:D4}");
                    if (!ids.Add(control)) throw new InvalidOperationException("Synthetic message identity invariant failed.");
                    var text = Render(options, encounter, location, type, control, at, order, status);
                    if (text.Length > Math.Min(budget.Message, MaximumMessageCharacters)) throw new Hl7LimitException("GeneratedMessageCharacters");
                    var line = 0;
                    foreach (var c in text)
                        if (c == '\r') line = 0; else if (++line > Math.Min(budget.Line, MaximumLineCharacters)) throw new Hl7LimitException("GeneratedLineCharacters");
                    total = checked(total + text.Length);
                    if (total > Math.Min(budget.Total, MaximumCharacters)) throw new Hl7LimitException("GeneratedContentCharacters");
                    // Validate one bounded message at a time. Never retain a batch of
                    // parsed models or implement another delimiter/path interpreter.
                    var model = Hl7Parser.Parse(text);
                    if (model.Diagnostics.Count != 0 || Hl7Validator.Validate(model).Count != 0 ||
                        model.Get("PID-3.1").Raw != patient.Id || model.Get("PV1-19.1").Raw != encounter.Id ||
                        model.Get("MSH-10").Raw != control || order is not null &&
                        (model.Get("ORC-2").Raw != order.Placer || model.Get("OBR-2").Raw != order.Placer ||
                         model.Get("ORC-3").Raw != order.Filler || model.Get("OBR-3").Raw != order.Filler || model.Get("OBR-4").Raw != order.Service))
                        throw new InvalidOperationException("Synthetic HL7 structure or shared-context invariant failed.");
                    messages.Add(Hl7Serializer.SerializeText(model)); previous = at;
                    types[type] = types.GetValueOrDefault(type) + 1; progress?.Report(messages.Count);
                }
            }
        }
        token.ThrowIfCancellationRequested();
        if (messages.Count != count) throw new InvalidOperationException("Synthetic message estimate invariant failed.");
        var encounters = options.Patients * options.EncountersPerPatient;
        var orders = options.HasOrders ? encounters * options.OrdersPerEncounter : 0;
        var results = orders * options.ResultsPerOrder;
        return new(options, messages, new(options.Patients, encounters, orders, results, results * 2, new ReadOnlyDictionary<string, int>(types)));
    }

    private static string Render(SyntheticScenarioOptions options, Encounter visit, string location, string type, string control,
        DateTimeOffset at, Order? order, string? status)
    {
        var adt = type.StartsWith("ADT", StringComparison.Ordinal); var discharge = type == "ADT^A03";
        var structure = type switch { "ADT^A02" => "ADT_A02", "ADT^A03" => "ADT_A03", "ORM^O01" => "ORM_O01", "ORU^R01" => "ORU_R01", _ => "ADT_A01" };
        var stamp = Timestamp(at); var patient = visit.Patient;
        var text = new StringBuilder();
        text.Append($"MSH|^~\\&|{options.SendingApplication}|{options.SendingFacility}|{options.ReceivingApplication}|{options.ReceivingFacility}|{stamp}||{type}^{structure}|{control}|T|{options.Hl7Version}\r");
        if (adt) text.Append(Segment("EVN", (1, type[4..]), (2, stamp), (6, stamp)));
        var patientFields = new List<(int Field, string Value)> { (1, "1"), (3, patient.Id + "^^^" + options.SendingFacility + "^MR"),
            (5, patient.Name), (7, patient.Birth), (8, patient.Sex), (11, patient.Address) };
        // Leave the legacy segment's trailing field boundary unchanged. Optional
        // phones use structured XTN components only in the new demographic mode.
        if (patient.Telephone.Length > 0) patientFields.Add((13, patient.Telephone));
        text.Append(Segment("PID", patientFields.ToArray()));
        text.Append(Segment("PV1", (1, "1"), (2, visit.Inpatient ? "I" : "O"), (3, location),
            (6, type == "ADT^A02" ? visit.InitialLocation : ""), (7, visit.Attending),
            (19, visit.Id + "^^^" + options.SendingFacility + "^VN"), (36, discharge ? "01" : ""), (44, Timestamp(visit.Start)), (45, discharge ? stamp : "")));
        if (order is null) return text.ToString();
        var reported = status is not null; var final = status == "F";
        var orderFields = new List<(int Field, string Value)> { (1, reported ? "RE" : "NW"), (2, order.Placer), (3, order.Filler),
            (5, reported ? final ? "CM" : "A" : "SC"), (9, stamp), (12, visit.Ordering), (15, Timestamp(order.Collected)) };
        if (options.DemographicStyle == SyntheticDemographicStyle.RealisticSynthetic)
            orderFields.Add((21, Components(options.SendingFacilityName, "", "", "", "", "SYN-FACILITY", "", "", "", options.SendingFacility)));
        text.Append(Segment("ORC", orderFields.ToArray()));
        text.Append(Segment("OBR", (1, "1"), (2, order.Placer), (3, order.Filler), (4, order.Service),
            (6, reported ? "" : Timestamp(order.Collected)), (7, reported ? Timestamp(order.Collected) : ""),
            (14, reported ? Timestamp(order.Collected.AddMinutes(1)) : ""), (15, options.ClinicalExample == SyntheticClinicalExample.Chemistry ? "SYN-SER&Synthetic serum&99SYN" : "SYN-BLD&Synthetic whole blood&99SYN"),
            (16, visit.Ordering), (22, reported ? stamp : ""), (24, "LAB"), (25, status ?? "")));
        if (reported)
        {
            if (options.ClinicalExample == SyntheticClinicalExample.Chemistry)
            {
                Observation(1, "SYN-GLU^Synthetic glucose^99SYN", order.First, "mg/dL", 70, 110);
                Observation(2, "SYN-NA^Synthetic sodium^99SYN", order.Second, "mmol/L", 135, 145);
            }
            else
            {
                Observation(1, "SYN-HGB^Synthetic hemoglobin^99SYN", order.First, "g/dL", 12, 17);
                Observation(2, "SYN-WBC^Synthetic white blood count^99SYN", order.Second, "10*9/L", 4, 11);
            }
        }
        text.Append(Segment("NTE", (1, "1"), (3, "SYNTHETIC TEST DATA - NOT FOR CLINICAL USE")));
        return text.ToString();

        void Observation(int number, string code, decimal value, string units, decimal lower, decimal upper) =>
            text.Append(Segment("OBX", (1, number.ToString(CultureInfo.InvariantCulture)), (2, "NM"), (3, code),
                (4, number.ToString(CultureInfo.InvariantCulture)), (5, value.ToString("0.############################", CultureInfo.InvariantCulture)), (6, units + "^^UCUM"),
                (7, lower.ToString(CultureInfo.InvariantCulture) + "-" + upper.ToString(CultureInfo.InvariantCulture)),
                (8, value < lower ? "L" : value > upper ? "H" : "N"), (11, status!), (14, Timestamp(order.Collected)), (19, stamp)));
    }
}
