using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.ComponentModel;
using System.Globalization;
using HL7Workbench.Core;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Count-only report associated before output publication; never reads patient/resource values.</summary>
public sealed class GenerationReportView : Grid
{
    private readonly GenerationReportViewModel model;
    private FhirGenerationReport? rendered;
    private SyntheticGenerationReport? renderedSynthetic;
    public SelectableTextBlock Details { get; } = new() { TextWrapping = TextWrapping.Wrap, Focusable = true };
    public TextBlock EmptyState { get; } = new() { Text = "No Generation Report for the selected document. Use Generate Messages or generate HL7 from FHIR Patient Workspace to create one.", TextWrapping = TextWrapping.Wrap };
    public ScrollViewer ReportScroll { get; }
    public Button CopySeed { get; } = new() { Content = "Copy seed" };
    public TextBlock CopyStatus { get; } = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false };
    internal Task ClipboardTask { get; private set; } = Task.CompletedTask;
    public GenerationReportView(GenerationReportViewModel model)
    {
        this.model = model; RowDefinitions = new("Auto,*"); Margin = new(8);
        Children.Add(new AdaptiveActionBar(new TextBlock { Text = "Generation Report — Review required", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, CopySeed));
        ToolTip.SetTip(CopySeed, "Copy the actual synthetic generation seed. Paste it into Seed with the same settings to reproduce output.");
        Avalonia.Automation.AutomationProperties.SetName(CopySeed, "Copy generation seed");
        CopySeed.Click += (_, _) => ClipboardTask = CopySeedAsync();
        var body = new StackPanel { Spacing = 8 }; body.Children.Add(EmptyState); body.Children.Add(CopyStatus); body.Children.Add(Details);
        ReportScroll = new() { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = true };
        SetRow(ReportScroll, 1); Children.Add(ReportScroll);
        AttachedToVisualTree += (_, _) => { model.PropertyChanged += Changed; model.Shown += FocusReport; Refresh(); if (model.FocusRequested) FocusReport(); };
        DetachedFromVisualTree += (_, _) => { model.PropertyChanged -= Changed; model.Shown -= FocusReport; rendered = null; renderedSynthetic = null; Details.Text = ""; CopySeed.IsEnabled = false; CopyStatus.IsVisible = false; };
        Refresh();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    public async Task CopySeedAsync()
    {
        if (model.SyntheticReport is not { } report) return;
        var text = report.Seed.ToString(CultureInfo.InvariantCulture);
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) throw new NotSupportedException();
            await clipboard.SetTextAsync(text);
            CopyStatus.Text = "Seed copied. Paste into Seed using the same settings to reproduce output.";
        }
        catch (Exception) { CopyStatus.Text = "Clipboard unavailable. Select the seed in the report text to copy it manually."; }
        CopyStatus.IsVisible = true;
    }
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
        var synthetic = model.SyntheticReport;
        if (!ReferenceEquals(rendered, report) || !ReferenceEquals(renderedSynthetic, synthetic))
        { ReportScroll.Offset = default; rendered = report; renderedSynthetic = synthetic; CopyStatus.IsVisible = false; }
        CopySeed.IsVisible = synthetic is not null; CopySeed.IsEnabled = synthetic is not null;
        Details.Text = string.Join("\n\n", new[] { synthetic is null ? "" : Format(synthetic), report is null ? "" : Format(report) }.Where(t => t.Length > 0));
        Details.IsVisible = report is not null || synthetic is not null; EmptyState.IsVisible = !Details.IsVisible;
    }
    public static string Format(SyntheticGenerationReport report) =>
        $"Synthetic HL7 v2.5.1 · {report.Scenario} · generator v{report.GeneratorVersion} · {report.DemographicStyleLabel}\n" +
        $"Seed: {report.Seed.ToString(CultureInfo.InvariantCulture)} ({(report.SeedWasAutomatic ? "automatic" : "explicit")}) · Base timestamp: {report.BaseTimestamp:O}\n" +
        $"Patients: {report.Patients:N0} · Encounters: {report.Encounters:N0} · Orders: {report.Orders:N0}\n" +
        $"Result reports: {report.Results:N0} · OBX observations: {report.Observations:N0} · Total messages: {report.Messages:N0}\n" +
        string.Join(" · ", report.MessageTypes.Select(t => $"{t.Key}: {t.Value:N0}")) + "\n" +
        $"Generation: {report.GenerationTime.TotalMilliseconds:N0} ms · Publication validation: {report.PublicationTime.TotalMilliseconds:N0} ms\n" +
        $"Output: {(report.IsNewDocument ? "new unsaved document" : "existing document insertion")} · Cancellation: no (successful atomic batch).\n" +
        "Warnings: " + (report.Warnings.Count == 0 ? "none" : string.Join(" · ", report.Warnings)) + "\n" + SyntheticGenerationReport.Notice;
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
