using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Core;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Paged synthetic clinical browser; session values never participate in Dock persistence.</summary>
public sealed class SyntheticPatientsView : Grid
{
    private readonly SyntheticPatientsViewModel model;
    private bool refreshing, subscribed;
    private IReadOnlyList<FhirResource>? patientPage, encounterPage;
    private IReadOnlyList<FhirTimelineEvent>? timelinePage;
    public CheckBox Confirm { get; } = new() { Content = "I confirm these files contain synthetic/test data only." };
    public Button ImportFiles { get; } = new() { Content = "Import FHIR Bundles…" };
    public Button ImportFolder { get; } = new() { Content = "Import folder…" };
    public Button Clear { get; } = new() { Content = "Clear session" };
    public Button Cancel { get; } = new() { Content = "Cancel" };
    public TextBox Search { get; } = new() { Width = 240, MaxLength = 256, PlaceholderText = "Synthetic identifier or name" };
    public Button Find { get; } = new() { Content = "Find patients" };
    public ListBox Patients { get; } = List();
    public ListBox Encounters { get; } = List(SelectionMode.Multiple | SelectionMode.Toggle);
    public ListBox Timeline { get; } = List(SelectionMode.Multiple | SelectionMode.Toggle);
    public TabControl Browse { get; } = new();
    public Button PatientsPrevious { get; } = new() { Content = "Previous" };
    public Button PatientsNext { get; } = new() { Content = "Next" };
    public Button EncountersPrevious { get; } = new() { Content = "Previous" };
    public Button EncountersNext { get; } = new() { Content = "Next" };
    public Button TimelinePrevious { get; } = new() { Content = "Previous" };
    public Button TimelineNext { get; } = new() { Content = "Next" };
    public TextBlock PatientPageStatus { get; } = new();
    public TextBlock EncounterPageStatus { get; } = new();
    public TextBlock TimelinePageStatus { get; } = new();
    public TextBlock SelectedPatient { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public TextBlock Details { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public TextBlock Status { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public TextBlock ImportSummary { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public TextBlock PreviewSummary { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public ComboBox Scope { get; } = new() { ItemsSource = Enum.GetValues<FhirGenerationScope>().Select(FhirGenerationReport.ScopeLabel).ToArray(), Width = 230 };
    public TextBlock GenerationState { get; } = new() { TextWrapping = TextWrapping.Wrap, MaxHeight = 65 };
    public ComboBox Mode { get; } = new() { ItemsSource = new[] { "ADT and ORU", "ADT only", "ORU only" }, Width = 145 };
    public TextBox Seed { get; } = new() { Width = 100, MaxLength = 10 };
    public TextBox TransportTime { get; } = new() { Width = 275, MaxLength = 40 };
    public Button Preview { get; } = new() { Content = "Preview generation" };
    public Button Generate { get; } = new() { Content = "Generate HL7" };
    public Button ClearSelection { get; } = new() { Content = "Clear event selection" };
    public Task ActionTask { get; private set; } = Task.CompletedTask;
    public SyntheticPatientsView(SyntheticPatientsViewModel model)
    {
        this.model = model; Margin = new(8); MinWidth = 280; MinHeight = 200; RowDefinitions = new("Auto,Auto,*,Auto,Auto"); RowSpacing = 4;
        var warning = new TextBlock { Text = FhirR4Importer.SyntheticWarning, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold, MaxHeight = 70 };
        ToolTip.SetTip(warning, warning.Text);
        Add(0, new StackPanel { Spacing = 3, Children = { warning, ScrollStrip(Confirm, ImportFiles, ImportFolder, Clear, Cancel) } });
        Add(1, new StackPanel { Spacing = 3, Children = { ScrollStrip(Search, Find), SelectedPatient } });
        Patients.ItemTemplate = new FuncDataTemplate<FhirResource>((r, _) => Label(r is null ? "" : PatientLabel(r), r is null ? "" : "Input " + r.InputNumber + " · " + r.Id));
        Encounters.ItemTemplate = new FuncDataTemplate<FhirResource>((r, _) => Label(r is null ? "" : r.Encounter?.ClassCode + " · " + r.Encounter?.Status + " · " + Date(r.Times[0].Time),
            r is null ? "" : FhirGenerationPlan.AdtEligibility(r) + " · " + FhirGenerationPlan.AdtEligibility(r, true)));
        Timeline.ItemTemplate = new FuncDataTemplate<FhirTimelineEvent>((e, _) =>
        {
            if (e is null || model.Session.Data is not { } data) return new TextBlock();
            var resource = data.Resource(e.ResourceKey); return Label(Date(e.Time) + " · " + e.Kind, Brief(resource));
        });
        Browse.ItemsSource = new[]
        {
            new TabItem { Header = "Patients", Content = Page(Patients, PatientsPrevious, PatientPageStatus, PatientsNext) },
            new TabItem { Header = "Encounters", Content = Page(Encounters, EncountersPrevious, EncounterPageStatus, EncountersNext) },
            new TabItem { Header = "Clinical timeline", Content = Page(Timeline, TimelinePrevious, TimelinePageStatus, TimelineNext) }
        };
        var body = new Grid { RowDefinitions = new("*,Auto") }; body.Children.Add(Browse);
        var detail = new Expander { Header = "Selected clinical details", Content = new ScrollViewer { MaxHeight = 130, Content = Details } }; SetRow(detail, 1); body.Children.Add(detail); Add(2, body);
        Add(3, new StackPanel { Spacing = 3, Children =
        {
            ScrollStrip(new TextBlock { Text = "Generation scope", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }, Scope,
                new TextBlock { Text = "Output: Combined new unsaved document", VerticalAlignment = VerticalAlignment.Center }),
            GenerationState,
            ScrollStrip(Mode, new TextBlock { Text = "Seed", VerticalAlignment = VerticalAlignment.Center }, Seed, ClearSelection, Preview, Generate),
            ScrollStrip(new TextBlock { Text = "Transport timestamp", VerticalAlignment = VerticalAlignment.Center }, TransportTime),
            new Expander { Header = "Generation preview and warnings", Content = new ScrollViewer { MaxHeight = 160, Content = PreviewSummary } },
            new Expander { Header = "Import summary and limitations", Content = new ScrollViewer { MaxHeight = 150, Content = ImportSummary } }
        } });
        Status.MaxHeight = 65; ToolTip.SetTip(Status, "Import/generation status. Sources are never modified."); Add(4, Status);
        AutomationProperties.SetName(Patients, "Imported synthetic patients"); AutomationProperties.SetName(Encounters, "Synthetic encounters"); AutomationProperties.SetName(Timeline, "Synthetic clinical timeline");
        AutomationProperties.SetName(Search, "Synthetic patient search"); AutomationProperties.SetName(Seed, "FHIR generation seed"); AutomationProperties.SetName(TransportTime, "Generated transport timestamp");
        AutomationProperties.SetName(Scope, "Synthetic history generation scope");
        ToolTip.SetTip(TransportTime, "Explicit generated message/recording metadata, UTC whole seconds; never fills absent clinical times.");
        Confirm.IsCheckedChanged += (_, _) => { if (!refreshing) model.Confirmed = Confirm.IsChecked == true; };
        ImportFiles.Click += (_, _) => ActionTask = model.ImportFilesAsync(); ImportFolder.Click += (_, _) => ActionTask = model.ImportFolderAsync();
        Clear.Click += (_, _) => { model.Clear(); Search.Text = ""; Details.Text = ""; }; Cancel.Click += (_, _) => model.Session.Cancel();
        Find.Click += (_, _) => ActionTask = model.Session.FilterPatientsAsync(Search.Text ?? "");
        Search.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; ActionTask = model.Session.FilterPatientsAsync(Search.Text ?? ""); } };
        Patients.SelectionChanged += (_, _) =>
        {
            if (!refreshing && Patients.SelectedItem is FhirResource patient && model.Session.PatientPage.Any(r => ReferenceEquals(r,patient)))
            { model.Session.SelectPatient(patient.Key); Details.Text = PatientLabel(patient); }
        };
        Encounters.SelectionChanged += (_, _) =>
        {
            if (refreshing) return;
            var visible = model.Session.EncounterPage.Select(r => r.Key).ToHashSet();
            model.Session.SelectEncounters(model.Session.SelectedEncounters.Except(visible).Concat(Encounters.SelectedItems!.OfType<FhirResource>().Where(r => model.Session.EncounterPage.Any(current => ReferenceEquals(current,r))).Select(r => r.Key)));
            if (Encounters.SelectedItem is FhirResource item && model.Session.EncounterPage.Any(r => ReferenceEquals(r,item))) Details.Text = ClinicalDetails(item);
        };
        Timeline.SelectionChanged += (_, _) =>
        {
            if (refreshing) return;
            var visible = model.Session.TimelinePage.Select(e => new FhirEventSelection(e.ResourceKey, e.Kind)).ToHashSet();
            model.Session.SelectEvents(model.Session.SelectedEvents.Except(visible).Concat(Timeline.SelectedItems!.OfType<FhirTimelineEvent>().Where(e => model.Session.TimelinePage.Any(current => ReferenceEquals(current,e))).Select(e => new FhirEventSelection(e.ResourceKey, e.Kind))));
            if (Timeline.SelectedItem is FhirTimelineEvent item && model.Session.Data is { } data && model.Session.TimelinePage.Any(e => ReferenceEquals(e,item))) Details.Text = ClinicalDetails(data.Resource(item.ResourceKey));
        };
        PatientsPrevious.Click += (_, _) => model.Session.SetPatientPage(model.Session.PatientPageIndex - 1); PatientsNext.Click += (_, _) => model.Session.SetPatientPage(model.Session.PatientPageIndex + 1);
        EncountersPrevious.Click += (_, _) => model.Session.SetEncounterPage(model.Session.EncounterPageIndex - 1); EncountersNext.Click += (_, _) => model.Session.SetEncounterPage(model.Session.EncounterPageIndex + 1);
        TimelinePrevious.Click += (_, _) => model.Session.SetTimelinePage(model.Session.TimelinePageIndex - 1); TimelineNext.Click += (_, _) => model.Session.SetTimelinePage(model.Session.TimelinePageIndex + 1);
        Mode.SelectionChanged += (_, _) => { if (!refreshing) model.Mode = Mode.SelectedIndex; };
        Scope.SelectionChanged += (_, _) => { if (!refreshing && Scope.SelectedIndex >= 0) model.Scope = (FhirGenerationScope)Scope.SelectedIndex; };
        Seed.PropertyChanged += (_, e) => { if (!refreshing && e.Property == TextBox.TextProperty) model.Seed = Seed.Text ?? ""; };
        TransportTime.PropertyChanged += (_, e) => { if (!refreshing && e.Property == TextBox.TextProperty) model.TransportTimestamp = TransportTime.Text ?? ""; };
        ClearSelection.Click += (_, _) => model.Session.ClearSelection(); Preview.Click += (_, _) => ActionTask = model.PreviewAsync(); Generate.Click += (_, _) => ActionTask = model.GenerateAsync();
        AttachedToVisualTree += (_, _) => { if (!subscribed) { model.PropertyChanged += Changed; model.Shown += FocusImport; subscribed = true; } Search.Text = model.Session.PatientFilter; Refresh(); };
        DetachedFromVisualTree += (_, _) =>
        {
            model.PropertyChanged -= Changed; model.Shown -= FocusImport; subscribed = false;
            refreshing = true;
            try { Patients.ItemsSource = Encounters.ItemsSource = Timeline.ItemsSource = null; patientPage = encounterPage = null; timelinePage = null; Search.Text = SelectedPatient.Text = Details.Text = ""; }
            finally { refreshing = false; }
        };
        Refresh();
    }
    private static ListBox List(SelectionMode mode = SelectionMode.Single) => new() { SelectionMode = mode, ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()) };
    private void Add(int row, Control control) { SetRow(control, row); Children.Add(control); }
    private static ScrollViewer ScrollStrip(params Control[] controls)
    { var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 }; foreach (var c in controls) strip.Children.Add(c); return new() { Content = strip, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }; }
    private static Grid Page(ListBox list, Button previous, TextBlock status, Button next)
    { var grid = new Grid { RowDefinitions = new("*,Auto") }; grid.Children.Add(list); var pager = ScrollStrip(previous,status,next); SetRow(pager,1); grid.Children.Add(pager); return grid; }
    private static Control Label(string first, string second) => new StackPanel { Margin = new(4), Children =
    { new TextBlock { Text = Short(first), TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = Short(second), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis } } };
    private static string Short(string text) => text.Length <= 512 ? text : text[..(char.IsHighSurrogate(text[511]) ? 511 : 512)] + "… [preview]";
    private static string PatientLabel(FhirResource resource) => Short((resource.Person!.DisplayName.Length == 0 ? "Unnamed synthetic patient" : resource.Person.DisplayName) + " · " + (resource.Person.Identifier.Length > 0 ? resource.Person.Identifier : resource.Id));
    private static string Date(FhirClinicalTime time) => time.Precision == FhirTimePrecision.Missing ? "[date missing]" : time.Precision == FhirTimePrecision.Invalid ? "[date invalid/unsupported precision]" : time.Text + (time.IsInstant ? "" : " [" + time.Precision + " precision]");
    private static string Brief(FhirResource resource)
    {
        var value = resource.Value;
        var text = resource.Kind + " · " + (resource.Code?.Codings.FirstOrDefault()?.Display ?? resource.Code?.Text ?? "") + " · " + resource.Status;
        if (value is not null) text += " · " + value.Kind + ": " + (value.Concept?.Codings.FirstOrDefault()?.Display ?? value.Concept?.Text ?? value.Text) + " " + value.Unit;
        return Short(text.Replace("\r", "␍", StringComparison.Ordinal).Replace("\n", "␊", StringComparison.Ordinal));
    }
    private string ClinicalDetails(FhirResource resource)
    {
        var data = model.Session.Data!;
        return Brief(resource) + "\n" + string.Join(" · ", resource.Times.Select(t => t.Kind + ": " + Date(t.Time))) + "\n" +
            "Input " + resource.InputNumber + " · " + resource.ResourceType + "/" + resource.Id + (data.IsQuarantined(resource.Key) ? " · Relationship conflict/unresolved encounter: generation disabled." : "") + "\n" +
            string.Join("\n", data.LinksOf(resource.Key).Select(l => l.Role + ": " + l.Resolution + (l.Target is { } key ? " · " + Short(data.Resource(key).OrganizationName.Length > 0 ? data.Resource(key).OrganizationName : data.Resource(key).Person?.DisplayName ?? data.Resource(key).ResourceType) : "")));
    }
    private void FocusImport() => Confirm.Focus();
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        refreshing = true;
        try
        {
            var session = model.Session; Confirm.IsChecked = model.Confirmed; Confirm.IsEnabled = model.CanWork;
            ImportFiles.IsEnabled = ImportFolder.IsEnabled = model.CanWork && model.Confirmed; Clear.IsEnabled = session.Data is not null || model.IsBusy; Cancel.IsEnabled = session.IsBusy;
            Search.IsEnabled = Find.IsEnabled = model.CanWork && session.Data is not null; Browse.IsEnabled = model.CanWork;
            if (!ReferenceEquals(patientPage, session.PatientPage)) { patientPage = session.PatientPage; Patients.ItemsSource = patientPage; }
            if (!ReferenceEquals(encounterPage, session.EncounterPage)) { encounterPage = session.EncounterPage; Encounters.ItemsSource = encounterPage; }
            if (!ReferenceEquals(timelinePage, session.TimelinePage)) { timelinePage = session.TimelinePage; Timeline.ItemsSource = timelinePage; }
            Patients.SelectedItem = patientPage?.FirstOrDefault(r => r.Key == session.PatientKey);
            Encounters.SelectedItems!.Clear(); foreach (var item in session.EncounterPage.Where(r => session.SelectedEncounters.Contains(r.Key))) Encounters.SelectedItems.Add(item);
            Timeline.SelectedItems!.Clear(); foreach (var item in session.TimelinePage.Where(r => session.SelectedEvents.Contains(new(r.ResourceKey,r.Kind)))) Timeline.SelectedItems.Add(item);
            SelectedPatient.Text = session.PatientKey is { } patient && session.Data is { } data ? "Selected: " + PatientLabel(data.Resource(patient)) : "Select a synthetic patient. Import is session-only; closing this tool clears it.";
            SelectedPatient.MaxHeight = 45;
            PatientPageStatus.Text = $"{session.PatientPageIndex+1}/{session.PatientPageCount} · {session.MatchingPatients:N0} patients";
            EncounterPageStatus.Text = $"{session.EncounterPageIndex+1}/{session.EncounterPageCount} · {session.SelectedEncounters.Count} selected";
            TimelinePageStatus.Text = $"{session.TimelinePageIndex+1}/{session.TimelinePageCount} · {session.SelectedEvents.Count} selected events";
            PatientsPrevious.IsEnabled = model.CanWork && session.PatientPageIndex > 0; PatientsNext.IsEnabled = model.CanWork && session.PatientPageIndex+1 < session.PatientPageCount;
            EncountersPrevious.IsEnabled = model.CanWork && session.EncounterPageIndex > 0; EncountersNext.IsEnabled = model.CanWork && session.EncounterPageIndex+1 < session.EncounterPageCount;
            TimelinePrevious.IsEnabled = model.CanWork && session.TimelinePageIndex > 0; TimelineNext.IsEnabled = model.CanWork && session.TimelinePageIndex+1 < session.TimelinePageCount;
            Scope.SelectedIndex = (int)model.Scope; Mode.SelectedIndex = model.Mode; Seed.Text = model.Seed; TransportTime.Text = model.TransportTimestamp; Scope.IsEnabled = Mode.IsEnabled = Seed.IsEnabled = TransportTime.IsEnabled = model.CanWork;
            ClearSelection.IsEnabled = model.CanWork && session.PatientKey is not null;
            Preview.IsEnabled = model.CanWork && session.PreviewUnavailableReason.Length == 0;
            Generate.IsEnabled = model.CanWork && session.Preview?.CanGenerate == true;
            var scopeHelp = model.Scope switch
            {
                FhirGenerationScope.SelectedPatient => "Select a patient, then Preview their complete supported history. No event selection needed.",
                FhirGenerationScope.EntireImport => "Include all imported patients, regardless of search or manual selections.",
                _ => "Advanced: select encounters and/or timeline events. Click/Space toggles selections; selections are retained when switching scopes."
            };
            GenerationState.Text = session.Preview is { } summary ?
                $"{FhirGenerationReport.ScopeLabel(summary.Scope)}: {summary.PatientsIncluded:N0} patients, {summary.EncountersConsidered:N0} encounters considered, {summary.Messages:N0} eligible HL7 events/messages. " + session.GenerationUnavailableReason :
                scopeHelp + " " + session.GenerationUnavailableReason;
            ToolTip.SetTip(GenerationState, GenerationState.Text); ToolTip.SetTip(Preview, session.PreviewUnavailableReason); ToolTip.SetTip(Generate, session.GenerationUnavailableReason);
            PreviewSummary.Text = session.Preview is { } preview ? GenerationReportView.Format(preview) : "Preview determines eligible ADT/ORU counts and output size. Limits: 1,000 messages and 4 Mi UTF-16 characters; no partial publication.";
            ImportSummary.Text = session.Data is { } source ? $"Captured session {source.Id:N}. {source.Summary.Files:N0} files; {source.Resources.Count:N0} unique resources; {source.Patients.Count:N0} patients. " +
                $"Synthea-identifier hints: {source.Summary.SyntheaIdentifierPatients:N0} (not proof of safety).\n" + string.Join(" · ", source.Summary.Counts.Select(c => c.Key + ": " + c.Value)) + "\n" +
                $"References: unresolved {source.Summary.UnresolvedReferences}, ambiguous {source.Summary.AmbiguousReferences}, conflicting {source.Summary.ConflictingReferences}, unsupported/unprojected {source.Summary.UnsupportedReferences}; ambiguous aliases {source.Summary.AmbiguousAliases}. " +
                $"Unassociated clinical resources {source.Summary.UnassociatedClinicalResources}; missing dates {source.Summary.MissingTimestamps}, invalid {source.Summary.InvalidTimestamps}, inconsistent periods {source.Summary.InconsistentPeriods}.\n" +
                "Partial dates are ordered at their available precision; unknown/invalid dates appear last. No dates or patient membership are inferred. Clinical text is a bounded preview. No raw JSON, narrative/attachment rendering or permanent patient database." : "No imported session.";
            Status.Text = model.Status; ToolTip.SetTip(Status,model.Status);
            if (session.Data is null) Search.Text = "";
            if (session.PatientKey is null) Details.Text = "";
        }
        finally { refreshing = false; }
    }
}
