using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Scripting.Tests;

public sealed class SyntheticIdentityTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Segment(string name, params (int Field, string Value)[] values)
    {
        var fields = new string[values.Max(v => v.Field) + 1]; fields[0] = name;
        foreach (var (field, value) in values) fields[field] = value;
        return string.Join('|', fields) + "\r";
    }
    private static string Input(string authority = "SYN-AUTH", string birth = "19800229", string sex = "M", string name = "SYN-PRIVATE-FAMILY^SYN-PRIVATE-GIVEN^SYN-PRIVATE-MIDDLE") =>
        "MSH|^~\\&|SYN|TEST|DEST|TEST|20300102120000+0000||ADT^A01|SYN-ID|T|2.5.1\r" +
        Segment("PID", (1, "1"), (3, $"SYN-ID^^^{authority}^MR"), (5, name + "~SYN-PRIVATE-ALIAS^SYN-PRIVATE-ALT^SYN-PRIVATE-MIDDLE"), (7, birth), (8, sex),
            (11, "SYN-PRIVATE-STREET^SYN-PRIVATE-UNIT^SYN-PRIVATE-CITY^SYN-PRIVATE-REGION^SYN-PRIVATE-ZIP^SYN-PRIVATE-COUNTRY~SYN-PRIVATE-OTHER^^^^^"),
            (13, "SYN-PRIVATE-PHONE^PRN^PH^^1^999^SYN-PRIVATE-NUMBER~^NET^Internet^SYN-PRIVATE@example.invalid"), (16, "SYN-PRIVATE-STATUS"), (24, "Y"), (25, "3"), (29, "20200101"), (30, "Y"));
    private static Hl7Message Apply(DeidentificationWorkflow flow, string text)
    { var facade = new ScriptMessage(Hl7Parser.Parse(text), Token); flow.Apply(facade); return Hl7Parser.Parse(facade.Serialize()); }

    [Fact]
    public void BundledLibraryProducesPlausibleFictionalContactsAndDeterministicDemographics()
    {
        var library = new SyntheticIdentityLibrary(42); var p = library.Get("opaque-patient", Token);
        Assert.Matches("^[A-Z][a-z]+$", p.GivenName); Assert.Matches("^[A-Z][a-z]+$", p.MiddleName); Assert.Matches("^[A-Z][a-z]+$", p.FamilyName);
        Assert.Matches("^[0-9]+ .* Test (Way|Lane|Road|Avenue|Drive|Court)$", p.Street); Assert.Contains("Test", p.Locality);
        Assert.Equal("ZZ", p.Region); Assert.Equal("00000", p.PostalCode); Assert.Equal("USA", p.Country);
        Assert.Matches("^\\+120255501[0-9]{2}$", p.Telephone); Assert.EndsWith("@example.org", p.Email);
        Assert.Contains(p.AdministrativeSex, new[] { "F", "M", "O" }); Assert.Equal(p, new SyntheticIdentityLibrary(42).Get("opaque-patient", Token));
        Assert.Same(p, library.Get("opaque-patient", Token)); Assert.NotEqual(p, new SyntheticIdentityLibrary(43).Get("opaque-patient", Token));
    }
    [Fact]
    public void CompleteIdentitiesRemainDistinctAcrossThousandAllocations()
    {
        var library = new SyntheticIdentityLibrary(42); var profiles = Enumerable.Range(0, 1000).Select(i => library.Get("opaque-" + i, Token)).ToArray();
        Assert.Equal(1000, profiles.Select(p => p.Identifier).Distinct().Count()); Assert.Equal(1000, profiles.Select(p => p.Email).Distinct().Count());
        Assert.Equal(1000, profiles.Select(p => (p.Street, p.Unit)).Distinct().Count());
    }
    [Fact]
    public void DeterministicCollisionProbingNeverReusesACompleteIdentityAndExhaustionIsSafe()
    {
        int Slot(string key) => (int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes("WB-Synthetic-v1:42:" + key))) % 2);
        var keys = Enumerable.Range(0, 10).Select(i => "key-" + i).GroupBy(Slot).First(g => g.Count() > 1).Take(2).ToArray();
        var library = new SyntheticIdentityLibrary(42, 10, 2); var one = library.Get(keys[0], Token); var two = library.Get(keys[1], Token);
        Assert.Equal(Slot(keys[0]), one.Allocation); Assert.Equal((one.Allocation + 1) % 2, two.Allocation);
        Assert.NotEqual(one.Identifier, two.Identifier); var error = Assert.Throws<ScriptRunException>(() => library.Get("third", Token));
        Assert.Equal("TransformationLimit", Assert.Single(error.Diagnostics).Code); Assert.Same(one, library.Get(keys[0], Token));
        var repeat = new SyntheticIdentityLibrary(42, 10, 2); Assert.Equal(one, repeat.Get(keys[0], Token)); Assert.Equal(two, repeat.Get(keys[1], Token));
    }
    [Fact]
    public void LibraryCancellationAndIdentityBudgetDoNotAllocatePartialEntries()
    {
        var library = new SyntheticIdentityLibrary(42, 1); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => library.Get("one", cancel.Token)); var first = library.Get("one", Token);
        Assert.Throws<ScriptRunException>(() => library.Get("two", Token)); Assert.Same(first, library.Get("one", Token));
    }
    [Fact]
    public void FullPatientDemographicsAreIndependentOfOriginalCharacteristicsAndRepeatAcrossMessages()
    {
        var flow = new DeidentificationWorkflow(new(), Token); var a = Apply(flow, Input()); var repeat = Apply(flow, Input(name: "SYN-OTHER-FAMILY^SYN-OTHER-GIVEN^SYN-OTHER-MIDDLE"));
        var independent = Apply(new(new(), Token), Input(birth: "20050101", sex: "F", name: "SYN-DIFFERENT^PERSON^CHARACTERISTICS"));
        foreach (var path in new[] { "PID-3", "PID-5", "PID-7", "PID-8", "PID-11", "PID-13", "PID-16", "PID-24", "PID-25", "PID-30" })
        { Assert.Equal(a.Get(path).Raw, repeat.Get(path).Raw); Assert.Equal(a.Get(path).Raw, independent.Get(path).Raw); }
        Assert.DoesNotContain("SYN-PRIVATE", a.RawText); Assert.DoesNotContain("SYNTHETIC", a.Get("PID-5").Raw!);
        Assert.Equal("N", a.Get("PID-24").Raw); Assert.Equal("1", a.Get("PID-25").Raw); Assert.Equal("N", a.Get("PID-30").Raw); Assert.Equal(Hl7ValueState.Empty, a.Get("PID-29").State);
        Assert.NotEqual(a.Get("PID-3").Raw, Apply(new(new(), Token), Input("SYN-OTHER-AUTH")).Get("PID-3").Raw);
    }
    [Theory]
    [InlineData("AGE^Age^L")] [InlineData("21612-7^Age^LN")] [InlineData("30525-0^Age^LN")]
    public void RecognizedAgeAndDobAgreeAtTheShiftedEncounterDate(string code)
    {
        var after = Apply(new(new(), Token), Input() + $"OBX|1|NM|{code}||99|a^year^UCUM\r");
        var birth = DateOnly.ParseExact(after.Get("PID-7").Raw!, "yyyyMMdd", CultureInfo.InvariantCulture);
        var encounter = DateOnly.ParseExact(after.Get("MSH-7").Raw![..8], "yyyyMMdd", CultureInfo.InvariantCulture);
        var age = encounter.Year - birth.Year - (encounter < birth.AddYears(encounter.Year - birth.Year) ? 1 : 0);
        Assert.Equal(age.ToString(CultureInfo.InvariantCulture), after.Get("OBX-5").Raw); Assert.Equal(code, after.Get("OBX-3").Raw); Assert.Equal("a^year^UCUM", after.Get("OBX-6").Raw);
    }
    [Fact]
    public void SupportedProviderReferencesAndContactProfilesAreCoherentAndDoNotCopySourceNames()
    {
        var provider = "SYN-PROVIDER^SYN-PRIVATE-FAMILY^SYN-PRIVATE-GIVEN^SYN-PRIVATE-MIDDLE^^^^^SYN-AUTH^^^^NPI";
        var input = Input() + Segment("PV1", (1, "1"), (7, provider)) + Segment("ORC", (1, "NW"), (12, provider)) +
            Segment("NK1", (1, "1"), (2, "SYN-PRIVATE-FAMILY^SYN-PRIVATE-GIVEN^SYN-PRIVATE-MIDDLE"), (4, "SYN-PRIVATE-STREET^^SYN-PRIVATE-CITY^SYN-PRIVATE-STATE^SYN-PRIVATE-ZIP"), (5, "SYN-PRIVATE-PHONE"), (15, "F"), (16, "19600101"));
        var flow = new DeidentificationWorkflow(new(), Token); var a = Apply(flow, input); var b = Apply(flow, input);
        Assert.Equal(a.Get("PV1-7").Raw, a.Get("ORC-12").Raw); Assert.Equal(a.Get("NK1-2").Raw, b.Get("NK1-2").Raw);
        Assert.Matches("^[A-Z][a-z]+$", a.Get("PV1-7.2").Raw!); Assert.Equal("PRN", a.Get("PV1-7.13").Raw);
        Assert.DoesNotContain("SYN-PRIVATE", a.RawText); Assert.True(flow.Summary.Categories.Single(c => c.Category == DeidentificationCategory.Providers).Transformed > 0);
    }
    [Theory]
    [InlineData("NK1", 16, 15, 2, 4, 0)] [InlineData("GT1", 8, 9, 3, 5, 2)] [InlineData("IN1", 18, 43, 16, 19, 49)]
    public void ContactGuarantorAndInsuredBirthAndSexAreGeneratedIndependently(string segment, int dob, int sex, int name, int address, int id)
    {
        string Related(string birth, string gender)
        {
            var fields = new List<(int, string)> { (1, "1"), (dob, birth), (sex, gender), (name, "SYN-PRIVATE-FAMILY^SYN-PRIVATE-GIVEN^SYN-PRIVATE-MIDDLE"), (address, "SYN-PRIVATE-STREET^^SYN-PRIVATE-CITY^SYN-PRIVATE-STATE^SYN-PRIVATE-ZIP") };
            if (id > 0) fields.Add((id, "SYN-CONTACT-ID^^^SYN-AUTH^PI"));
            return Input() + Segment(segment, fields.ToArray());
        }
        var first = Apply(new(new(), Token), Related("19700101", "M")); var other = Apply(new(new(), Token), Related("19990101", "F"));
        Assert.Equal(first.Get($"{segment}-{dob}").Raw, other.Get($"{segment}-{dob}").Raw);
        Assert.Equal(first.Get($"{segment}-{sex}").Raw, other.Get($"{segment}-{sex}").Raw);
        Assert.DoesNotContain("SYN-PRIVATE", first.RawText); Assert.Matches("^[0-9]{8}$", first.Get($"{segment}-{dob}").Raw!);
    }
    [Fact]
    public void RepetitionsNullsAndDemographicShapeSurviveWithoutRetainingSourceGeography()
    {
        var text = Input().Replace("SYN-PRIVATE-UNIT", "\"\"", StringComparison.Ordinal); var before = Hl7Parser.Parse(text); var flow = new DeidentificationWorkflow(new(), Token); var after = Apply(flow, text);
        Assert.Equal(Hl7ValueState.ExplicitNull, after.Get("PID-11.2").State); Assert.Equal(before.Segments.SelectMany(s => s.Fields).Select(f => f.Raw.Count(c => c is '^' or '~' or '&')), after.Segments.SelectMany(s => s.Fields).Select(f => f.Raw.Count(c => c is '^' or '~' or '&')));
        Assert.Contains("Test", after.Get("PID-11.3").Raw!); Assert.Equal("00000", after.Get("PID-11.5").Raw); Assert.EndsWith("@example.org", after.Get("PID-13[2].4").Raw!);
        Assert.True(flow.Summary.Categories.Single(c => c.Category == DeidentificationCategory.Addresses).Transformed > 0);
        Assert.True(flow.Summary.Categories.Single(c => c.Category == DeidentificationCategory.Contacts).Transformed > 0);
    }
    [Theory]
    [InlineData(DeidentificationPreset.IdentifierPseudonymizer)] [InlineData(DeidentificationPreset.TimestampShifter)]
    public void NarrowPresetsRetainOriginalDemographicsAndTheirDistinctVersionOnePolicy(DeidentificationPreset preset)
    {
        var flow = new DeidentificationWorkflow(new() { Preset = preset }, Token); var after = Apply(flow, Input());
        Assert.Contains("SYN-PRIVATE-FAMILY", after.Get("PID-5").Raw!); Assert.Equal("M", after.Get("PID-8").Raw); Assert.Equal(1, flow.Summary.Version);
        Assert.Equal(preset == DeidentificationPreset.IdentifierPseudonymizer, after.Get("PID-7").Raw == "19800229");
        Assert.Equal(preset == DeidentificationPreset.TimestampShifter, after.Get("PID-3.1").Raw == "SYN-ID");
    }
}
