using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Dock-owned UI displaying only validated structural coordinates and fixed reasons.</summary>
public sealed class TransformationReportView : Grid
{
    private readonly DeidentificationReportViewModel model;
    private TransformationReport? rendered;
    public SelectableTextBlock Summary { get; } = new() { TextWrapping = TextWrapping.Wrap, Focusable = true };
    public SelectableTextBlock Provenance { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public TextBlock EmptyState { get; } = new() { Text = "No De-identification Report for the selected document. Run a de-identification preset to create one.", TextWrapping = TextWrapping.Wrap };
    public StackPanel Categories { get; } = new() { Spacing = 3 };
    public StackPanel Findings { get; } = new() { Spacing = 8 };
    public ScrollViewer ReportScroll { get; }
    public TransformationReportView(DeidentificationReportViewModel model)
    {
        this.model = model; RowDefinitions = new("Auto,*"); Margin = new(8);
        Children.Add(new TextBlock { Text = "De-identification Report", FontWeight = FontWeight.SemiBold, Margin = new(0, 0, 0, 8) });
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(EmptyState); body.Children.Add(Summary); body.Children.Add(Provenance); body.Children.Add(Categories); body.Children.Add(Findings);
        ReportScroll = new() { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = true };
        SetRow(ReportScroll, 1); Children.Add(ReportScroll);
        AttachedToVisualTree += (_, _) => { model.PropertyChanged += Changed; model.Shown += FocusReport; Refresh(); if (model.FocusRequested) FocusReport(); };
        DetachedFromVisualTree += (_, _) =>
        {
            model.PropertyChanged -= Changed; model.Shown -= FocusReport;
            rendered = null; Summary.Text = Provenance.Text = ""; Categories.Children.Clear(); Findings.Children.Clear();
        };
        Refresh();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    { if (Dispatcher.UIThread.CheckAccess()) Refresh(); else Dispatcher.UIThread.Post(Refresh); }
    private void FocusReport() => Dispatcher.UIThread.Post(() =>
    {
        if (model.FocusRequested && this.IsAttachedToVisualTree() && IsEffectivelyVisible &&
            (Summary.IsVisible ? Summary.Focus() : ReportScroll.Focus())) model.FocusHandled();
    }, DispatcherPriority.Loaded);
    private void Refresh()
    {
        var report = model.Report;
        var rebuild = !ReferenceEquals(rendered, report);
        if (rebuild) { ReportScroll.Offset = default; rendered = report; }
        EmptyState.IsVisible = report is null; Summary.IsVisible = Provenance.IsVisible = Categories.IsVisible = Findings.IsVisible = report is not null;
        Summary.Text = Provenance.Text = "";
        if (rebuild) { Categories.Children.Clear(); Findings.Children.Clear(); }
        if (report is null) return;
        var s = report.Summary;
        Summary.Text = $"{s.Status}\nMessages processed: {s.Messages:N0} · Values transformed: {s.ReplacedValues:N0} · Suppressed: {s.SuppressedValues:N0}\n" +
            $"High-risk findings: {s.HighRiskFields:N0} · Retained values requiring review: {s.RetainedValuesRequiringReview:N0} of {s.RetainedValues:N0}\n" +
            $"Missing identities: {s.MissingIdentities:N0} · Ambiguous identities: {s.AmbiguousIdentities:N0} · Unknown segments: {s.UnknownSegments:N0} · Unsupported timestamps: {s.UnsupportedTimestamps:N0}";
        Provenance.Text = $"Execution completed · {report.PresetName} · {(report.Scope == ScriptScope.WholeDocument ? "Whole document" : "Current message")} · policy {s.Version}\n" +
            $"Execution {report.ExecutionId:N} · Captured source revision {report.CapturedRevision}. {report.SourceState}\n{report.OutputState}\n" +
            (report.SourceChangedDuringRun ? "Source changed during execution; output uses the captured snapshot.\n" : "") +
            TransformationReport.Warning + " CSV export: Script Editor → Reports / Export CSV (last successful run only).";
        // Immutable details are rebuilt only on a document change, not every editor keystroke.
        if (!rebuild) return;
        Categories.Children.Add(new TextBlock { Text = "Category counts — transformed / suppressed", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        foreach (var c in s.Categories)
        {
            var row = new Grid { ColumnDefinitions = new("*,Auto,Auto") };
            row.Children.Add(new TextBlock { Text = c.Category.ToString(), TextWrapping = TextWrapping.Wrap });
            var transformed = new TextBlock { Text = $"{c.Transformed:N0}", Margin = new(8, 0) }; SetColumn(transformed, 1); row.Children.Add(transformed);
            var suppressed = new TextBlock { Text = $"{c.Suppressed:N0}", Margin = new(8, 0) }; SetColumn(suppressed, 2); row.Children.Add(suppressed);
            Categories.Children.Add(row);
        }
        Findings.Children.Add(new TextBlock { Text = "Specific review findings", FontWeight = FontWeight.SemiBold });
        if (!report.FindingDetailsAvailable) Findings.Children.Add(new TextBlock { Text = "Detailed findings unavailable for this script. Counts remain authoritative; review the captured output.", TextWrapping = TextWrapping.Wrap });
        else if (report.Findings.Count == 0) Findings.Children.Add(new TextBlock { Text = "No specific findings flagged by this policy. General limitations still apply.", TextWrapping = TextWrapping.Wrap });
        foreach (var finding in report.Findings)
            Findings.Children.Add(new SelectableTextBlock { Text = $"Captured source message {report.SourceMessageNumber(finding.MessageNumber)} · {finding.Path}\n{finding.Description}", TextWrapping = TextWrapping.Wrap });
        if (s.OmittedFindings > 0) Findings.Children.Add(new TextBlock { Text = $"{s.OmittedFindings:N0} additional finding occurrences omitted from details (1,000-row bound); all summary counts are retained.", TextWrapping = TextWrapping.Wrap });
    }
}
