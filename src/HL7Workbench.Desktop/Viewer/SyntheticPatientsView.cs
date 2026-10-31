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

/// <summary>Session-only FHIR patient workspace; clinical values never participate in Dock persistence.</summary>
public sealed partial class SyntheticPatientsView : Grid
{
    private readonly SyntheticPatientsViewModel model;
    private bool refreshing, subscribed;
    private IReadOnlyList<FhirResource>? patientPage, encounterPage;
    private IReadOnlyList<FhirTimelineEvent>? timelinePage;
    public Button ImportFiles { get; } = new() { Content = "Import FHIR Bundles…" };
    public Button ImportFolder { get; } = new() { Content = "Import folder…" };
    public Button Clear { get; } = new() { Content = "Clear session" };
    public Button Cancel { get; } = new() { Content = "Cancel" };
    public TextBox Search { get; } = new() { Width = 240, MaxLength = 256, PlaceholderText = "Name or identifier" };
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
    public ResponsiveToolSurface LayoutSurface { get; }
    private void WireEvents()
    {
        ImportFiles.Click += (_, _) => ActionTask = model.ImportFilesAsync(); ImportFolder.Click += (_, _) => ActionTask = model.ImportFolderAsync();
        Clear.Click += (_, _) => { model.Clear(); Search.Text = ""; Details.Text = ""; }; Cancel.Click += (_, _) => model.Cancel();
        Find.Click += (_, _) => ActionTask = model.Session.FilterPatientsAsync(Search.Text ?? "");
        Search.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; ActionTask = model.Session.FilterPatientsAsync(Search.Text ?? ""); } };
        Patients.SelectionChanged += (_, _) =>
        {
            if (!refreshing && Patients.SelectedItem is FhirResource patient && model.Session.PatientPage.Any(r => ReferenceEquals(r,patient)))
            { model.Session.SelectPatient(patient.Key); RememberInspection(patient); }
        };
        Encounters.SelectionChanged += (_, _) =>
        {
            if (refreshing) return;
            var visible = model.Session.EncounterPage.Select(r => r.Key).ToHashSet();
            model.Session.SelectEncounters(model.Session.SelectedEncounters.Except(visible).Concat(Encounters.SelectedItems!.OfType<FhirResource>().Where(r => model.Session.EncounterPage.Any(current => ReferenceEquals(current,r))).Select(r => r.Key)));
            if (Encounters.SelectedItem is FhirResource item && model.Session.EncounterPage.Any(r => ReferenceEquals(r,item))) RememberInspection(item);
        };
        Timeline.SelectionChanged += (_, _) =>
        {
            if (refreshing) return;
            var visible = model.Session.TimelinePage.Select(e => new FhirEventSelection(e.ResourceKey, e.Kind)).ToHashSet();
            model.Session.SelectEvents(model.Session.SelectedEvents.Except(visible).Concat(Timeline.SelectedItems!.OfType<FhirTimelineEvent>().Where(e => model.Session.TimelinePage.Any(current => ReferenceEquals(current,e))).Select(e => new FhirEventSelection(e.ResourceKey, e.Kind))));
            if (Timeline.SelectedItem is FhirTimelineEvent item && model.Session.Data is { } data && model.Session.TimelinePage.Any(e => ReferenceEquals(e,item))) RememberInspection(data.Resource(item.ResourceKey));
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
            try { Patients.ItemsSource = Encounters.ItemsSource = Timeline.ItemsSource = null; patientPage = encounterPage = null; timelinePage = null; Search.Text = SelectedPatient.Text = Details.Text = Overview.Text = SessionSummary.Text = ""; }
            finally { refreshing = false; }
        };
    }
    private static ListBox List(SelectionMode mode = SelectionMode.Single)
    {
        var list = new ListBox { SelectionMode = mode, ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()) };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled); return list;
    }
    private void Add(int row, Control control) { SetRow(control, row); Children.Add(control); }
    private static AdaptiveActionBar ScrollStrip(params Control[] controls) => new(controls);
    private static Grid Page(ListBox list, Button previous, TextBlock status, Button next)
    { var grid = new ToolContentGrid { RowDefinitions = new("*,Auto") }; grid.RowDefinitions[0].MinHeight = ToolLayoutMetrics.ListHeight; grid.Children.Add(list); var pager = ScrollStrip(previous,status,next); SetRow(pager,1); grid.Children.Add(pager); return grid; }
    private static Control Label(string first, string second) => new StackPanel { Margin = new(4), Children =
    { new TextBlock { Text = Short(first), TextWrapping = TextWrapping.Wrap }, new TextBlock { Text = Short(second), [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.11"), TextWrapping = TextWrapping.Wrap } } };
    private static string Short(string text) => text.Length <= 512 ? text : text[..(char.IsHighSurrogate(text[511]) ? 511 : 512)] + "… [preview]";
    private static string PatientLabel(FhirResource resource) => Short((resource.Person!.DisplayName.Length == 0 ? "Unnamed patient" : resource.Person.DisplayName) + " · " + (resource.Person.Identifier.Length > 0 ? resource.Person.Identifier : resource.Id));
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
    private void FocusImport() => Import.Focus();
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        refreshing = true;
        try
        {
            var session = model.Session;
            ImportFiles.IsEnabled = ImportFolder.IsEnabled = model.CanWork; Clear.IsEnabled = session.Data is not null || model.IsBusy; Cancel.IsEnabled = model.IsBusy; Cancel.IsVisible = model.IsBusy; Import.IsEnabled = EmptyImport.IsEnabled = model.CanWork;
            Search.IsEnabled = Find.IsEnabled = model.CanWork && session.Data is not null; Browse.IsEnabled = model.CanWork;
            if (!ReferenceEquals(patientPage, session.PatientPage)) { patientPage = session.PatientPage; Patients.ItemsSource = patientPage; }
            if (!ReferenceEquals(encounterPage, session.EncounterPage)) { encounterPage = session.EncounterPage; Encounters.ItemsSource = encounterPage; }
            if (!ReferenceEquals(timelinePage, session.TimelinePage)) { timelinePage = session.TimelinePage; Timeline.ItemsSource = timelinePage; }
            Patients.SelectedItem = patientPage?.FirstOrDefault(r => r.Key == session.PatientKey);
            Encounters.SelectedItems!.Clear(); foreach (var item in session.EncounterPage.Where(r => session.SelectedEncounters.Contains(r.Key))) Encounters.SelectedItems.Add(item);
            Timeline.SelectedItems!.Clear(); foreach (var item in session.TimelinePage.Where(r => session.SelectedEvents.Contains(new(r.ResourceKey,r.Kind)))) Timeline.SelectedItems.Add(item);
            SelectedPatient.Text = session.PatientKey is { } patient && session.Data is { } data ? "Selected: " + PatientLabel(data.Resource(patient)) : "Select a patient to inspect clinical records.";
            PatientPageStatus.Text = $"{session.PatientPageIndex+1}/{session.PatientPageCount} · {session.MatchingPatients:N0} patients";
            EncounterPageStatus.Text = $"{session.EncounterPageIndex+1}/{session.EncounterPageCount} · {session.SelectedEncounters.Count} selected";
            TimelinePageStatus.Text = $"{session.TimelinePageIndex+1}/{session.TimelinePageCount} · {session.SelectedEvents.Count} selected events";
            PatientsPrevious.IsEnabled = model.CanWork && session.PatientPageIndex > 0; PatientsNext.IsEnabled = model.CanWork && session.PatientPageIndex+1 < session.PatientPageCount;
            EncountersPrevious.IsEnabled = model.CanWork && session.EncounterPageIndex > 0; EncountersNext.IsEnabled = model.CanWork && session.EncounterPageIndex+1 < session.EncounterPageCount;
            TimelinePrevious.IsEnabled = model.CanWork && session.TimelinePageIndex > 0; TimelineNext.IsEnabled = model.CanWork && session.TimelinePageIndex+1 < session.TimelinePageCount;
            Scope.SelectedIndex = (int)model.Scope; Mode.SelectedIndex = model.Mode; Seed.Text = model.Seed; TransportTime.Text = model.TransportTimestamp; Scope.IsEnabled = Mode.IsEnabled = Seed.IsEnabled = TransportTime.IsEnabled = model.CanWork;
            ClearSelection.IsEnabled = model.CanWork && session.PatientKey is not null;
            Preview.IsEnabled = model.CanPreview;
            Generate.IsEnabled = model.CanGenerate;
            var scopeHelp = model.Scope switch
            {
                FhirGenerationScope.SelectedPatient => "Generate the selected patient's complete supported history. No event selection needed.",
                FhirGenerationScope.EntireImport => "Include all imported patients, regardless of search or manual selections.",
                _ => "Advanced: select encounters and/or timeline events. Click/Space toggles selections; selections are retained when switching scopes."
            };
            GenerationState.Text = session.Preview is { } summary ?
                $"{FhirGenerationReport.ScopeLabel(summary.Scope)}: {summary.PatientsIncluded:N0} patients, {summary.EncountersConsidered:N0} encounters considered, {summary.Messages:N0} eligible HL7 events/messages. " + session.GenerationUnavailableReason :
                scopeHelp + " " + session.GenerationUnavailableReason;
            ToolTip.SetTip(GenerationState, GenerationState.Text);
            ToolTip.SetTip(Preview, model.ConfigurationError.Length > 0 ? model.ConfigurationError : session.PreviewUnavailableReason.Length > 0 ? session.PreviewUnavailableReason : "Optional: inspect eligibility, output size and limitations without creating a document.");
            ToolTip.SetTip(Generate, model.ConfigurationError.Length > 0 ? model.ConfigurationError : session.GenerationUnavailableReason.Length > 0 ? session.GenerationUnavailableReason : "Prepare eligibility and output size, then generate a new unsaved document.");
            PreviewSummary.Text = session.Preview is { } preview ? GenerationReportView.Format(preview) : "Generate or Preview calculates eligible ADT/ORU counts and output size. Limits: 1,000 messages and 4 Mi UTF-16 characters; no partial publication.";
            ImportSummary.Text = session.Data is { } source ? $"Captured session {source.Id:N}. {source.Summary.Files:N0} files; {source.Resources.Count:N0} unique resources; {source.Patients.Count:N0} patients. " +
                $"Synthea-identifier hints: {source.Summary.SyntheaIdentifierPatients:N0}.\n" + string.Join(" · ", source.Summary.Counts.Select(c => c.Key + ": " + c.Value)) + "\n" +
                $"References: unresolved {source.Summary.UnresolvedReferences}, ambiguous {source.Summary.AmbiguousReferences}, conflicting {source.Summary.ConflictingReferences}, unsupported/unprojected {source.Summary.UnsupportedReferences}; ambiguous aliases {source.Summary.AmbiguousAliases}. " +
                $"Unassociated clinical resources {source.Summary.UnassociatedClinicalResources}; missing dates {source.Summary.MissingTimestamps}, invalid {source.Summary.InvalidTimestamps}, inconsistent periods {source.Summary.InconsistentPeriods}.\n" +
                "Partial dates are ordered at their available precision; unknown/invalid dates appear last. No dates or patient membership are inferred. Clinical text is a bounded preview. No raw JSON, narrative/attachment rendering or permanent patient database." : "No imported session.";
            RefreshWorkspace();
            Status.Text = model.Status; ToolTip.SetTip(Status,model.Status);
            if (session.Data is null) Search.Text = "";
            if (session.PatientKey is null) Details.Text = "";
        }
        finally { refreshing = false; }
    }
}
