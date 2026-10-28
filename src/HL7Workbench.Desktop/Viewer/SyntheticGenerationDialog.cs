using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Core;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

internal sealed class SyntheticGenerationDialog : Window
{
    internal const string SeedHelp = "Controls test-name suffixes, birth dates, ORU values and seed-based identifiers. Same seed and settings repeat output for this generator version. Changing it may change those values; fixed facilities/providers and configured timestamps stay unchanged. Blank uses 42.";
    internal SyntheticGenerationViewModel Model { get; }
    internal Button Generate { get; } = new() { Content = "Generate", IsDefault = true };
    internal Button CancelGeneration { get; } = new() { Content = "Cancel", IsCancel = true };
    private bool dismissAfterCancel;
    internal SyntheticGenerationDialog(DocumentManager documents)
    {
        Model = new(documents, documents.ActiveDocument);
        Title = "Generate Messages"; Width = 560; MinWidth = 500; MaxWidth = 640;
        SizeToContent = SizeToContent.Height; CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var form = new StackPanel { Spacing = 10 };
        void Row(string label, Control control, string? help = null)
        {
            var row = new Grid { ColumnDefinitions = new("135,*") };
            var caption = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetLabeledBy(control, caption);
            if (help is not null)
            {
                foreach (var target in new[] { caption, control })
                {
                    ToolTip.SetTip(target, new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, MaxWidth = 400 });
                    AutomationProperties.SetHelpText(target, help);
                }
                control.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.F1) { ToolTip.SetIsOpen(control, true); e.Handled = true; }
                    else if (e.Key == Key.Escape && ToolTip.GetIsOpen(control)) { ToolTip.SetIsOpen(control, false); e.Handled = true; }
                };
                control.LostFocus += (_, _) => ToolTip.SetIsOpen(control, false);
                control.DetachedFromVisualTree += (_, _) => ToolTip.SetIsOpen(control, false);
            }
            row.Children.Add(caption);
            Grid.SetColumn(control, 1); row.Children.Add(control); form.Children.Add(row);
        }
        var fields = new List<Control>();
        void Input(string label, string initial, Action<string> changed, string? help = null)
        {
            var input = new TextBox { Text = initial }; input.TextChanged += (_, _) => changed(input.Text ?? "");
            fields.Add(input); Row(label, input, help);
        }
        var types = new ComboBox { ItemsSource = Enum.GetValues<SyntheticMessageType>().Select(t => new SyntheticGenerationOptions { MessageType = t }.TypeLabel).ToArray(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        types.SelectionChanged += (_, _) => { if (types.SelectedIndex >= 0) Model.MessageType = (SyntheticMessageType)types.SelectedIndex; };
        fields.Add(types); Row("Message type", types);
        Input("Count (1–1,000)", Model.Count, v => Model.Count = v);
        Input("Seed (optional)", Model.Seed, v => Model.Seed = v, SeedHelp);
        Input("Base timestamp", Model.Timestamp, v => Model.Timestamp = v);
        Input("Identifier prefix", Model.Prefix, v => Model.Prefix = v);
        var destinations = new ComboBox { ItemsSource = Model.HasTarget ? new[] { "New unsaved document", Model.TargetLabel } : new[] { "New unsaved document" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        destinations.SelectionChanged += (_, _) => Model.InsertIntoTarget = destinations.SelectedIndex == 1;
        fields.Add(destinations); Row("Destination", destinations);
        var positions = new ComboBox { ItemsSource = MessageInsertionWorkflow.Choices.Select(c => c.Label).ToArray(), SelectedIndex = 3, HorizontalAlignment = HorizontalAlignment.Stretch };
        positions.SelectionChanged += (_, _) => { if (positions.SelectedIndex >= 0) Model.Position = MessageInsertionWorkflow.Choices[positions.SelectedIndex].Position; };
        Row("Insert at", positions);
        var preview = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 500 };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 500 };
        void Refresh()
        {
            foreach (var field in fields) field.IsEnabled = !Model.IsBusy;
            positions.IsEnabled = !Model.IsBusy && Model.InsertIntoTarget;
            Generate.IsEnabled = Model.CanGenerate; CancelGeneration.Content = Model.IsBusy ? "Cancel generation" : "Cancel";
            preview.Text = Model.Preview; status.Text = Model.Status;
        }
        Model.PropertyChanged += (_, _) => Refresh(); Refresh();
        Generate.Click += async (_, _) =>
        {
            await Model.GenerateAsync();
            if (Model.Result is not null) Close(Model.Result);
            else if (dismissAfterCancel) Close();
        };
        CancelGeneration.Click += (_, _) => { if (Model.IsBusy) { dismissAfterCancel = true; Model.Cancel(); } else Close(); };
        Closing += (_, e) => { if (Model.IsBusy) { e.Cancel = true; dismissAfterCancel = true; Model.Cancel(); } };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { Generate, CancelGeneration } };
        Content = new StackPanel { Margin = new Thickness(18), Spacing = 16, Children = { form, preview, status, buttons } };
    }
}
