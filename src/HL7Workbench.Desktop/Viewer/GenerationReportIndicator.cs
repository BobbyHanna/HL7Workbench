using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Generation provenance outside the editable HL7; counts remain those of the original report.</summary>
public sealed class GenerationReportIndicator : Grid
{
    public TextBlock Summary { get; } = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    public Button ViewReport { get; } = new() { Content = "View report", Padding = new(8, 3), Margin = new(8, 0, 0, 0) };

    public GenerationReportIndicator(MessageDocument document)
    {
        ColumnDefinitions = new("*,Auto"); Margin = new(6, 0, 6, 4);
        void Refresh()
        {
            IsVisible = document.GenerationReport is not null || document.SyntheticGenerationReport is not null;
            if (document.SyntheticGenerationReport is { } synthetic)
                Summary.Text = $"Synthetic · {synthetic.Scenario} · {synthetic.Messages:N0} messages · {synthetic.Patients:N0} patients";
            else if (document.GenerationReport is { } report)
                Summary.Text = $"Generated from FHIR R4 · {report.GeneratedMessages:N0} {(report.GeneratedMessages == 1 ? "message" : "messages")} · {report.PatientsIncluded:N0} {(report.PatientsIncluded == 1 ? "patient" : "patients")}";
            else Summary.Text = "";
            ToolTip.SetTip(Summary, Summary.Text);
        }
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        { if (e.PropertyName == nameof(MessageDocument.SyntheticGenerationReport)) Refresh(); }
        AttachedToVisualTree += (_, _) => { document.PropertyChanged += Changed; Refresh(); };
        DetachedFromVisualTree += (_, _) => document.PropertyChanged -= Changed;
        Refresh();
        ToolTip.SetTip(ViewReport, "Review the original generation counts, warnings and mapping limitations.");
        AutomationProperties.SetName(ViewReport, "View Generation Report");
        Children.Add(Summary); SetColumn(ViewReport, 1); Children.Add(ViewReport);
        ViewReport.Click += (_, _) => RaiseEvent(new GenerationReportRequestEventArgs(document));
    }
}
