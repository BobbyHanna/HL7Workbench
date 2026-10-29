using System.Text;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class Hl7SchemaValidationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private const string Header = "MSH|^~\\&|SYN|TEST|LAB|TEST|20300101000000+0000||ADT^A01^ADT_A01|SYN-1|T|2.5.1\r";
    private const string Patient = "PID|||SYN-MR^^^SYN-FAC^MR||EXAMPLE^PERSON\r";
    private const string Visit = "PV1||I\r";
    internal static Hl7ValidationProfile Profile => Hl7ValidationLimits.Freeze(new()
    {
        Id = "test.adt", Name = "Synthetic test profile", MessageType = "ADT", TriggerEvent = "A01", MessageStructure = "ADT_A01",
        Structure = [new() { Key = "MSH", Segment = "MSH", Min = 1 }, new() { Key = "PID", Segment = "PID", Min = 1 }, new() { Key = "PV1", Segment = "PV1", Min = 1 }],
        Segments = [new() { Id = "MSH", AllowExtraFields = true, Fields = [new() { Position = 9, Min = 1, DataType = "MSG", Children = [
            new() { Position = 1, DataType = "ID", AllowedValues = ["ADT"] }, new() { Position = 2, DataType = "ID", AllowedValues = ["A01"] }, new() { Position = 3, DataType = "ID", AllowedValues = ["ADT_A01"] }] }, new() { Position = 10, Min = 1, MaxLength = 20 }] },
            new() { Id = "PID", AllowExtraFields = true, Fields = [new() { Position = 3, Min = 1, Max = null, DataType = "CX", Children = [new() { Position = 1, Min = 1 }, new() { Position = 4, Min = 1, DataType = "HD", Children = [new() { Position = 1, Min = 1 }] },
                new() { Position = 5, DataType = "ID", AllowedValues = ["MR"] }] }] },
            new() { Id = "PV1", AllowExtraFields = true, Fields = [new() { Position = 2, Min = 1, DataType = "IS", Table = "test-class", AllowedValues = ["I", "O"] }] }]
    });
    private static Hl7SchemaValidationResult Validate(string text, Hl7ValidationProfile? profile = null, Hl7SchemaValidationOptions? options = null) =>
        Hl7SchemaValidator.Validate(Hl7Parser.Parse(text), profile ?? Profile, Token, options);

    [Theory]
    [InlineData(SyntheticMessageType.AdtA01, false)] [InlineData(SyntheticMessageType.AdtA04, false)] [InlineData(SyntheticMessageType.AdtA08, false)]
    [InlineData(SyntheticMessageType.OrmO01, false)] [InlineData(SyntheticMessageType.OruR01, false)]
    [InlineData(SyntheticMessageType.AdtA01, true)] [InlineData(SyntheticMessageType.AdtA04, true)] [InlineData(SyntheticMessageType.AdtA08, true)]
    [InlineData(SyntheticMessageType.OrmO01, true)] [InlineData(SyntheticMessageType.OruR01, true)]
    public void BuiltInDefinitionsValidateGeneratedStructureAndRetainLegitimateLegacyLengthFindings(SyntheticMessageType type, bool placeholders)
    {
        var batch = SyntheticMessageGenerator.Generate(new() { MessageType = type, Count = 2, DemographicStyle = placeholders ? SyntheticDemographicStyle.ObviousPlaceholders : SyntheticDemographicStyle.RealisticSynthetic }, Token);
        foreach (var text in batch.Messages)
        {
            var message = Hl7Parser.Parse(Encoding.UTF8.GetBytes(text)); var original = Hl7Serializer.Serialize(message);
            var profile = Assert.Single(Hl7ValidationCatalog.Detect(message.Get("MSH-9.1").Raw!, message.Get("MSH-9.2").Raw!, "2.5.1", message.Get("MSH-9.3").Raw));
            var result = Hl7SchemaValidator.Validate(message, profile, Token);
            Assert.True(result.Parsed); Assert.False(result.Complete);
            // Stage 16 never claimed schema conformance. Preserve its golden
            // output: v1 MSH-10 is 21 characters and its order EI values, also
            // retained by v2, can exceed these v2.5.1 field declarations.
            Assert.Equal((placeholders ? 1 : 0) + (type is SyntheticMessageType.OrmO01 or SyntheticMessageType.OruR01 ? 4 : 0), result.Errors);
            Assert.All(result.Diagnostics.Where(d => d.Severity == Hl7DiagnosticSeverity.Error), d => Assert.Equal("Value.Length", d.RuleId));
            Assert.Contains(result.Diagnostics, d => d.RuleId == "Coverage.Incomplete"); Assert.Equal(original, Hl7Serializer.Serialize(message));
        }
    }

    [Fact]
    public void BuiltInCatalogContainsSevenTriggersFiveStructuresAndFullSegmentFieldDeclarations()
    {
        Assert.Equal(7, Hl7ValidationCatalog.BuiltIns.Count); Assert.Equal(5, Hl7ValidationCatalog.BuiltIns.Select(p => p.MessageStructure).Distinct().Count());
        Assert.Empty(Hl7ValidationCatalog.Detect("ADT", "A01", "2.3")); Assert.Empty(Hl7ValidationCatalog.Detect("ADT", "A01", "2.5.1", "ADT_A03"));
        var a01 = Hl7ValidationCatalog.BuiltIns[0]; Assert.Equal(39, a01.Segments.Count);
        Assert.Equal(39, a01.Segments.Single(s => s.Id == "PID").Fields.Count); Assert.Equal(21, a01.Segments.Single(s => s.Id == "MSH").Fields.Count);
        Assert.Contains(a01.Structure, s => s.Children.Count > 0); Assert.All(a01.Structure, s => Assert.NotEmpty(s.Provenance));
        Assert.Throws<NotSupportedException>(() => ((IList<Hl7ValidationProfile>)Hl7ValidationCatalog.BuiltIns).Clear());
    }

    [Fact]
    public void CustomCompleteProfileReturnsNoIssuesAndNeverChangesSource()
    {
        var message = Hl7Parser.Parse(Encoding.UTF8.GetBytes(Header + Patient + Visit)); var before = Hl7Serializer.Serialize(message);
        var result = Hl7SchemaValidator.Validate(message, Profile, Token); Assert.True(result.Parsed); Assert.True(result.Complete); Assert.Empty(result.Diagnostics);
        Assert.Equal(before, Hl7Serializer.Serialize(message)); Assert.False(message.IsModified);
    }

    [Theory]
    [InlineData("missing-segment", "Structure.Required")] [InlineData("order", "Structure.Order")] [InlineData("repeated", "Structure.Cardinality")]
    [InlineData("missing-field", "Value.Required")] [InlineData("component", "Value.Required")] [InlineData("coded", "Value.Allowed")]
    [InlineData("prohibited", "Value.Prohibited")] [InlineData("repetition", "Value.Cardinality")]
    public void ProfileRulesProduceStableExactDiagnosticSpans(string mutation, string rule)
    {
        var profile = Profile; var text = Header + Patient + Visit;
        switch (mutation)
        {
            case "missing-segment": text = Header + Patient; break;
            case "order": text = Header + Visit + Patient; break;
            case "repeated": text += Visit; break;
            case "missing-field": text = Header + Patient + "PV1|\r"; break;
            case "component": text = Header + Patient.Replace("SYN-FAC", "") + Visit; break;
            case "coded": text = Header + Patient + "PV1||X\r"; break;
            case "prohibited": profile = profile with { Segments = profile.Segments.Select(s => s.Id == "PV1" ? s with { Fields = [new() { Position = 2, Max = 0 }] } : s).ToArray() }; break;
            case "repetition": text = Header + Patient + "PV1||I~O\r"; break;
        }
        var result = Validate(text, profile); Assert.Contains(result.Diagnostics, d => d.RuleId == rule);
        Assert.Equal(result.Diagnostics, Validate(text, profile).Diagnostics);
        Assert.All(result.Diagnostics, d => { Assert.InRange(d.Range.Offset, 0, text.Length); Assert.InRange(d.Range.End, d.Range.Offset, text.Length); Assert.Equal(profile.Id, d.ProfileId); Assert.NotEmpty(d.Provenance); });
        if (mutation == "coded")
        {
            var issue = Assert.Single(result.Diagnostics); Assert.Equal("X", text.Substring((int)issue.Range.Offset, (int)issue.Range.Length)); Assert.Equal("PV1[1]-2[1]", issue.Path);
        }
        if (mutation == "missing-field") Assert.Contains(result.Diagnostics, d => d.RuleId == rule && d.IsBoundary && d.Range.Length == 0);
    }

    [Theory]
    [InlineData("NM", "-12.5", true)] [InlineData("NM", "1.2.3", false)] [InlineData("SI", "1e3", false)]
    [InlineData("DT", "20300229", false)] [InlineData("DT", "20320229", true)] [InlineData("DTM", "20300101235959.1234+0530", true)]
    [InlineData("DTM", "203013", false)] [InlineData("TM", "240000", false)] [InlineData("TM", "2359-0500", true)]
    public void SupportedPrimitiveSyntaxUsesBoundedInvariantRules(string type, string value, bool valid)
    {
        var profile = Profile with { Segments = Profile.Segments.Select(s => s.Id == "PV1" ? s with { Fields = [new() { Position = 2, DataType = type }] } : s).ToArray() };
        var result = Validate(Header + Patient + "PV1||" + value + "\r", profile); Assert.Equal(valid ? 0 : 1, result.Errors);
    }

    [Theory]
    [InlineData("\r")] [InlineData("\n")] [InlineData("\r\n")]
    public void SourceMapHandlesRepeatedSegmentsEmptyAndMissingElementsAndLineEndings(string terminator)
    {
        var text = (Header + Patient + Patient.Replace("SYN-MR", "SYN-SECOND") + Visit).Replace("\r", terminator);
        var structure = Hl7StructureScanner.Scan(new StringHl7TextSource(text), Encoding.UTF8, cancellationToken: Token);
        foreach (var (path, expected) in new[] { ("MSH-1", "|"), ("MSH-2", "^~\\&"), ("MSH-9.2", "A01"), ("PID[2]-3.1", "SYN-SECOND"), ("PV1-2", "I"), ("PID-2", "") })
        {
            var location = Hl7ValidationSourceMap.Locate(structure, Hl7Path.Parse(path));
            Assert.Equal(expected, text.Substring((int)location.Range.Offset, (int)location.Range.Length)); Assert.Equal(expected.Length == 0, location.IsBoundary);
        }
        var missing = Hl7ValidationSourceMap.Locate(structure, Hl7Path.Parse("PV1-19.4")); Assert.Equal(0, missing.Range.Length); Assert.True(missing.IsBoundary);
        Assert.Equal(text.Length - terminator.Length, missing.Range.Offset);
    }

    [Fact]
    public void CustomSeparatorsAndEscapedLiteralsShareTheExistingParserCoordinates()
    {
        var text = (Header + Patient + Visit).Replace('|', '*').Replace('^', '%').Replace('~', '$').Replace('\\', '!').Replace('&', '?');
        var profile = Profile; var result = Validate(text, profile); Assert.Empty(result.Diagnostics);
        var escaped = Header + Patient.Replace("SYN-MR", "SYN\\F\\ID") + Visit;
        var parsed = Hl7Parser.Parse(escaped); var structure = Hl7ValidationSourceMap.FromMessage(parsed);
        var location = Hl7ValidationSourceMap.Locate(structure, Hl7Path.Parse("PID-3.1")); Assert.Equal("SYN\\F\\ID", escaped.Substring((int)location.Range.Offset, (int)location.Range.Length));
        Assert.Empty(Hl7SchemaValidator.Validate(parsed, Profile, Token).Diagnostics);
    }

    [Fact]
    public void GroupsConditionsUnknownExtensionsAndSeverityAreProfileDriven()
    {
        var profile = Profile with
        {
            Structure = [.. Profile.Structure, new() { Key = "extra", Min = 0, Max = 2, Children = [new() { Key = "ZPI", Segment = "ZPI", Min = 1 }] }],
            Segments = [.. Profile.Segments, new() { Id = "ZPI", Fields = [new() { Position = 1, Min = 1 }] }],
            Conditions = [new() { Id = "outpatient-extra", IfPath = "PV1-2", EqualsValue = "O", ThenPath = "ZPI-1", Severity = Hl7DiagnosticSeverity.Warning }]
        };
        var missing = Validate(Header + Patient + "PV1||O\r", profile); Assert.Equal(1, missing.Warnings); Assert.Equal(0, missing.Errors);
        Assert.Equal("Condition.outpatient-extra", Assert.Single(missing.Diagnostics).RuleId);
        Assert.Empty(Validate(Header + Patient + "PV1||O\rZPI|SYN-LOCAL\r", profile).Diagnostics);
        Assert.Contains(Validate(Header + Patient + Visit + "ZPI|1\rZPI|2\rZPI|3\r", profile).Diagnostics, d => d.RuleId == "Structure.Cardinality");
        var unknown = Validate(Header + Patient + Visit + "ZXX|SYN\r"); Assert.False(unknown.Complete); Assert.Equal(1, unknown.Warnings);
        var rejected = Validate(Header + Patient + Visit + "ZXX|SYN\r", Profile with { UnknownZSegments = Hl7UnknownSegmentPolicy.Reject }); Assert.Equal(1, rejected.Errors);
    }

    [Theory]
    [InlineData("MSH|^~\\&|SYN\rPID|1\r")] [InlineData("PID|1\r")] [InlineData("MSH|^~\\&|SYN\rBROKEN\r")]
    public void MalformedOrIncompatibleMessagesNeverReceiveQualifiedSuccess(string text)
    {
        var result = Validate(text); Assert.False(result.Complete); Assert.NotEmpty(result.Diagnostics);
        if (text.StartsWith("PID", StringComparison.Ordinal) || text.Contains("BROKEN", StringComparison.Ordinal))
        { Assert.False(result.Parsed); Assert.Contains(result.Diagnostics, d => d.IsParsingDiagnostic); }
    }

    [Fact]
    public void DiagnosticWorkAndLargeValueBudgetsAreExplicitAndCancellationThrows()
    {
        var result = Validate(Header + Patient + "PV1||X\rPV1||Y\r", options: new() { MaximumDiagnostics = 1 });
        Assert.False(result.Complete); Assert.Equal("Budget.Incomplete", Assert.Single(result.Diagnostics).RuleId);
        Assert.False(Validate(Header + Patient + Visit, options: new() { MaximumSteps = 1 }).Complete);
        var large = Validate(Header + Patient.Replace("SYN-MR", new string('A', 5000)) + Visit); Assert.False(large.Complete);
        Assert.Contains(large.Diagnostics, d => d.RuleId == "Coverage.Incomplete");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => Hl7SchemaValidator.Validate(Hl7Parser.Parse(Header + Patient + Visit), Profile, cancellation.Token));
    }

    [Fact]
    public void ExplicitNullIsStillProhibitedContentAndHasItsExactTwoCharacterSpan()
    {
        var profile = Profile with { Segments = Profile.Segments.Select(s => s.Id == "PV1" ? s with { Fields = [new() { Position = 2, Max = 0 }] } : s).ToArray() };
        var text = Header + Patient + "PV1||\"\"\r"; var result = Validate(text, profile);
        var diagnostic = Assert.Single(result.Diagnostics, d => d.RuleId == "Value.Prohibited"); Assert.Equal("\"\"", text.Substring((int)diagnostic.Range.Offset, (int)diagnostic.Range.Length));
    }

    [Fact]
    public void NonclinicalOrderVariantsRequireAnAppropriateProfileInsteadOfInventedStructureFailures()
    {
        var text = SyntheticMessageGenerator.Generate(new() { MessageType = SyntheticMessageType.OrmO01, DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic }, Token).Messages[0]
            .Replace("\rOBR|", "\rRQD|") + "RQ1|SYN-SUPPLY\r";
        var profile = Hl7ValidationCatalog.BuiltIns.Single(p => p.MessageType == "ORM"); var result = Hl7SchemaValidator.Validate(Hl7Parser.Parse(text), profile, Token);
        Assert.True(result.Parsed); Assert.False(result.Complete); Assert.Equal(0, result.Errors);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("outside this profile", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, d => d.RuleId.StartsWith("Structure.", StringComparison.Ordinal));
    }
}
