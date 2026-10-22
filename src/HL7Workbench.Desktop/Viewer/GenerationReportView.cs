using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.ComponentModel;
using HL7Workbench.Core;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Count-only report associated before output publication; never reads patient/resource values.</summary>
public sealed class GenerationReportView : Grid
{
    private readonly GenerationReportViewModel model;
    private FhirGenerationReport? rendered;
    public SelectableTextBlock Details { get; } = new() { TextWrapping = TextWrapping.Wrap, Focusable = true };
    public TextBlock EmptyState { get; } = new() { Text = "No Generation Report for the selected document. Generate HL7 from Synthetic Patients to create one.", TextWrapping = TextWrapping.Wrap };
    public ScrollViewer ReportScroll { get; }
    public GenerationReportView(GenerationReportViewModel model)
    {
        this.model = model; RowDefinitions = new("Auto,*"); Margin = new(8);
        Children.Add(new TextBlock { Text = "Generation Report — Review required", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 8) });
        var body = new StackPanel { Spacing = 8 }; body.Children.Add(EmptyState); body.Children.Add(Details);
        ReportScroll = new() { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = true };
        SetRow(ReportScroll, 1); Children.Add(ReportScroll);
        AttachedToVisualTree += (_, _) => { model.PropertyChanged += Changed; model.Shown += FocusReport; Refresh(); if (model.FocusRequested) FocusReport(); };
        DetachedFromVisualTree += (_, _) => { model.PropertyChanged -= Changed; model.Shown -= FocusReport; rendered = null; Details.Text = ""; };
        Refresh();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void FocusReport() => Dispatcher.UIThread.Post(() =>
    {
        // An explicit open can precede Dock attaching this projection. Retain that
        // request until attachment; ordinary document changes never request focus.
        if (model.FocusRequested && this.IsAttachedToVisualTree() && IsEffectivelyVisible &&
            (Details.IsVisible ? Details.Focus() : ReportScroll.Focus())) model.FocusHandled();
    }, DispatcherPriority.Loaded);
    private void Refresh()
    {
        var report = model.Report;
        if (!ReferenceEquals(rendered, report)) { ReportScroll.Offset = default; rendered = report; }
        Details.Text = report is null ? "" : Format(report);
        Details.IsVisible = report is not null; EmptyState.IsVisible = report is null;
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
