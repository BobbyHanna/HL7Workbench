using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Compact link to the dockable report; no raw report is embedded in the document.</summary>
public sealed class DeidentificationReportIndicator : Grid
{
    private readonly TransformationReport? report;
    public TextBlock Summary { get; } = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    public Button ViewReport { get; } = new() { Content = "View report", Margin = new(8, 0, 0, 0) };
    public DeidentificationReportIndicator(MessageDocument document)
    {
        report = document.TransformationReport; IsVisible = report is not null; ColumnDefinitions = new("*,Auto"); Margin = new(6, 0, 6, 4);
        Children.Add(Summary); SetColumn(ViewReport, 1); Children.Add(ViewReport);
        Avalonia.Automation.AutomationProperties.SetName(ViewReport, "View De-identification Report");
        ViewReport.Click += (_, _) => RaiseEvent(new DeidentificationReportRequestEventArgs(document)); Refresh();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); if (report is not null) report.PropertyChanged += Changed; Refresh(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { if (report is not null) report.PropertyChanged -= Changed; base.OnDetachedFromVisualTree(e); }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    { if (Dispatcher.UIThread.CheckAccess()) Refresh(); else Dispatcher.UIThread.Post(Refresh); }
    private void Refresh()
    {
        if (report is null) return;
        Summary.Text = $"De-identification · {report.Summary.Status}" + (!report.OutputIsCurrent ? " · Stale output" : "") + (!report.SourceIsCurrent ? " · Source changed or closed" : "");
    }
}
