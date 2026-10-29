using System.Globalization;
using System.Text;
using static HL7Workbench.Core.SyntheticDemographics;

namespace HL7Workbench.Core;

public static partial class SyntheticMessageGenerator
{
    private static string RealisticMessage(SyntheticGenerationOptions options, int number, ref StableRandom random)
    {
        // The original renderer remains the sole standalone template and clinical
        // stream. Project only existing demographic fields using Core-parsed ranges;
        // never split HL7 text or add segments/fields to demonstrate realism.
        var legacy = Message(new() { MessageType = options.MessageType, Count = options.Count, Seed = options.Seed,
            BaseTimestamp = options.BaseTimestamp, Prefix = options.Prefix }, number, ref random);
        var model = Hl7Parser.Parse(legacy);
        var patient = CreatePatient(options.Seed, options.PatientIdentifierPrefix ?? options.Prefix, number, options.BaseTimestamp.AddMinutes(number - 1));
        var attending = Provider(options.Seed, number, false); var ordering = Provider(options.Seed, number, true);
        var location = CareLocations(options.Seed, number, options.MessageType == SyntheticMessageType.AdtA01, options.SendingFacility).Initial;
        string Id(string domain) => string.Create(CultureInfo.InvariantCulture, $"SYN-{options.Prefix}-{options.Seed:X8}-{domain}{number:D6}");
        var replacements = new List<(Hl7Field Field, string Value)>();
        void Replace(string segment, int field, string value) => replacements.Add((model.Segments.Single(s => s.Identifier == segment).GetField(field)
            ?? throw new InvalidOperationException("The standalone template is missing a required demographic field."), value));
        Replace("MSH", 3, options.SendingApplication); Replace("MSH", 4, options.SendingFacility);
        Replace("MSH", 5, options.ReceivingApplication); Replace("MSH", 6, options.ReceivingFacility); Replace("MSH", 10, options.ControlId(number));
        Replace("PID", 3, Components(patient.Id, "", "", options.SendingFacility, "MR"));
        Replace("PID", 5, patient.Name); Replace("PID", 7, patient.Birth); Replace("PID", 8, patient.Sex);
        Replace("PID", 11, patient.Address); Replace("PID", 13, patient.Telephone);
        Replace("PV1", 3, location); Replace("PV1", 7, attending);
        Replace("PV1", 19, Components(Id("VN"), "", "", options.SendingFacility, "VN"));
        if (options.MessageType is SyntheticMessageType.AdtA01 or SyntheticMessageType.AdtA04 or SyntheticMessageType.AdtA08)
            Replace("EVN", 5, attending);
        else
        {
            var placer = Components(Id("PO"), options.SendingFacility); var filler = Components(Id("FO"), options.ReceivingFacility);
            Replace("ORC", 2, placer); Replace("OBR", 2, placer); Replace("ORC", 3, filler); Replace("OBR", 3, filler);
            Replace("ORC", 12, ordering); Replace("OBR", 16, ordering);
        }
        var text = new StringBuilder(); var offset = 0;
        foreach (var (field, value) in replacements.OrderBy(r => r.Field.CharacterOffset))
        {
            text.Append(legacy.AsSpan(offset, field.CharacterOffset - offset)).Append(value);
            offset = field.CharacterOffset + field.Raw.Length;
        }
        text.Append(legacy.AsSpan(offset));
        // Bounded materialized messages only. Validate the complete projection,
        // including escapes, before it can enter a closed batch/publication.
        var projected = Hl7Parser.Parse(text.ToString());
        if (projected.Diagnostics.Count != 0 || Hl7Validator.Validate(projected).Count != 0)
            throw new InvalidOperationException("The realistic standalone message failed structural validation.");
        return Hl7Serializer.SerializeText(projected);
    }
}
