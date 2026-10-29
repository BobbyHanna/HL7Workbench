using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    internal const string SeedHelp = "Controls synthetic demographics (including birth dates), providers, facilities, ORU values and seed-based identifiers. Leave blank for one new cryptographically random seed on each Generate, for both standalone messages and scenarios. The actual seed appears in Generation Report; use Copy seed and paste here to reproduce the same settings, base time, style and generator version. Explicit 0 and 42 are valid. Select Obvious Placeholders to reproduce original standalone v1 bytes.";
    internal SyntheticGenerationViewModel Model { get; }
    internal Button Generate { get; } = new() { Content = "Generate", IsDefault = true };
    internal Button CancelGeneration { get; } = new() { Content = "Cancel", IsCancel = true };
    internal Expander AdvancedOptions { get; } = new() { Header = "Advanced options", HorizontalAlignment = HorizontalAlignment.Stretch };
    internal ScrollViewer ConfigurationScroll { get; }
    private bool dismissAfterCancel;
    internal SyntheticGenerationDialog(DocumentManager documents)
    {
        Model = new(documents, documents.ActiveDocument);
        Title = "Generate Messages"; Width = 680; MinWidth = 380; MaxWidth = 900; Height = 760; MinHeight = 450;
        CanResize = true; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var form = new StackPanel { Spacing = 10 };
        ResponsiveFormField Row(string label, Control control, string? help = null, StackPanel? parent = null)
        {
            var row = new ResponsiveFormField(label, control);
            var caption = row.Label;
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
            (parent ?? form).Children.Add(row); return row;
        }
        var fields = new List<Control>();
        ResponsiveFormField Input(string label, string initial, Action<string> changed, string? help = null, StackPanel? parent = null)
        {
            var input = new TextBox { Text = initial, MaxLength = 64 }; input.TextChanged += (_, _) => changed(input.Text ?? "");
            fields.Add(input); return Row(label, input, help, parent);
        }
        var modes = new ComboBox { ItemsSource = new[] { "Standalone messages", "Admission to discharge", "Order to result", "Combined encounter" }, SelectedIndex = 0 };
        modes.SelectionChanged += (_, _) => { if (modes.SelectedIndex >= 0) Model.Mode = (SyntheticGenerationMode)modes.SelectedIndex; };
        fields.Add(modes); Row("Generate", modes);
        var types = new ComboBox { ItemsSource = Enum.GetValues<SyntheticMessageType>().Select(t => new SyntheticGenerationOptions { MessageType = t }.TypeLabel).ToArray(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        types.SelectionChanged += (_, _) => { if (types.SelectedIndex >= 0) Model.MessageType = (SyntheticMessageType)types.SelectedIndex; };
        fields.Add(types); var typeRow = Row("Message type", types);
        var countRow = Input("Count (1–1,000)", Model.Count, v => Model.Count = v);
        var demographicStyle = new ComboBox { ItemsSource = new[] { "Realistic Synthetic", "Obvious Placeholders" }, SelectedIndex = 0 };
        demographicStyle.SelectionChanged += (_, _) => { if (demographicStyle.SelectedIndex >= 0) Model.DemographicStyle = (SyntheticDemographicStyle)demographicStyle.SelectedIndex; };
        fields.Add(demographicStyle); Row("Demographic style", demographicStyle,
            "Realistic Synthetic uses fictional American-style profiles and reserved example phones in both modes. Names/addresses can coincide with real people or places. Obvious Placeholders preserves standalone v1 output and previous scenario placeholder bytes. Standalone transport/patient overrides require Realistic Synthetic.");
        var scenarioFields = new StackPanel { Spacing = 6 }; form.Children.Add(scenarioFields);
        Input("Encounters per patient", Model.Encounters, v => Model.Encounters = v, parent: scenarioFields);
        var encounter = new ComboBox { ItemsSource = new[] { "Inpatient admission (A01)", "Outpatient registration (A04)" }, SelectedIndex = 0 };
        encounter.SelectionChanged += (_, _) => { if (encounter.SelectedIndex >= 0) Model.EncounterType = (SyntheticEncounterType)encounter.SelectedIndex; };
        fields.Add(encounter); Row("Encounter", encounter, parent: scenarioFields);
        var transfer = new CheckBox { Content = "Include inpatient transfer (A02)" };
        transfer.IsCheckedChanged += (_, _) => Model.IncludeTransfer = transfer.IsChecked == true;
        fields.Add(transfer); var transferRow = Row("Optional event", transfer, parent: scenarioFields);
        var orderFields = new StackPanel { Spacing = 6 }; scenarioFields.Children.Add(orderFields);
        Input("Orders per encounter", Model.Orders, v => Model.Orders = v, parent: orderFields);
        Input("Reports per order", Model.Results, v => Model.Results = v, "Report updates of the same collected observations. Earlier reports are preliminary; the last uses the configured status. Two numeric OBX observations per report.", orderFields);
        var seedField = Input("Seed (optional)", Model.Seed, v => Model.Seed = v, SeedHelp);
        Input("Base timestamp", Model.Timestamp, v => Model.Timestamp = v);
        Input("Identifier prefix", Model.Prefix, v => Model.Prefix = v);
        var advanced = new StackPanel { Spacing = 6 }; AdvancedOptions.Content = advanced; form.Children.Add(AdvancedOptions);
        const string automaticHelp = "Blank uses the selected style's automatic default. An explicit code takes precedence and stays fixed when seed or demographic style changes.";
        var sendingApp = Input("Sending application", Model.SendingApplication, v => Model.SendingApplication = v, automaticHelp, advanced);
        var sendingFacility = Input("Sending facility", Model.SendingFacility, v => Model.SendingFacility = v, automaticHelp, advanced);
        var receivingApp = Input("Receiving application", Model.ReceivingApplication, v => Model.ReceivingApplication = v, automaticHelp, advanced);
        var receivingFacility = Input("Receiving facility", Model.ReceivingFacility, v => Model.ReceivingFacility = v, automaticHelp, advanced);
        Row("HL7 version", new ComboBox { ItemsSource = new[] { "2.5.1" }, SelectedIndex = 0 }, parent: advanced);
        Input("Patient ID prefix", Model.PatientPrefix, v => Model.PatientPrefix = v, "Optional shared prefix; IDs always retain a SYN marker. Blank uses the identifier prefix.", advanced);
        var advancedOrders = new StackPanel { Spacing = 6 }; advanced.Children.Add(advancedOrders);
        var clinical = new ComboBox { ItemsSource = new[] { "Synthetic chemistry (serum)", "Synthetic blood count (whole blood)" }, SelectedIndex = 0 };
        clinical.SelectionChanged += (_, _) => { if (clinical.SelectedIndex >= 0) Model.ClinicalExample = (SyntheticClinicalExample)clinical.SelectedIndex; };
        fields.Add(clinical); Row("Clinical example", clinical, parent: advancedOrders);
        Input("Local order code", Model.OrderCode, v => Model.OrderCode = v, "Optional local code in 99SYN. It propagates to ORM and ORU; the selected panel and observations stay paired.", advancedOrders);
        var statusChoice = new ComboBox { ItemsSource = new[] { "Final (F)", "Preliminary (P)" }, SelectedIndex = 0 };
        statusChoice.SelectionChanged += (_, _) => Model.ResultStatus = statusChoice.SelectedIndex == 1 ? "P" : "F";
        fields.Add(statusChoice); Row("Last result status", statusChoice, parent: advancedOrders);
        Input("First analyte value", Model.ObservationValue, v => Model.ObservationValue = v, "Optional decimal for synthetic glucose or hemoglobin. Propagates through report updates; flags follow the example reference range. Not for clinical decisions.", advancedOrders);
        var destinations = new ComboBox { ItemsSource = Model.HasTarget ? new[] { "New unsaved document", Model.TargetLabel } : new[] { "New unsaved document" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        destinations.SelectionChanged += (_, _) => Model.InsertIntoTarget = destinations.SelectedIndex == 1;
        fields.Add(destinations); Row("Destination", destinations);
        var positions = new ComboBox { ItemsSource = MessageInsertionWorkflow.Choices.Select(c => c.Label).ToArray(), SelectedIndex = 3, HorizontalAlignment = HorizontalAlignment.Stretch };
        positions.SelectionChanged += (_, _) => { if (positions.SelectedIndex >= 0) Model.Position = MessageInsertionWorkflow.Choices[positions.SelectedIndex].Position; };
        Row("Insert at", positions);
        var preview = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        SyntheticGenerationMode? displayedSeedMode = null;
        void Refresh()
        {
            foreach (var field in fields) field.IsEnabled = !Model.IsBusy;
            AdvancedOptions.IsEnabled = !Model.IsBusy;
            typeRow.IsVisible = !Model.IsScenario; scenarioFields.IsVisible = Model.IsScenario;
            if (displayedSeedMode != Model.Mode)
            { ((TextBox)seedField.Input).Text = Model.Seed; displayedSeedMode = Model.Mode; }
            ((TextBox)seedField.Input).PlaceholderText = "Automatic — new seed on Generate";
            var defaults = new SyntheticScenarioOptions { DemographicStyle = Model.DemographicStyle,
                Seed = uint.TryParse(Model.Seed, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : SyntheticGenerationOptions.DefaultSeed };
            var automatic = string.IsNullOrWhiteSpace(Model.Seed);
            ((TextBox)sendingApp.Input).PlaceholderText = automatic ? "Automatic — resolved on Generate" : defaults.SendingApplication;
            ((TextBox)sendingFacility.Input).PlaceholderText = automatic ? "Automatic — resolved on Generate" : defaults.SendingFacility + " · " + defaults.SendingFacilityName;
            ((TextBox)receivingApp.Input).PlaceholderText = automatic ? "Automatic — resolved on Generate" : defaults.ReceivingApplication;
            ((TextBox)receivingFacility.Input).PlaceholderText = automatic ? "Automatic — resolved on Generate" : defaults.ReceivingFacility + " · " + defaults.ReceivingFacilityName;
            countRow.Label.Text = Model.IsScenario ? "Patients (1–1,000)" : "Count (1–1,000)";
            transferRow.IsVisible = Model.HasAdt;
            orderFields.IsVisible = Model.HasOrders; advancedOrders.IsVisible = Model.HasOrders;
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
        var layout = new Grid { Margin = new Thickness(18), RowDefinitions = new("*,Auto,Auto,Auto"), RowSpacing = 10 };
        ConfigurationScroll = new() { Content = form, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        layout.Children.Add(ConfigurationScroll);
        Grid.SetRow(preview, 1); layout.Children.Add(preview); Grid.SetRow(status, 2); layout.Children.Add(status);
        Grid.SetRow(buttons, 3); layout.Children.Add(buttons); Content = layout;
    }
}
