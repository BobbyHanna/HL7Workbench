using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Text.Json.Nodes;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class SyntheticPatientsViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Fixture => Path.Combine(AppContext.BaseDirectory,"Fixtures","fhir-r4","patient-alpha.json");
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump(Window window){window.UpdateLayout();Dispatcher.UIThread.RunJobs();window.UpdateLayout();}
    [Theory]
    [InlineData(false,380)] [InlineData(true,1100)]
    public async Task ImportWithoutAcknowledgmentBrowseSearchTimelinePreviewGenerateAndClearUseNormalThemedControls(bool dark,int width)
    {
        await using var ui=HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async()=>
        {
            using var documents=new DocumentManager();using var model=new SyntheticPatientsViewModel(documents);model.ChooseFilesAsync=()=>Task.FromResult<IReadOnlyList<string>>([Fixture]);
            var view=new SyntheticPatientsView(model);var window=new Window{Content=view,Width=width,Height=780,RequestedThemeVariant=dark?ThemeVariant.Dark:ThemeVariant.Light};window.Show();
            try
            {
                Pump(window);Assert.True(view.ImportFiles.IsEnabled);Assert.False(view.Generate.IsEnabled);Assert.True(view.EmptyState.IsVisible);Assert.False(view.GenerationPanel.IsExpanded);view.GenerationPanel.IsExpanded=true;
                Click(view.ImportFiles);await view.ActionTask;Pump(window);Assert.Single(model.Session.PatientPage);Assert.NotNull(model.Session.Data);
                view.Search.Text="syn-alpha";view.Search.Focus();view.Search.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter});await view.ActionTask;Pump(window);Assert.Equal(1,model.Session.MatchingPatients);
                view.Patients.SelectedItem=model.Session.PatientPage[0];Pump(window);Assert.NotNull(model.Session.PatientKey);Assert.Contains("SYNTHETIC",view.SelectedPatient.Text!);
                view.Browse.SelectedIndex=1;view.Encounters.SelectedItems!.Add(model.Session.EncounterPage[0]);Pump(window);Assert.Single(model.Session.SelectedEncounters);
                view.Browse.SelectedIndex=2;Pump(window);Assert.Contains(model.Session.TimelinePage,e=>e.Kind==FhirEventKind.ConditionOnset);Assert.Contains(model.Session.TimelinePage,e=>e.Kind==FhirEventKind.ObservationEffective);
                view.Seed.Text="123";Click(view.Preview);await view.ActionTask;Pump(window);Assert.Equal(3,model.Session.Preview!.Messages);Assert.True(view.Generate.IsEnabled);Assert.Contains("Review required",view.PreviewSummary.Text!);
                Click(view.Generate);await view.ActionTask;Pump(window);var doc=Assert.Single(documents.Documents);Assert.NotNull(doc.GenerationReport);Assert.True(doc.IsDirty);Assert.Null(doc.SavePath);
                using var reportModel=new GenerationReportViewModel(documents);var reportView=new GenerationReportView(reportModel);Assert.Contains("mapping 1",reportView.Details.Text!);Assert.DoesNotContain("SYN-ALPHA",reportView.Details.Text!);
                var reconstructed=new FileDocumentView(doc);Assert.True(reconstructed.GenerationIndicator.IsVisible);Assert.Contains("3 messages",reconstructed.GenerationIndicator.Summary.Text!);Assert.Equal(reportView.Details.Text,new GenerationReportView(reportModel).Details.Text);
                Assert.True(view.Browse.Bounds.Height>70);Assert.True(view.Generate.Bounds.Width>50);
                Click(view.Clear);Pump(window);Assert.Null(model.Session.Data);Assert.Equal(0,view.Patients.ItemCount);Assert.Equal("",view.Search.Text);Assert.Equal("",view.Details.Text);Assert.Same(doc.GenerationReport,documents.Documents[0].GenerationReport);
            }
            finally{window.Close();}
            return true;
        },Token);
    }
    [Fact]
    public async Task PendingPickerCannotImportAfterClearAndViewReconstructionRetainsOnlyLiveSessionState()
    {
        await using var ui=HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async()=>
        {
            using var documents=new DocumentManager();using var model=new SyntheticPatientsViewModel(documents);var picker=new TaskCompletionSource<IReadOnlyList<string>>();model.ChooseFilesAsync=()=>picker.Task;
            var pending=model.ImportFilesAsync();model.Clear();picker.SetResult([Fixture]);await pending;Assert.Null(model.Session.Data);
            await model.Session.ImportFilesAsync([Fixture],true,Token);await model.Session.FilterPatientsAsync("syn-alpha",Token);
            model.Session.SelectPatient(model.Session.PatientPage[0].Key);
            var first=new SyntheticPatientsView(model);var window=new Window{Content=first,Width=900,Height=780};window.Show();Pump(window);
            try
            {
                Assert.Equal("syn-alpha",first.Search.Text);Assert.Contains("SYNTHETIC",first.Overview.Text!);window.Content=new TextBlock();Pump(window);Assert.Equal(0,first.Patients.ItemCount);Assert.Equal("",first.Overview.Text);Assert.NotNull(model.Session.Data);
                var second=new SyntheticPatientsView(model);window.Content=second;Pump(window);Assert.Equal("syn-alpha",second.Search.Text);Assert.Single(model.Session.PatientPage);model.Dispose();Pump(window);Assert.Null(model.Session.Data);Assert.Equal(0,second.Patients.ItemCount);
            }
            finally{window.Close();}
            return true;
        },Token);
    }
    [Fact]
    public async Task PatientPagesStayBoundedAndFilteringClearsAnExcludedPatientSelection()
    {
        await using var ui=HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"hl7-fhir-pages-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            using var documents=new DocumentManager();using var model=new SyntheticPatientsViewModel(documents);
            var entries=new JsonArray();
            for(var n=0;n<205;n++)entries.Add(new JsonObject{["resource"]=new JsonObject{["resourceType"]="Patient",["id"]=$"syn-{n:D3}",["name"]=new JsonArray(new JsonObject{["family"]=$"SYNTHETIC {n:D3}"})}});
            var path=Path.Combine(root,"synthetic-pages.json");await File.WriteAllTextAsync(path,new JsonObject{["resourceType"]="Bundle",["type"]="collection",["entry"]=entries}.ToJsonString(),Token);
            var view=new SyntheticPatientsView(model);var window=new Window{Content=view,Width=900,Height=780};window.Show();
            try
            {
                await model.Session.ImportFilesAsync([path],true,Token);Pump(window);Assert.Equal(100,view.Patients.ItemCount);Assert.Equal(3,model.Session.PatientPageCount);
                view.Patients.SelectedItem=model.Session.PatientPage[0];Assert.NotNull(model.Session.PatientKey);
                Click(view.PatientsNext);Pump(window);Assert.Equal(100,view.Patients.ItemCount);Assert.NotNull(model.Session.PatientKey);
                Click(view.PatientsNext);Pump(window);Assert.Equal(5,view.Patients.ItemCount);Assert.False(view.PatientsNext.IsEnabled);
                view.Patients.SelectedItem=model.Session.PatientPage[^1];view.Search.Text="syn-000";Click(view.Find);await view.ActionTask;Pump(window);
                Assert.Single(model.Session.PatientPage);Assert.Null(model.Session.PatientKey);Assert.False(view.Generate.IsEnabled);Assert.Equal("",view.Details.Text);
                Assert.IsType<VirtualizingStackPanel>(view.Patients.GetVisualDescendants().OfType<VirtualizingStackPanel>().Single());
            }
            finally{window.Close();Directory.Delete(root,true);}
            return true;
        },Token);
    }
    [Fact]
    public async Task ReplacedImportCannotBeRetargetedByDetachedPatientEncounterOrTimelineObjects()
    {
        await using var ui=HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async()=>
        {
            using var documents=new DocumentManager();using var model=new SyntheticPatientsViewModel(documents);
            await model.Session.ImportFilesAsync([Fixture],true,Token);var first=new SyntheticPatientsView(model);var window=new Window{Content=first,Width=900,Height=780};window.Show();Pump(window);
            try
            {
                first.Patients.SelectedItem=model.Session.PatientPage[0];var patient=model.Session.PatientPage[0];var encounter=model.Session.EncounterPage[0];var clinical=model.Session.TimelinePage[0];
                window.Content=new TextBlock();Pump(window);await model.Session.ImportFilesAsync([Fixture],true,Token);
                // Resource integers are intentionally session-local and reused. A stale view
                // must match the current immutable object, rather than just that integer.
                first.Patients.ItemsSource=new[]{patient};first.Patients.SelectedItem=patient;Assert.Null(model.Session.PatientKey);
                var second=new SyntheticPatientsView(model);window.Content=second;Pump(window);second.Patients.SelectedItem=model.Session.PatientPage[0];
                first.Encounters.ItemsSource=new[]{encounter};first.Encounters.SelectedItems!.Add(encounter);
                first.Timeline.ItemsSource=new[]{clinical};first.Timeline.SelectedItems!.Add(clinical);
                Assert.Empty(model.Session.SelectedEncounters);Assert.Empty(model.Session.SelectedEvents);Assert.Null(model.Session.Preview);
            }
            finally{window.Close();}
            return true;
        },Token);
    }
    [Fact]
    public async Task ActualFileAndViewMenusShareOneToolAndCloseOrResetReleasesClinicalData()
    {
        await using var ui=HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"hl7-fhir-shell-"+Guid.NewGuid().ToString("N"));var window=new MainWindow(root);window.Show();
            try
            {
                var file=window.FindControl<MenuItem>("ImportSyntheticPatientsMenu")!;var menu=window.FindControl<MenuItem>("SyntheticPatientsMenu")!;
                file.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Pump(window);var dock=window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!;var factory=Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory);
                var tool=factory.GetDockable<ITool>(WorkspaceDockFactory.SyntheticPatientsId)!;var first=Assert.IsType<SyntheticPatientsViewModel>(tool.Context);await first.Session.ImportFilesAsync([Fixture],true,Token);await first.Session.FilterPatientsAsync("SYN-ALPHA",Token);
                menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert.Same(first,tool.Context);var layout=LayoutCodec.Encode(factory.Capture());Assert.DoesNotContain("SYN-ALPHA",layout);Assert.DoesNotContain("EXAMPLE",layout);Assert.DoesNotContain(first.Session.Data!.Id.ToString(),layout);
                factory.CloseDockable(tool);Assert.Null(first.Session.Data);Assert.Equal("",first.Session.PatientFilter);menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Pump(window);var next=Assert.IsType<SyntheticPatientsViewModel>(tool.Context);Assert.NotSame(first,next);Assert.Null(next.Session.Data);
                await next.Session.ImportFilesAsync([Fixture],true,Token);factory.CloseFloatingWindows();dock.Layout=factory.CreateLayout();Assert.Null(next.Session.Data);
            }
            finally{window.Close();if(Directory.Exists(root))Directory.Delete(root,true);}
            return true;
        },Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DockedAndFloatingPlacementRetainsNoImportedPatientOrSearchState(bool floating)
    {
        using var documents=new DocumentManager();var tools=new ToolWindowRegistry();tools.Register(new(WorkspaceDockFactory.BrowserId,"Browser",()=>new object()));
        tools.Register(new(WorkspaceDockFactory.SyntheticPatientsId,"FHIR Patient Workspace",()=>new SyntheticPatientsViewModel(documents),true,true));
        using var factory=new WorkspaceDockFactory(documents,tools){HostWindowLocator=new(){[nameof(IDockWindow)]=()=>new FakeHostWindow()}};factory.CreateLayout();factory.ShowTool(WorkspaceDockFactory.SyntheticPatientsId);
        var tool=factory.GetDockable<ITool>(WorkspaceDockFactory.SyntheticPatientsId)!;var first=Assert.IsType<SyntheticPatientsViewModel>(tool.Context);await first.Session.ImportFilesAsync([Fixture],true,Token);await first.Session.FilterPatientsAsync("SYN-ALPHA",Token);
        if(floating)factory.FloatDockable(tool);var layout=factory.Capture();var encoded=LayoutCodec.Encode(layout);Assert.DoesNotContain("SYN-ALPHA",encoded);Assert.DoesNotContain("EXAMPLE",encoded);Assert.DoesNotContain("patient-alpha",encoded);
        factory.CloseFloatingWindows();factory.CreateLayout();Assert.Null(first.Session.Data);factory.Restore(LayoutCodec.Decode(encoded),[]);factory.ShowTool(WorkspaceDockFactory.SyntheticPatientsId);var next=Assert.IsType<SyntheticPatientsViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.SyntheticPatientsId)!.Context);Assert.Null(next.Session.Data);
        Assert.Equal(floating,Assert.Single(factory.Capture().ToolPlacements!,p=>p.Id==WorkspaceDockFactory.SyntheticPatientsId).Floating is not null);
        factory.CloseFloatingWindows();
    }
}
