using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Core;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Count-only report associated before output publication; never reads patient/resource values.</summary>
public sealed class GenerationReportView : Expander
{
    public TextBlock Details { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public GenerationReportView(FhirGenerationReport report)
    {
        Header = "Generation Report — Review required"; IsExpanded = true; HorizontalAlignment = HorizontalAlignment.Stretch;
        Details.Text = Format(report); Content = new ScrollViewer { MaxHeight = 180, Content = Details, Margin = new(8) };
    }
    public static string Format(FhirGenerationReport report)
    {
        var imported = report.Imported;
        int Count(FhirMappingWarning warning) => report.Warnings.GetValueOrDefault(warning);
        var size = report.CharacterMeasurementComplete ? $"{report.MeasuredCharacters:N0} UTF-16 characters" :
            report.MeasuredCharacters > 0 ? $"at least {report.MeasuredCharacters:N0} UTF-16 characters (measurement stopped at a limit)" : "not fully measured because a generation limit blocks output";
        var limits = report.Limits.Count == 0 ? "within output ceilings" : string.Join(", ", report.Limits.Select(l => l switch
        {
            FhirGenerationLimit.MessageCount => "1,000-message ceiling exceeded",
            FhirGenerationLimit.TotalCharacters => "4 Mi UTF-16 total ceiling exceeded",
            FhirGenerationLimit.MessageCharacters => "1 Mi UTF-16 message ceiling exceeded",
            FhirGenerationLimit.SegmentCharacters => "16 Ki segment ceiling exceeded",
            _ => "mapped field ceiling exceeded"
        }));
        return $"FHIR R4 → HL7 v2.5.1 · mapping {report.MappingVersion} · seed {report.Seed}\n" +
            $"Scope: {FhirGenerationReport.ScopeLabel(report.Scope)} · Output: Combined document. {report.PatientsIncluded:N0} patients; {report.EncountersConsidered:N0} encounters considered.\n" +
            $"Captured session {report.SessionId:N} · source fingerprint {report.SourceFingerprint}.\n" +
            $"Generated transport timestamp: {report.TransportTimestamp:O} (not a missing clinical date).\n" +
            $"Imported: {imported.Files:N0} files; {imported.ResourceOccurrences:N0} resource occurrences; {imported.Resources:N0} unique resources; {imported.Patients:N0} patients. " +
            string.Join(" · ", imported.Counts.Select(c => $"{c.Key}: {c.Value:N0}")) + "\n" +
            $"{(report.IsPreview ? "Eligible / estimated output" : "Successfully generated")}: {report.Messages:N0} messages; A04 {report.AdtA04:N0}, A01 {report.AdtA01:N0}, A03 {report.AdtA03:N0}, ORU R01 {report.OruR01:N0}; {report.Observations:N0} OBX results.\n" +
            $"Output size: {size}; {limits}. No truncation or partial publication.\n" +
            $"Scoped exclusions: {report.SkippedResources:N0} skipped clinical/unsupported resources; {report.IneligibleEvents:N0} ineligible ADT events. " +
            "ADT reasons: " + (report.IneligibleReasons.Count == 0 ? "none" : string.Join(" · ", report.IneligibleReasons.Select(r => $"{r.Key}: {r.Value:N0}"))) + ". " +
            $"Unsupported mapping occurrences: values {Count(FhirMappingWarning.UnsupportedValue):N0}, codes/units {Count(FhirMappingWarning.UnsupportedCodeSystem):N0}, statuses {Count(FhirMappingWarning.UnsupportedStatus):N0}, browse-only/events {Count(FhirMappingWarning.UnsupportedEvent):N0}. " +
            $"Missing/ambiguous/conflicting relationship occurrences {Count(FhirMappingWarning.UnresolvedOrConflictingLink):N0}; unassociated clinical resources {Count(FhirMappingWarning.UnassociatedClinicalResource):N0}; unsupported resource types {Count(FhirMappingWarning.UnsupportedResourceType):N0}. " +
            "Warning occurrences can overlap; ineligible ADT events are counted separately from skipped resources. Failed/canceled operations publish no document/report; errors are shown separately in operation status.\n" +
            $"Import references: unresolved {imported.UnresolvedReferences:N0}, ambiguous {imported.AmbiguousReferences:N0}, conflicting {imported.ConflictingReferences:N0}, unsupported/unprojected {imported.UnsupportedReferences:N0}; ambiguous aliases {imported.AmbiguousAliases:N0}. " +
            $"Unsupported resources {imported.UnsupportedResources:N0}; unassociated clinical resources {imported.UnassociatedClinicalResources:N0}; deduplicated {imported.DeduplicatedResources:N0}.\n" +
            $"Dates: missing {imported.MissingTimestamps:N0}, invalid/unsupported precision {imported.InvalidTimestamps:N0}, inconsistent periods {imported.InconsistentPeriods:N0}.\n" +
            "Selected mapping warnings: " + (report.Warnings.Count == 0 ? "none" : string.Join(" · ", report.Warnings.Select(w => $"{w.Key}: {w.Value:N0}"))) + "\n" + FhirGenerationReport.Notice;
    }
}
