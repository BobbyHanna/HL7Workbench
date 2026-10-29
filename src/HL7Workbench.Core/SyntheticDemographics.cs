using System.Globalization;
using static HL7Workbench.Core.SyntheticMessageGenerator;

namespace HL7Workbench.Core;

internal static class SyntheticDemographics
{
    internal sealed record Patient(string Id, string Name, string Birth, string Sex, string Address, string Telephone);

    // Original small vocabulary, authored for this feature; not a person/address
    // dataset. Ordering and PRNG domains are part of both realistic-v2 contracts.
    private static readonly string[] FamilyNames = ["MORGAN", "BENNETT", "PARKER", "HAYES", "SULLIVAN", "BROOKS", "REED", "FOSTER", "COLEMAN", "WALKER", "MITCHELL", "TURNER", "RAMIREZ", "PATEL", "KIM", "NGUYEN", "CHEN", "ROBINSON", "JAMES", "HARTWELL", "MORALES", "PRICE", "O'NEILL", "DE LA CRUZ", "SMITH-JONES", "ELLIS", "WALLACE", "PERRY", "GRANT", "LINDEN", "MASON", "LAWSON"];
    private static readonly string[] FemaleNames = ["ELIZABETH", "SARAH", "AMELIA", "MAYA", "HANNAH", "NATALIE", "OLIVIA", "ISABEL", "CLAIRE", "EVELYN", "SOFIA", "AUDREY", "NAOMI", "JULIA", "LEAH", "VIVIAN"];
    private static readonly string[] MaleNames = ["JAMES", "DANIEL", "BENJAMIN", "MARCUS", "DAVID", "ETHAN", "GABRIEL", "ADRIAN", "NATHAN", "JONATHAN", "SAMUEL", "LUKE", "MILES", "ANTHONY", "ISAAC", "NOAH"];
    private static readonly string[] NeutralNames = ["AVERY", "JORDAN", "RILEY", "MORGAN", "CASEY", "QUINN", "ROWAN", "TAYLOR", "CAMERON", "ELLIS", "ALEX", "REESE"];
    private static readonly string[] MiddleNames = ["ANNE", "LEE", "MARIE", "JAMES", "ROSE", "MICHAEL", "GRACE", "RAY", "ELENA", "DEAN", "JEAN", "RENEE"];
    private static readonly string[] SexCodes = ["F", "M", "F", "M", "F", "M", "O", "U"];
    private static readonly string[] StreetNames = ["MAPLE", "WILLOW", "CEDAR", "OAK", "PINE", "BIRCH", "RIVERBEND", "MEADOW", "HIGHLAND", "LAUREL", "CHESTNUT", "WALNUT"];
    private static readonly string[] StreetTypes = ["STREET", "AVENUE", "LANE", "DRIVE", "COURT", "ROAD"];
    private sealed record Locality(string City, string State, string ZipPrefix, string AreaCode);
    // ZIPs are constructed formatting examples, not a postal/deliverability lookup.
    // The second XAD component explicitly marks this feature's synthetic namespace.
    private static readonly Locality[] Localities =
    [
        new("FAIRVIEW", "MI", "49", "616"), new("CEDAR GLEN", "OH", "43", "614"),
        new("MEADOWBROOK", "CO", "80", "303"), new("BROOKHAVEN", "NC", "27", "919"),
        new("OAK RIDGE", "VA", "22", "703"), new("LAKEWOOD", "OR", "97", "503"),
        new("PINE HOLLOW", "NH", "03", "603"), new("WILLOW CREEK", "MA", "01", "413")
    ];
    private sealed record Facility(string Code, string Name, string LaboratoryCode, string LaboratoryName);
    private static readonly Facility[] Facilities =
    [
        new("SYN-NVMC", "NORTH VALLEY MEDICAL CENTER", "SYN-NVLAB", "NORTH VALLEY DIAGNOSTIC LABORATORY"),
        new("SYN-CRCH", "CEDAR RIDGE COMMUNITY HOSPITAL", "SYN-CRLAB", "CEDAR RIDGE DIAGNOSTIC LABORATORY"),
        new("SYN-LRH", "LAKESHORE REGIONAL HOSPITAL", "SYN-LRLAB", "LAKESHORE REGIONAL LABORATORY"),
        new("SYN-CPMC", "CEDAR & PINE MEDICAL CENTER", "SYN-CPLAB", "CEDAR & PINE DIAGNOSTIC LABORATORY"),
        new("SYN-RBMC", "RIVERBEND MEDICAL CENTER", "SYN-RBLAB", "RIVERBEND DIAGNOSTIC LABORATORY"),
        new("SYN-MCH", "MEADOWBROOK COMMUNITY HOSPITAL", "SYN-MCLAB", "MEADOWBROOK DIAGNOSTIC LABORATORY")
    ];

