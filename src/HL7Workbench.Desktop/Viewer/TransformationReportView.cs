using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Direct document-associated access to the existing journal's safe summary.
/// Mounted views subscribe only while attached; the document owns the report lifetime.</summary>
public sealed class TransformationReportView : Expander
{
    private readonly TransformationReport report;
    public TextBlock Details { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public TransformationReportView(TransformationReport report)
    {
        this.report = report; Header = "Transformation Report — Review required"; IsExpanded = true;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        Content = new ScrollViewer { MaxHeight = 180, Content = Details, Margin = new Thickness(8) };
        Refresh();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); report.PropertyChanged += Changed; Refresh(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { report.PropertyChanged -= Changed; base.OnDetachedFromVisualTree(e); }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    { if (Dispatcher.UIThread.CheckAccess()) Refresh(); else Dispatcher.UIThread.Post(Refresh); }
    private void Refresh()
    {
        var summary = report.Summary;
        Details.Text = $"Review required · {report.PresetName} · {(report.Scope == ScriptScope.WholeDocument ? "Whole document" : "Current message")} · policy {summary.Version}\n" +
            $"Captured source revision {report.CapturedRevision}. {report.SourceState}\n" +
            (report.SourceChangedDuringRun ? "Source changed during execution; output uses the captured snapshot.\n" : "") +
            $"Messages processed: {summary.Messages:N0}. Transformed values: {summary.ReplacedValues:N0}; suppressed: {summary.SuppressedValues:N0}.\n" +
            string.Join(" · ", summary.Categories.Select(c => $"{c.Category}: {c.Transformed:N0} transformed, {c.Suppressed:N0} suppressed")) + "\n" +
            $"Missing identities: {summary.MissingIdentities:N0}; ambiguous: {summary.AmbiguousIdentities:N0}; unknown segments: {summary.UnknownSegments:N0}; unsupported timestamps: {summary.UnsupportedTimestamps:N0}; high-risk fields: {summary.HighRiskFields:N0}; retained values: {summary.RetainedValues:N0}.\n" +
            TransformationReport.Warning + " This report describes the producing run; later output edits are not reassessed. CSV export remains available in Script Editor → Reports for the last successful run.";
    }
}
