using System.Text;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class Hl7ProfileImportTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private const string Xml = """
        <HL7v2xConformanceProfile HL7Version="2.5.1" ProfileType="Implementation">
          <MetaData Name="Synthetic HIE" OrgName="Synthetic Organization" Version="1"/>
          <Encodings><Encoding>ER7</Encoding></Encodings>
          <HL7v2xStaticDef MsgType="ADT" EventType="A01" MsgStructID="ADT_A01" Identifier="synthetic.xml">
            <Segment Name="MSH" Usage="R" Min="1" Max="1">
              <Field Name="Separator" Usage="R" Datatype="ST" Length="1"/>
              <Field Name="Encoding" Usage="R" Datatype="ST" Length="4"/>
            </Segment>
            <SegGroup Name="PATIENT" Usage="R" Min="1" Max="*">
              <Segment Name="PID" Usage="R" Min="1" Max="1">
                <Field Name="Set ID" Usage="O" Datatype="SI"/>
                <Field Name="Unused" Usage="X" Datatype="ST"/>
                <Field Name="MRN" Usage="R" Min="1" Max="*" Datatype="CX">
                  <Component Name="ID" Usage="R" Datatype="ST"/>
                </Field>
              </Segment>
              <Segment Name="ZPI" Usage="R" Min="1" Max="1"><Field Name="Local flag" Usage="R" Datatype="ID" ConstantValue="SYN"/></Segment>
            </SegGroup>
          </HL7v2xStaticDef>
        </HL7v2xConformanceProfile>
        """;
    private static Hl7ValidationProfile Child(string id = "synthetic.hie", string parent = "hl7.v251.ADT.A01") => new()
    {
        Id = id, Name = "Synthetic HIE", BaseProfile = parent, MessageType = "ADT", TriggerEvent = "A01", MessageStructure = "ADT_A01",
        TradingPartner = "Synthetic trading partner", Organization = "Synthetic organization",
        Structure = [new() { Key = "ZPI", Segment = "ZPI", Min = 1, After = "PID" }],
        Segments = [new() { Id = "PID", Fields = [new() { Position = 3, Min = 1, Max = null, DataType = "CX", Children = [new() { Position = 4, Min = 1, DataType = "HD" }] }] },
            new() { Id = "PV1", Fields = [new() { Position = 2, Min = 1, DataType = "IS", AllowedValues = ["I"] }] },
            new() { Id = "ZPI", Fields = [new() { Position = 1, Min = 1, AllowedValues = ["SYN"] }] }]
    };

    [Fact]
    public void NativeJsonRoundTripsAndResolvesRequiredAuthorityClassAndPlacedZSegmentWithProvenance()
    {
        var profile = Hl7ProfileImporter.ImportJson(Hl7ProfileImporter.ExportJson(Child()), Token);
        var resolved = Hl7ProfileResolver.Resolve(profile.Id, Hl7ValidationCatalog.BuiltIns.Append(profile));
        Assert.Equal(new[] { "hl7.v251.ADT.A01", profile.Id }, resolved.Lineage); Assert.Null(resolved.Profile.BaseProfile);
        Assert.Equal("ZPI", resolved.Profile.Structure[4].Key); Assert.Equal("PID", resolved.Profile.Structure[3].Key);
        Assert.Equal(profile.Id, resolved.Profile.Segments.Single(s => s.Id == "PID").Fields.Single(f => f.Position == 3).Children.Single().Provenance);
        Assert.NotEqual(profile.Id, resolved.Profile.Segments.Single(s => s.Id == "MSH").Fields[0].Provenance);
        Assert.DoesNotContain(Hl7ValidationCatalog.BuiltIns[0].Structure, r => r.Segment == "ZPI");
        var text = SyntheticMessageGenerator.Generate(new() { DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic }, Token).Messages[0];
        var invalid = Hl7Parser.Parse(text); invalid.SetRaw("PID-3.4", ""); invalid.SetRaw("PV1-2", "O");
        var result = Hl7SchemaValidator.Validate(invalid, resolved.Profile, Token);
        Assert.Contains(result.Diagnostics, d => d.Path == "PID[1]-3[1].4" && d.RuleId == "Value.Required");
        Assert.Contains(result.Diagnostics, d => d.Path == "PV1[1]-2[1]" && d.RuleId == "Value.Allowed");
        Assert.Contains(result.Diagnostics, d => d.RuleId == "Structure.Required" && d.Message.Contains("ZPI", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("unknown-property")] [InlineData("duplicate-property")] [InlineData("bad-version")] [InlineData("null-rules")]
    [InlineData("datatype")] [InlineData("negative-cardinality")] [InlineData("nested-values")]
    public void InvalidOrUnsupportedNativeRulesAreRejected(string mutation)
    {
        var content = Encoding.UTF8.GetString(Hl7ProfileImporter.ExportJson(Child()));
        content = mutation switch
        {
            "unknown-property" => content.Replace("\"formatVersion\": 1", "\"formatVersion\": 1, \"script\": \"not executable\""),
            "duplicate-property" => content.Replace("\"formatVersion\": 1", "\"formatVersion\": 1, \"formatVersion\": 1"),
            "bad-version" => content.Replace("\"formatVersion\": 1", "\"formatVersion\": 99"),
            "null-rules" => content.Replace("\"conditions\": []", "\"conditions\": null"),
            "datatype" => content.Replace("\"dataType\": \"CX\"", "\"dataType\": \"UNSUPPORTED\""),
            "negative-cardinality" => content.Replace("\"min\": 1", "\"min\": -1"),
            _ => "{\"id\":\"synthetic\",\"name\":\"Test\",\"messageType\":\"ADT\",\"triggerEvent\":\"A01\",\"segments\":[{\"id\":\"PID\",\"fields\":[{\"position\":1,\"children\":[{\"position\":1,\"children\":[{\"position\":1,\"children\":[{\"position\":1}]}]}]}]}]}"
        };
        Assert.ThrowsAny<ArgumentException>(() => Hl7ProfileImporter.ImportJson(Encoding.UTF8.GetBytes(content), Token));
    }

    [Fact]
    public void SupportedXmlTranslatesToSameImmutableProfileModelAndHonorsExplicitRules()
    {
        var profile = Hl7ProfileImporter.ImportXml(Encoding.UTF8.GetBytes(Xml), Token);
        Assert.Equal("synthetic.xml", profile.Id); Assert.Equal("Synthetic HIE", profile.Name); Assert.Empty(profile.CoverageWarnings);
        var resolved = Hl7ProfileResolver.Resolve(profile.Id, [profile]); Assert.Equal(2, resolved.Profile.Structure.Count);
        var bytes = Hl7ProfileImporter.ExportJson(profile); var json = Hl7ProfileImporter.ImportJson(bytes, Token);
        Assert.Equal(bytes, Hl7ProfileImporter.ExportJson(json));
        var text = "MSH|^~\\&|SYN|TEST|||20300101000000+0000||ADT^A01^ADT_A01|SYN|T|2.5.1\rPID|||SYN\rZPI|WRONG\r";
        var result = Hl7SchemaValidator.Validate(Hl7Parser.Parse(text), profile, Token);
        Assert.Contains(result.Diagnostics, d => d.RuleId == "Value.Allowed" && d.Path == "ZPI[1]-1[1]");
    }

    [Theory]
    [InlineData("conditional")] [InlineData("predicate")] [InlineData("unknown-attribute")] [InlineData("encoding")]
    [InlineData("wrong-version")] [InlineData("wrong-root")] [InlineData("malformed")] [InlineData("flavors")]
    public void UnsupportedXmlIsRejectedRatherThanSilentlyDroppingRules(string mutation)
    {
        var xml = mutation switch
        {
            "conditional" => Xml.Replace("Usage=\"R\"", "Usage=\"C\""),
            "predicate" => Xml.Replace("</HL7v2xStaticDef>", "<Predicate>unsupported</Predicate></HL7v2xStaticDef>"),
            "unknown-attribute" => Xml.Replace("Length=\"1\"", "Length=\"1\" Regex=\".*\""),
            "encoding" => Xml.Replace("ER7", "XML"),
            "wrong-version" => Xml.Replace("2.5.1", "2.3.1"),
            "wrong-root" => "<ADT_A01><MSH/></ADT_A01>",
            "flavors" => Xml.Replace("</HL7v2xStaticDef>", "<Segment Name=\"PID\"><Field Datatype=\"NM\"/></Segment></HL7v2xStaticDef>"),
            _ => "<HL7v2xConformanceProfile>"
        };
        Assert.ThrowsAny<ArgumentException>(() => Hl7ProfileImporter.ImportXml(Encoding.UTF8.GetBytes(xml), Token));
    }

    [Theory]
    [InlineData("file:///C:/synthetic-secret-do-not-read.txt")] [InlineData("https://example.invalid/entity")]
    public void XmlExternalEntitiesAreProhibitedBeforeAnyExternalAccess(string reference)
    {
        var xml = "<!DOCTYPE HL7v2xConformanceProfile [<!ENTITY attack SYSTEM '" + reference + "'>]>" + Xml.Replace("Synthetic HIE", "&attack;");
        Assert.ThrowsAny<ArgumentException>(() => Hl7ProfileImporter.ImportXml(Encoding.UTF8.GetBytes(xml), Token));
    }

    [Fact]
    public void NotesAndKnowledgeDependentUsageProduceExplicitIncompleteMetadata()
    {
        var xml = Xml.Replace("</HL7v2xStaticDef>", "<ImpNote>Sender-specific narrative rule.</ImpNote></HL7v2xStaticDef>").Replace("Usage=\"O\"", "Usage=\"RE\"");
        var profile = Hl7ProfileImporter.ImportXml(Encoding.UTF8.GetBytes(xml), Token); Assert.Equal(2, profile.CoverageWarnings.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ImportSizeDepthAndCancellationAreBounded(bool xml)
    {
        var oversized = new byte[Hl7ValidationLimits.ProfileBytes + 1];
        Assert.ThrowsAny<ArgumentException>(() => xml ? Hl7ProfileImporter.ImportXml(oversized, Token) : Hl7ProfileImporter.ImportJson(oversized, Token));
        var deep = xml ? string.Concat(Enumerable.Repeat("<x>", 20)) + string.Concat(Enumerable.Repeat("</x>", 20)) : string.Concat(Enumerable.Repeat("[", 20)) + "0" + new string(']', 20);
        Assert.ThrowsAny<ArgumentException>(() => xml ? Hl7ProfileImporter.ImportXml(Encoding.UTF8.GetBytes(deep), Token) : Hl7ProfileImporter.ImportJson(Encoding.UTF8.GetBytes(deep), Token));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => xml ? Hl7ProfileImporter.ImportXml(Encoding.UTF8.GetBytes(Xml), canceled.Token) : Hl7ProfileImporter.ImportJson(Hl7ProfileImporter.ExportJson(Child()), canceled.Token));
    }

    [Theory]
    [InlineData("missing")] [InlineData("cycle")] [InlineData("version")] [InlineData("weaken")] [InlineData("values")] [InlineData("anchor")]
    public void InheritanceDetectsMissingParentsCyclesCompatibilityConflictsAndRelaxation(string mutation)
    {
        var child = Child(); var profiles = Hl7ValidationCatalog.BuiltIns.ToList();
        if (mutation == "missing") child = child with { BaseProfile = "missing" };
        if (mutation == "cycle") { child = child with { BaseProfile = "other" }; profiles.Add(Child("other", child.Id)); }
        if (mutation == "version") child = child with { Hl7Version = "2.5" };
        if (mutation == "weaken") child = child with { Segments = [new() { Id = "PID", Fields = [new() { Position = 3, Min = 0, Max = null, DataType = "CX" }] }] };
        if (mutation == "anchor") child = child with { Structure = [new() { Key = "ZPI", Segment = "ZPI", After = "missing" }] };
        if (mutation == "values") { var basis = Hl7SchemaValidationTests.Profile; profiles.Add(basis); child = Child() with { BaseProfile = basis.Id, Structure = [], Segments = [new() { Id = "PV1", Fields = [new() { Position = 2, Min = 1, DataType = "IS", AllowedValues = ["X"] }] }] }; }
        profiles.Add(child); Assert.ThrowsAny<ArgumentException>(() => Hl7ProfileResolver.Resolve(child.Id, profiles));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DocumentedSyntheticExamplesAreExecutableAgainstTheCompanionMessage(bool xml)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Profiles");
        var bytes = File.ReadAllBytes(Path.Combine(root, xml ? "stage17-synthetic-profile.xml" : "stage17-synthetic-hie.json"));
        var profile = xml ? Hl7ProfileImporter.ImportXml(bytes, Token) : Hl7ProfileImporter.ImportJson(bytes, Token);
        var resolved = Hl7ProfileResolver.Resolve(profile.Id, Hl7ValidationCatalog.BuiltIns.Append(profile));
        var message = Hl7Parser.Parse(File.ReadAllBytes(Path.Combine(root, "stage17-synthetic-message.hl7")));
        var original = Hl7Serializer.Serialize(message); var result = Hl7SchemaValidator.Validate(message, resolved.Profile, Token);
        Assert.True(result.Parsed); Assert.Equal(0, result.Errors); Assert.Equal(original, Hl7Serializer.Serialize(message));
        if (!xml) Assert.False(result.Complete);
    }

    [Theory]
    [InlineData("stray-text")] [InlineData("misplaced-component")] [InlineData("subcomponent-depth")]
    public void XmlCannotHideUntranslatedConstraintsInTextOrWrongChildLevels(string mutation)
    {
        var xml = mutation switch
        {
            "stray-text" => Xml.Replace("</Segment>", "Unstructured predicate text</Segment>"),
            "misplaced-component" => Xml.Replace("<Component Name=\"ID\" Usage=\"R\" Datatype=\"ST\"/>", "<SubComponent Name=\"ID\" Usage=\"R\" Datatype=\"ST\"/>"),
            _ => Xml.Replace("<Component Name=\"ID\" Usage=\"R\" Datatype=\"ST\"/>", "<Component Name=\"ID\" Usage=\"R\" Datatype=\"HD\"><SubComponent Datatype=\"ST\"><Component Datatype=\"ST\"/></SubComponent></Component>")
        };
        Assert.ThrowsAny<ArgumentException>(() => Hl7ProfileImporter.ImportXml(Encoding.UTF8.GetBytes(xml), Token));
    }

    [Fact]
    public void Utf8BomIsAcceptedButInheritedClosedFieldsCannotBeOpened()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Hl7ProfileImporter.ExportJson(Child())).ToArray();
        Assert.Equal("synthetic.hie", Hl7ProfileImporter.ImportJson(bytes, Token).Id);
        var profile = Child() with { Segments = [new() { Id = "PID", Fields = [new() { Position = 100, DataType = "ST" }] }] };
        Assert.ThrowsAny<ArgumentException>(() => Hl7ProfileResolver.Resolve(profile.Id, Hl7ValidationCatalog.BuiltIns.Append(profile)));
    }

    [Fact]
    public void ImpossibleComponentRepetitionIsRejectedInsteadOfSilentlyBecomingAnExistenceCheck()
    {
        var profile = Child() with { Segments = [new() { Id = "PID", Fields = [new() { Position = 3, Min = 1, Max = null, DataType = "CX",
            Children = [new() { Position = 1, Min = 2, Max = 3, DataType = "ST" }] }] }] };
        Assert.ThrowsAny<ArgumentException>(() => Hl7ValidationLimits.Freeze(profile));
        profile = Child() with { Segments = [new() { Id = "PID", Fields = [new() { Position = 3, Min = 1, Max = null, DataType = "CX",
            Children = [new() { Position = 4, DataType = "HD", Children = [new() { Position = 1, Min = 1, Max = null }] }] }] }] };
        Assert.ThrowsAny<ArgumentException>(() => Hl7ValidationLimits.Freeze(profile));
    }
}
