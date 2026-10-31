using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Core;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop.Viewer;

public sealed partial class SyntheticPatientsView
{
    public Button Import { get; } = new() { Content = "Import FHIR ▾" };
    public Button EmptyImport { get; } = new() { Content = "Import FHIR" };
    public Button More { get; } = new() { Content = "More actions ▾" };
    public TextBlock SessionSummary { get; } = Text();
    public TextBlock EmptyTitle { get; } = new() { Text = "No patients loaded", [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.18"), FontWeight = FontWeight.SemiBold };
    public StackPanel EmptyState { get; } = new() { Spacing = ToolLayoutMetrics.SectionGap, VerticalAlignment = VerticalAlignment.Top, Margin = new(4, 16) };
    public TextBlock PatientEmpty { get; } = Text();
    public TextBlock SelectionPrompt { get; } = new() { Text = "Select a patient to inspect demographics, encounters and clinical timeline.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 12) };
    public SelectableTextBlock Overview { get; } = new() { TextWrapping = TextWrapping.Wrap, Margin = new(4, 10) };
    public TextBlock EncounterEmpty { get; } = new() { Text = "No encounters for this patient.", TextWrapping = TextWrapping.Wrap };
    public TextBlock TimelineEmpty { get; } = new() { Text = "No clinical timeline entries for this patient.", TextWrapping = TextWrapping.Wrap };
    public ToolContentGrid WorkspaceBody { get; } = new();
    public ToolContentGrid PatientBrowser { get; } = new() { RowDefinitions = new("Auto,*,Auto"), RowSpacing = ToolLayoutMetrics.Gap };
    public ToolContentGrid DetailPane { get; } = new() { RowDefinitions = new("Auto,Auto,*,Auto"), RowSpacing = ToolLayoutMetrics.Gap };
    public Expander GenerationPanel { get; } = new() { Header = "Generate HL7", HorizontalAlignment = HorizontalAlignment.Stretch };
    public Expander AdvancedOptions { get; } = new() { Header = "Advanced Options", HorizontalAlignment = HorizontalAlignment.Stretch };
    public ResponsiveFormField SeedField { get; }
    internal const string SeedHelp = "Controls generated message-control IDs (MSH-10). The same seed, source and settings produce repeatable IDs. Changing the seed may change transport IDs; patient/visit IDs, clinical values and event order remain unchanged.";
    public Expander PreviewPanel { get; } = new() { Header = "Generation preview and limitations", HorizontalAlignment = HorizontalAlignment.Stretch };
    public Expander ImportPanel { get; } = new() { Header = "Import summary and limitations", HorizontalAlignment = HorizontalAlignment.Stretch };
    public Expander ClinicalPanel { get; } = new() { Header = "Selected clinical details", HorizontalAlignment = HorizontalAlignment.Stretch, IsExpanded = true };
    public bool IsStacked => stacked == true;
    private bool? stacked;
    private readonly GridSplitter patientSplitter = new() { ResizeBehavior = GridResizeBehavior.PreviousAndNext, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    private static TextBlock Text() => new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };

    public SyntheticPatientsView(SyntheticPatientsViewModel model)
    {
        this.model = model; Margin = new(8); RowDefinitions = new("Auto,*,Auto,Auto"); RowSpacing = ToolLayoutMetrics.Gap;
        AutomationProperties.SetName(this, "FHIR Patient Workspace");
        var importChoices = new StackPanel { Spacing = ToolLayoutMetrics.Gap, Children = { ImportFiles, ImportFolder } };
        Import.Flyout = new Flyout { Content = importChoices };
        EmptyImport.Click += (_, _) => Import.Flyout.ShowAt(EmptyImport);
        ImportFiles.Click += (_, _) => Import.Flyout.Hide(); ImportFolder.Click += (_, _) => Import.Flyout.Hide();
        var showImport = new Button { Content = "Import summary" };
        More.Flyout = new Flyout { Content = new StackPanel { Spacing = ToolLayoutMetrics.Gap, Children = { Clear, showImport } } };
        showImport.Click += (_, _) => { ImportPanel.IsExpanded = true; model.ImportDetailsExpanded = true; More.Flyout.Hide(); Refresh(); ImportPanel.BringIntoView(); };
        Clear.Click += (_, _) => More.Flyout.Hide();
        var toolbar = new AdaptiveActionBar(Import, Cancel, SessionSummary, More);
        Add(0, toolbar);
        EmptyState.Children.Add(EmptyTitle);
        EmptyState.Children.Add(new TextBlock { Text = "Import FHIR data to browse patients and generate HL7 messages.", TextWrapping = TextWrapping.Wrap });
        EmptyState.Children.Add(EmptyImport); Add(1, EmptyState); Add(1, WorkspaceBody);

        var search = new StackPanel { Spacing = ToolLayoutMetrics.Gap, Children =
        { new TextBlock { Text = "Patients", [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.16"), FontWeight = FontWeight.SemiBold }, new AdaptiveActionBar(Search, Find) } };
        Search.Width = 170; Find.Content = "Find"; PatientBrowser.Children.Add(search);
        PatientBrowser.RowDefinitions[1].MinHeight = ToolLayoutMetrics.ListHeight;
        SetRow(Patients, 1); PatientBrowser.Children.Add(Patients); SetRow(PatientEmpty, 1); PatientBrowser.Children.Add(PatientEmpty);
        var patientPages = new AdaptiveActionBar(PatientsPrevious, PatientPageStatus, PatientsNext); SetRow(patientPages, 2); PatientBrowser.Children.Add(patientPages);
        Patients.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<FhirResource>((r, _) => Label(r is null ? "" : PatientLabel(r), r is null ? "" : "Input " + r.InputNumber + " · " + r.Id));
        Encounters.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<FhirResource>((r, _) => Label(r is null ? "" : r.Encounter?.ClassCode + " · " + r.Encounter?.Status + " · " + Date(r.Times[0].Time), r is null ? "" : FhirGenerationPlan.AdtEligibility(r) + " · " + FhirGenerationPlan.AdtEligibility(r, true)));
        Timeline.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<FhirTimelineEvent>((e, _) => e is null || model.Session.Data is not { } data ? new TextBlock() : Label(Date(e.Time) + " · " + e.Kind, Brief(data.Resource(e.ResourceKey))));
        var encounters = Page(Encounters, EncountersPrevious, EncounterPageStatus, EncountersNext); encounters.Children.Add(EncounterEmpty);
        var timeline = Page(Timeline, TimelinePrevious, TimelinePageStatus, TimelineNext); timeline.Children.Add(TimelineEmpty);
        Browse.ItemsSource = new[]
        {
            new TabItem { Header = "Overview", Content = new ScrollViewer { Content = Overview, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } },
            new TabItem { Header = "Encounters", Content = encounters },
            new TabItem { Header = "Timeline", Content = timeline }
        };
        Browse.MinHeight = 220; Browse.SelectedIndex = model.ClinicalTab;
        Browse.SelectionChanged += (_, _) => { if (!refreshing && Browse.SelectedIndex >= 0) model.ClinicalTab = Browse.SelectedIndex; };
        SelectedPatient.FontSize = 16; SelectedPatient.FontWeight = FontWeight.SemiBold;
        DetailPane.Children.Add(SelectedPatient); SetRow(SelectionPrompt, 1); DetailPane.Children.Add(SelectionPrompt);
        SetRow(Browse, 2); DetailPane.Children.Add(Browse); DetailPane.RowDefinitions[2].MinHeight = 220;
        ClinicalPanel.Content = new ScrollViewer { Content = Details, MaxHeight = 140, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        ClinicalPanel.IsExpanded = model.ClinicalExpanded;
        ClinicalPanel.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty) model.ClinicalExpanded = ClinicalPanel.IsExpanded; };
        SetRow(ClinicalPanel, 3); DetailPane.Children.Add(ClinicalPanel);
        WorkspaceBody.Children.Add(PatientBrowser); WorkspaceBody.Children.Add(patientSplitter); WorkspaceBody.Children.Add(DetailPane);
        WorkspaceBody.SizeChanged += (_, _) => ArrangeWorkspace();
        patientSplitter.DragCompleted += (_, _) =>
        {
            if (IsStacked) return;
            var columns = WorkspaceBody.ColumnDefinitions;
            var total = columns[0].ActualWidth + columns[2].ActualWidth;
            if (total > 0) model.PatientProportion = Math.Clamp(columns[0].ActualWidth / total, .2, .5);
        };

        PreviewPanel.Content = PreviewSummary; PreviewPanel.IsExpanded = model.PreviewExpanded;
        PreviewPanel.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty) model.PreviewExpanded = PreviewPanel.IsExpanded; };
        GenerationPanel.IsExpanded = model.GenerationExpanded;
        GenerationPanel.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty) model.GenerationExpanded = GenerationPanel.IsExpanded; };
        SeedField = new ResponsiveFormField("Seed", Seed);
        SeedHelpFor(SeedField.Label, SeedHelp); SeedHelpFor(Seed, SeedHelp);
        AdvancedOptions.IsExpanded = model.AdvancedExpanded;
        AdvancedOptions.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty) model.AdvancedExpanded = AdvancedOptions.IsExpanded; };
        AdvancedOptions.Content = new StackPanel { Spacing = ToolLayoutMetrics.Gap, Children = { SeedField, new ResponsiveFormField("Transport timestamp", TransportTime) } };
        Generate.Classes.Add("accent");
        GenerationPanel.Content = new StackPanel { Spacing = ToolLayoutMetrics.Gap, Children =
        {
            new TextBlock { Text = "Generate HL7 directly, or preview eligibility and limits first.", TextWrapping = TextWrapping.Wrap },
            new ResponsiveFormField("Generation scope", Scope),
            new ResponsiveFormField("Message types", Mode), AdvancedOptions,
            new TextBlock { Text = "Output: combined new unsaved document. Source files remain unchanged.", TextWrapping = TextWrapping.Wrap },
            GenerationState, new AdaptiveActionBar(Generate, Preview, ClearSelection), PreviewPanel
        } };
        GenerationState.MaxHeight = double.PositiveInfinity; Add(2, GenerationPanel);
        ImportPanel.Content = ImportSummary; ImportPanel.IsExpanded = model.ImportDetailsExpanded;
        ImportPanel.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty) model.ImportDetailsExpanded = ImportPanel.IsExpanded; };
        Add(3, new StackPanel { Spacing = ToolLayoutMetrics.Gap, Children = { Status, ImportPanel } });
        ToolTip.SetTip(Import, "Open local FHIR R4 bundle files or a folder. Import replaces the current session after successful validation.");
        ToolTip.SetTip(Cancel, "Cancel the current operation; completed data remains available.");
        ToolTip.SetTip(TransportTime, "Explicit generated transport metadata; never fills absent clinical times.");
        ToolTip.SetTip(ClearSelection, "Clear manual encounter/event selections. Selected Events then requires a selection; full-history scopes keep their normal meaning.");
        AutomationProperties.SetName(Patients, "FHIR patients"); AutomationProperties.SetName(Search, "Find FHIR patients");
        AutomationProperties.SetName(Encounters, "Patient encounters"); AutomationProperties.SetName(Timeline, "Patient clinical timeline");
        WireEvents(); LayoutSurface = ResponsiveToolSurface.Attach(this, toolbar, (1, 280)); ArrangeWorkspace(); Refresh();
    }

    private static void SeedHelpFor(Control control, string text)
    {
        ToolTip.SetTip(control, new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 400 });
        AutomationProperties.SetHelpText(control, text);
        if (control is not TextBox) return;
        control.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.F1) { ToolTip.SetIsOpen(control, true); e.Handled = true; }
            else if (e.Key == Avalonia.Input.Key.Escape && ToolTip.GetIsOpen(control)) { ToolTip.SetIsOpen(control, false); e.Handled = true; }
        };
        control.LostFocus += (_, _) => ToolTip.SetIsOpen(control, false);
        control.DetachedFromVisualTree += (_, _) => ToolTip.SetIsOpen(control, false);
    }

    private void ArrangeWorkspace()
    {
        var next = WorkspaceBody.Bounds.Width < ToolLayoutMetrics.FullWidth;
        if (stacked == next) return; stacked = next;
        WorkspaceBody.ColumnDefinitions = new(next ? "*" : FormattableString.Invariant($"{model.PatientProportion}*,6,{1 - model.PatientProportion}*"));
        WorkspaceBody.RowDefinitions = new(next ? "260,6,*" : "*");
        WorkspaceBody.RowDefinitions[next ? 2 : 0].MinHeight = 280;
        PatientBrowser.Height = next ? 260 : double.NaN;
        SetRow(PatientBrowser, 0); SetColumn(PatientBrowser, 0);
        SetRow(patientSplitter, next ? 1 : 0); SetColumn(patientSplitter, next ? 0 : 1);
        SetRow(DetailPane, next ? 2 : 0); SetColumn(DetailPane, next ? 0 : 2);
        patientSplitter.ResizeDirection = next ? GridResizeDirection.Rows : GridResizeDirection.Columns;
        patientSplitter.IsVisible = !next;
        if (LayoutSurface is { } surface) surface.Content.RowDefinitions[1].MinHeight = model.Session.Data?.Patients.Count > 0 ? next ? 546 : 280 : 0;
    }
    private void RememberInspection(FhirResource resource)
    {
        model.InspectedSession = model.Session.Data?.Id; model.InspectedResource = resource.Key;
        Details.Text = resource.Kind == FhirResourceKind.Patient ? PatientLabel(resource) : ClinicalDetails(resource);
        ClinicalPanel.IsVisible = resource.Kind != FhirResourceKind.Patient;
    }
    private void RefreshWorkspace()
    {
        var session = model.Session; var data = session.Data;
        var loaded = data?.Patients.Count > 0;
        WorkspaceBody.IsVisible = loaded; EmptyState.IsVisible = !loaded;
        EmptyTitle.Text = data is null ? "No patients loaded" : "No patients in this import";
        SessionSummary.Text = data is null ? "" : $"{data.Patients.Count:N0} patients · {data.Resources.Count:N0} resources";
        LayoutSurface.Content.RowDefinitions[1].MinHeight = loaded ? IsStacked ? 546 : 280 : 0;
        LayoutSurface.Content.RowDefinitions[1].Height = loaded ? GridLength.Star : GridLength.Auto;
        PatientEmpty.Text = "No matching patients. Try another name or identifier."; PatientEmpty.IsVisible = loaded && session.MatchingPatients == 0;
        Patients.IsVisible = loaded && session.MatchingPatients > 0;
        PatientsPrevious.IsVisible = PatientsNext.IsVisible = session.PatientPageCount > 1;
        SelectionPrompt.IsVisible = session.PatientKey is null; Browse.IsVisible = session.PatientKey is not null;
        Encounters.IsVisible = session.EncounterCount > 0; EncounterEmpty.IsVisible = !Encounters.IsVisible;
        Timeline.IsVisible = session.TimelinePage.Count > 0; TimelineEmpty.IsVisible = !Timeline.IsVisible;
        EncountersPrevious.IsVisible = EncountersNext.IsVisible = session.EncounterPageCount > 1;
        TimelinePrevious.IsVisible = TimelineNext.IsVisible = session.TimelinePageCount > 1;
        ImportPanel.IsVisible = data is not null && model.ImportDetailsExpanded;
        ClinicalPanel.IsVisible = false;
        Overview.Text = "";
        if (data is not null && session.PatientKey is { } key)
        {
            var patient = data.Resource(key); var p = patient.Person!;
            static string Available(string value) => value.Length == 0 ? "(not supplied)" : Short(value);
            Overview.Text = $"Name: {Available(p.DisplayName)}\nIdentifier: {Available(p.Identifier)}\nIdentifier system: {Available(p.IdentifierSystem)}\nBirth date: {Available(p.BirthDate)}\nGender: {Available(p.Gender)}\nAddress: {Available(string.Join(", ", p.AddressLines.Concat(new[] { p.City, p.State, p.PostalCode, p.Country }).Where(v => v.Length > 0)))}\n\nSource: input {patient.InputNumber} · Patient/{Short(patient.Id)}\n{session.EncounterCount:N0} encounters";
            if (model.InspectedSession == data.Id && model.InspectedResource is { } inspected && data.PatientOf(inspected) == key)
            { var resource = data.Resource(inspected); Details.Text = resource.Kind == FhirResourceKind.Patient ? PatientLabel(resource) : ClinicalDetails(resource); ClinicalPanel.IsVisible = resource.Kind != FhirResourceKind.Patient; }
        }
    }
}
