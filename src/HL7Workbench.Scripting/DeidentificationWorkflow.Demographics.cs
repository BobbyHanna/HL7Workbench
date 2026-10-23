using System.Globalization;
using HL7Workbench.Core;

namespace HL7Workbench.Scripting;

public sealed partial class DeidentificationWorkflow
{
    private SyntheticIdentity Profile(string identity) => synthetic!.Get(identity, cancellation);
    private void SyntheticIdentifier(Hl7Repetition repetition, SyntheticIdentity person, string domain, int authority, int type,
        string identity, List<ScriptMessage.LiteralEdit> edits)
    {
        var oid = "2.25." + ulong.Parse(Digest("synthetic-namespace", identity)[..16], NumberStyles.HexNumber, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        for (var c = 0; c < repetition.Components.Count; c++)
        for (var s = 0; s < repetition.Components[c].Subcomponents.Count; s++)
        {
            var value = c == 0 ? person.Identifier : domain == "provider" && c is 1 or 2 or 3 ?
                c == 1 ? (s == 0 ? person.FamilyName : "") : c == 2 ? person.GivenName : person.MiddleName :
                c + 1 == authority ? s switch { 0 => "WBTEST", 1 => oid, 2 => "ISO", _ => "" } :
                domain == "order" && c == 2 ? oid : domain == "order" && c == 3 ? "ISO" :
                c + 1 == type ? domain == "provider" ? "PRN" : repetition.Components[c].Raw is "MR" or "PI" or "PN" or "AN" or "VN" ? repetition.Components[c].Raw : "PI" : "";
            // Check digits, effective dates and other unsupported metadata cannot
            // retain the original or receive an invalid literal "SYNTHETIC" date.
            Write(repetition.Components[c].Subcomponents[s], value, edits, suppress: value.Length == 0);
        }
    }
    private static bool IsName(string segment, int number) => segment == "PID" && number is 5 or 6 or 9 || segment == "NK1" && number == 2 || segment == "GT1" && number == 3 || segment == "IN1" && number == 16;
    private static bool IsAddress(string segment, int number) => segment == "PID" && number == 11 || segment == "NK1" && number == 4 || segment == "GT1" && number == 5 || segment == "IN1" && number == 19;
    private static bool IsContact(string segment, int number) => segment == "PID" && number is 13 or 14 || segment == "NK1" && number is 5 or 6 || segment == "GT1" && number is 6 or 7;
    private static bool IsDemographic(string segment, int number) => segment == "PID" && number is 7 or 8 or 16 or 24 or 25 or 29 or 30 ||
        segment == "NK1" && number is 15 or 16 || segment == "GT1" && number is 8 or 9 || segment == "IN1" && number is 18 or 43;
    private static DeidentificationCategory Category(Hl7Segment segment, Hl7Field field)
    {
        var name = segment.Identifier; var number = field.Number;
        if (IsProvider(name, number)) return DeidentificationCategory.Providers;
        if (IsName(name, number)) return DeidentificationCategory.Names;
        if (IsAddress(name, number)) return DeidentificationCategory.Addresses;
        if (IsContact(name, number)) return DeidentificationCategory.Contacts;
        if (IsDemographic(name, number) || IsAge(segment, field)) return DeidentificationCategory.Demographics;
        if (Identity(name, number) is not null) return DeidentificationCategory.Identifiers;
        if (name == "OBX" && number == 5 && segment.GetField(2)?.Raw is "ED" or "RP") return DeidentificationCategory.Attachments;
        if (name == "NTE" || name == "OBX" && number == 5 && segment.GetField(2)?.Raw is "TX" or "FT" or "ST") return DeidentificationCategory.Narratives;
        return DeidentificationCategory.Other;
    }
    private SyntheticIdentity Person(Hl7Message model, Hl7Segment segment, Context context)
    {
        if (segment.Identifier == "PID" || segment.Identifier == "OBX") return Profile(context.Tag);
        if (segment.Identifier == "GT1" && segment.GetField(2)?.Repetitions.FirstOrDefault() is { } rep &&
            QualifiedIdentity(model, rep, "guarantor", 4, 5) is { } qualified) return Profile("guarantor:" + qualified);
        if (segment.Identifier == "IN1" && segment.GetField(49)?.Repetitions.FirstOrDefault() is { } insured &&
            QualifiedIdentity(model, insured, "insurance", 4, 5) is { } insurance) return Profile("insurance:" + insurance);
        var occurrence = model.Segments.Where(s => s.Identifier == segment.Identifier).TakeWhile(s => !ReferenceEquals(s, segment)).Count();
        // Unqualified contacts have no reliable cross-message identity key. Use a
        // documented patient/role/occurrence context, never a source name or address.
        return Profile(Digest("contact-role", context.Tag, segment.Identifier, occurrence.ToString(CultureInfo.InvariantCulture)));
    }
    private bool SubstituteDemographics(Hl7Message model, Hl7Segment segment, Hl7Field field, Context context, List<ScriptMessage.LiteralEdit> edits)
    {
        var name = segment.Identifier; var number = field.Number;
        if (!(IsName(name, number) || IsAddress(name, number) || IsContact(name, number) || IsDemographic(name, number) || IsAge(segment, field))) return false;
        var person = Person(model, segment, context);
        if (IsAge(segment, field))
        {
            var reference = new DateOnly(2030, 1, 1);
            var stamp = segment.GetField(14)?.Repetitions.FirstOrDefault()?.Components[0].Subcomponents[0].Raw ??
                model.Segments.FirstOrDefault(s => s.Identifier == "MSH")?.GetField(7)?.Raw;
            if (stamp is not null && Hl7TimestampShift.TryShift(stamp, ShiftDays, out var shiftedDate))
                DateOnly.TryParseExact(shiftedDate.AsSpan(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out reference);
            var age = person.AgeOn(reference);
            Replace(field, (_, c, _) => c == 0 && age >= 0 ? age.ToString(CultureInfo.InvariantCulture) : "", edits);
            if (age < 0) Risk(DeidentificationReason.InvalidSyntheticAge);
            return true;
        }
        if (IsName(name, number))
        {
            Replace(field, (r, c, s) =>
            {
                var alias = r > 0 || name == "PID" && number is 6 or 9 ? Profile(Digest("name-alias", context.Tag, name, number.ToString(CultureInfo.InvariantCulture), r.ToString(CultureInfo.InvariantCulture))) : person;
                return c switch { 0 => s == 0 ? alias.FamilyName : "", 1 => alias.GivenName, 2 => alias.MiddleName, 6 => "L", 7 => "A", _ => "" };
            }, edits); return true;
        }
        if (IsAddress(name, number))
        {
            Replace(field, (_, c, s) => c switch
            {
                0 => s switch { 0 => person.Street, 1 => person.Street[(person.Street.IndexOf(' ') + 1)..], 2 => person.DwellingNumber, _ => "" },
                1 => person.Unit, 2 => person.Locality,
                3 => person.Region, 4 => person.PostalCode, 5 => person.Country, 6 => "H", _ => ""
            }, edits); return true;
        }
        if (IsContact(name, number))
        {
            Replace(field, (r, c, _) =>
            {
                var rep = field.Repetitions[r];
                var email = rep.Components.ElementAtOrDefault(2)?.Raw == "Internet" || rep.Components.ElementAtOrDefault(3)?.Value.State == Hl7ValueState.Value;
                return c switch { 0 => email ? "" : person.Telephone, 1 => "PRN", 2 => email ? "Internet" : "PH", 3 => person.Email,
                    4 => "1", 5 => person.AreaCode, 6 => person.LocalTelephone, 7 => (person.Allocation / 100 + 1).ToString(CultureInfo.InvariantCulture), _ => "" };
            }, edits); return true;
        }
        var birth = name == "PID" && number == 7 || name == "NK1" && number == 16 || name == "GT1" && number == 8 || name == "IN1" && number == 18;
        var sex = name == "PID" && number == 8 || name == "NK1" && number == 15 || name == "GT1" && number == 9 || name == "IN1" && number == 43;
        Replace(field, (_, c, _) => c > 0 ? "" : birth ? person.DateOfBirth.ToString("yyyyMMdd", CultureInfo.InvariantCulture) : sex ? person.AdministrativeSex : number switch
        { 16 => person.MaritalStatus, 24 => "N", 25 => "1", 30 => "N", _ => "" }, edits);
        return true;
    }
    private void Replace(Hl7Field field, Func<int, int, int, string> value, List<ScriptMessage.LiteralEdit> edits)
    {
        for (var r = 0; r < field.Repetitions.Count; r++)
        for (var c = 0; c < field.Repetitions[r].Components.Count; c++)
        for (var s = 0; s < field.Repetitions[r].Components[c].Subcomponents.Count; s++)
        {
            if (field.Repetitions[r].Components[c].Subcomponents[s].Value.State != Hl7ValueState.Value) continue;
            var replacement = value(r, c, s);
            Write(field.Repetitions[r].Components[c].Subcomponents[s], replacement, edits, suppress: replacement.Length == 0);
        }
    }
    // Only these explicit age conventions are supported; other numeric clinical
    // observations remain reviewable, and no arbitrary code is guessed to be age.
    private static bool IsAge(Hl7Segment segment, Hl7Field field) => field.Number == 5 && IsAgeObservation(segment);
    private static bool IsAgeObservation(Hl7Segment segment) => segment.Identifier == "OBX" &&
        segment.GetField(2)?.Raw == "NM" && segment.GetField(3)?.Repetitions.FirstOrDefault() is { } code &&
        (code.Components[0].Raw == "AGE" && (code.Components.Count < 3 || code.Components[2].Raw is "" or "L") ||
         code.Components[0].Raw is "21612-7" or "30525-0" && code.Components.Count >= 3 && code.Components[2].Raw == "LN");
}