    internal static (string SendingApplication, string SendingFacility, string ReceivingApplication, string ReceivingFacility)
        DemographicDefaults(SyntheticDemographicStyle style, uint seed)
    {
        if (style == SyntheticDemographicStyle.ObviousPlaceholders) return ("SYN-GENERATOR", "SYN-FACILITY", "SYN-RECEIVER", "SYN-DEST");
        var random = new StableRandom(seed ^ 0x46414332u); var facility = Facilities[random.Below(Facilities.Length)];
        return ("EHR", facility.Code, "LAB-INTERFACE", facility.LaboratoryCode);
    }

    internal static string FacilityName(string code)
    {
        foreach (var facility in Facilities)
        {
            if (facility.Code == code) return facility.Name;
            if (facility.LaboratoryCode == code) return facility.LaboratoryName;
        }
        // An unknown explicit facility is never relabeled as a generated institution.
        return code;
    }

    internal static string Components(params string[] values) =>
        string.Join('^', values.Select(v => Hl7EscapeCodec.Encode(v, Hl7Delimiters.Default, 256)));

    internal static Patient CreatePatient(uint seed, string prefix, int number, DateTimeOffset at)
    {
        var random = new StableRandom(seed ^ 0x44454D32u ^ unchecked((uint)number * 0x9E3779B9u));
        var sex = SexCodes[random.Below(SexCodes.Length)];
        var family = FamilyNames[random.Below(FamilyNames.Length)];
        var givenPool = sex == "F" ? FemaleNames : sex == "M" ? MaleNames : NeutralNames;
        var given = givenPool[random.Below(givenPool.Length)]; var middle = Middle(ref random);
        // These panels and ward templates model adults. Use an actual calendar DOB
        // relative to this patient's first encounter, including leap-year behavior.
        var birth = at.AddYears(-18 - random.Below(72)).AddDays(-random.Below(365)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var locality = Localities[random.Below(Localities.Length)];
        var street = string.Create(CultureInfo.InvariantCulture, $"{100 + random.Below(9800)} {StreetNames[random.Below(StreetNames.Length)]} {StreetTypes[random.Below(StreetTypes.Length)]}");
        var zip = locality.ZipPrefix + random.Below(1000).ToString("D3", CultureInfo.InvariantCulture);
        var address = Components(street, "SYNTHETIC TEST ADDRESS", locality.City, locality.State, zip, "USA", "H");
        // NANPA reserves precisely 555-0100..0199 for fictional use. Other 555
        // ranges must not be treated as fictional. The finite pool can repeat.
        var telephone = random.Below(5) == 0 ? "" : Components("", "PRN", "PH", "", "1", locality.AreaCode,
            "555" + (100 + random.Below(100)).ToString("D4", CultureInfo.InvariantCulture));
        return new(string.Create(CultureInfo.InvariantCulture, $"SYN-{prefix}-{seed:X8}-MR{number:D6}"),
            Components(family, given, middle, "", "", "", "L"), birth, sex, address, telephone);
    }

    private static string Middle(ref StableRandom random)
    {
        var choice = random.Below(4); if (choice == 0) return "";
        var middle = MiddleNames[random.Below(MiddleNames.Length)]; return choice == 1 ? middle[..1] : middle;
    }

    internal static string Provider(uint seed, int encounter, bool ordering)
    {
        var random = new StableRandom(seed ^ (ordering ? 0x4F524432u : 0x41545432u) ^ unchecked((uint)encounter * 0x9E3779B9u));
        var family = FamilyNames[random.Below(FamilyNames.Length)]; var names = random.Below(2) == 0 ? FemaleNames : MaleNames;
        var given = names[random.Below(names.Length)]; var middle = Middle(ref random);
        var id = string.Create(CultureInfo.InvariantCulture, $"SYN-PRV-{seed:X8}-{encounter:D4}-{(ordering ? "OR" : "AT")}");
        return Components(id, family, given, middle, "", "", "", "", "SYN-PROVIDER", "", "", "", "PRN");
    }

    internal static (string Initial, string Transfer) CareLocations(uint seed, int encounter, bool inpatient, string facility)
    {
        var random = new StableRandom(seed ^ 0x4C4F4332u ^ unchecked((uint)encounter * 0x9E3779B9u));
        if (!inpatient)
            return (Components("OUTPATIENT", (1 + random.Below(12)).ToString("D2", CultureInfo.InvariantCulture), "A", facility), "");
        var room = (201 + random.Below(20)).ToString(CultureInfo.InvariantCulture);
        // A planned bed move within MEDSURG fits the curated routine encounters;
        // no unrelated ICU/ED admission is invented by a demographic setting.
        return (Components("MEDSURG", room, "A", facility), Components("MEDSURG", room, "B", facility));
    }
}
