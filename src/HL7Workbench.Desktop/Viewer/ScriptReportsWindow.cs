using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Contextual access to the retained run, with no report rows or script source in the shell.</summary>
public sealed class ScriptReportsWindow : Window
{
    private readonly ScriptEditorViewModel model;
    public ListBox Reports { get; } = new() { MinHeight = 90 };
    public TextBlock Origin { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public Button Export { get; } = new() { Content = "Export selected CSV…" };
    public TextBlock Status { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public ScriptReportsWindow(ScriptEditorViewModel model)
    {
        this.model = model; Title = "Reports — last successful run"; Width = 560; Height = 340; MinWidth = 360; MinHeight = 260;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Reports.ItemTemplate = new FuncDataTemplate<ScriptReportInfo>((r, _) => new TextBlock { Text = r is null ? "" : $"{r.Name} · {r.Rows:N0} rows · {r.Bytes:N0} bytes" });
        var close = new Button { Content = "Close", IsCancel = true }; close.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { Export, close } };
        var root = new Grid { Margin = new Thickness(16), RowDefinitions = new("Auto,*,Auto,Auto"), RowSpacing = 12 };
        root.Children.Add(Origin); Grid.SetRow(Reports, 1); root.Children.Add(Reports); Grid.SetRow(Status, 2); root.Children.Add(Status); Grid.SetRow(buttons, 3); root.Children.Add(buttons); Content = root;
        Reports.SelectionChanged += (_, _) => Export.IsEnabled = model.CanExport && Reports.SelectedItem is ScriptReportInfo;
        Export.Click += async (_, _) =>
        {
            if (Reports.SelectedItem is not ScriptReportInfo report) return;
            await model.ChooseAndExportReportDestinationAsync(report.Name, () => SaveFilePicker.ChooseAsync(this, new FilePickerSaveOptions
            { Title = "Export staged CSV report", SuggestedFileName = report.Name + ".csv", DefaultExtension = "csv" }));
        };
        model.PropertyChanged += Changed;
        Closed += (_, _) => model.PropertyChanged -= Changed;
        Refresh();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        Origin.Text = model.ReportOrigin?.Description ?? "No successful reports are available.";
        if (!ReferenceEquals(Reports.ItemsSource, model.Reports)) { Reports.ItemsSource = model.Reports; Reports.SelectedIndex = model.Reports.Count > 0 ? 0 : -1; }
        Reports.IsEnabled = !model.IsExporting; Export.IsEnabled = model.CanExport && Reports.SelectedItem is ScriptReportInfo;
        Status.Text = model.Status;
    }
}
