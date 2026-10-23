using System.Text.Json;
using HL7Workbench.Core;
using HL7Workbench.Scripting;
using Xunit;

namespace HL7Workbench.Scripting.Tests;

public sealed class DeidentificationReviewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private const string Minimal = "MSH|^~\\&|SYN-PRIVATE-APP|SYN-FAC|DEST|TEST|20300102120000||ADT^A01|SYN-CONTROL|T|2.5.1\rPID|1||SYN-ID^^^SYN-AUTH^MR||SYN-PRIVATE-FAMILY^SYN-PRIVATE-GIVEN\r";
    private static DeidentificationWorkflow Run(string text)
    { var workflow = new DeidentificationWorkflow(new(), Token); workflow.Apply(new ScriptMessage(Hl7Parser.Parse(text), Token)); return workflow; }

    [Fact]
    public void GenericLimitationAndRoutineRetainedFieldsDoNotInventReviewFindings()
    {
        var flow = Run(Minimal);
        Assert.Equal(1, flow.Summary.Messages); Assert.True(flow.Summary.ReplacedValues > 0);
        Assert.Equal(4, flow.Summary.RetainedValues); Assert.Equal(0, flow.Summary.RetainedValuesRequiringReview);
        Assert.Empty(flow.Findings); Assert.False(flow.Summary.RequiresReview);
        Assert.Equal("Completed — no flagged findings", flow.Summary.Status);
        Assert.Contains("not certified", DeidentificationSummary.ScopeNotice);
    }
    [Fact]
    public void NativeCountScenarioExplainsUnqualifiedProviderAndEveryRetainedField()
    {
        var flow = Run(SyntheticMessageGenerator.Generate(new(), Token).Messages[0]); var s = flow.Summary;
        Assert.Equal(28, s.ReplacedValues); Assert.Equal(7, s.SuppressedValues); Assert.Equal(1, s.HighRiskFields); Assert.Equal(8, s.RetainedValues);
        Assert.Equal(3, s.RetainedValuesRequiringReview); Assert.Equal(4, flow.Findings.Count);
        var provider = Assert.Single(flow.Findings, f => f.Reason == DeidentificationReason.UnqualifiedIdentifier);
        Assert.Equal("PV1[1]-7", provider.Path); Assert.Contains("assigning authority", provider.Description);
        Assert.Equal(new[] { "EVN[1]-1", "PV1[1]-2", "PV1[1]-4" }, flow.Findings.Where(f => f.Reason == DeidentificationReason.RetainedClinicalValue).Select(f => f.Path));
        Assert.True(s.RequiresReview); Assert.Equal("Completed — review required", s.Status);
    }
    [Fact]
    public void NumericClinicalValueAndSuppressedNarrativeRemainFlaggedWithoutSourceValues()
    {
        var flow = Run(Minimal + "OBX|1|NM|SYN-CODE||98.6|mg/dL\rNTE|1||SYN-PRIVATE-NARRATIVE\r");
        Assert.True(flow.Summary.RequiresReview);
        Assert.Contains(flow.Findings, f => f.Path == "OBX[1]-5" && f.Reason == DeidentificationReason.RetainedClinicalValue);
        Assert.Contains(flow.Findings, f => f.Path == "NTE[1]-3" && f.Reason == DeidentificationReason.SensitiveContentSuppressed);
        var metadata = JsonSerializer.Serialize(flow.Summary) + JsonSerializer.Serialize(flow.Findings) + string.Join(" ", flow.Findings.SelectMany(f => f.Cells));
        Assert.DoesNotContain("SYN-PRIVATE", metadata); Assert.DoesNotContain("98.6", metadata); Assert.DoesNotContain("SYN-ID", metadata);
    }
    [Fact]
    public void FindingBoundDisclosesOmissionsWithoutChangingExactReviewCounts()
    {
        var flow = Run(Minimal + string.Concat(Enumerable.Repeat("NTE|1||SYN-PRIVATE-NARRATIVE\r", 1010)));
        Assert.Equal(1010, flow.Summary.HighRiskFields); Assert.Equal(1000, flow.Findings.Count); Assert.Equal(10, flow.Summary.OmittedFindings);
        Assert.True(flow.Summary.RequiresReview);
    }
}
