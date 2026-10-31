using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class SyntheticGenerationViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    [Fact]
    public void DefaultsAndThousandMessagePreviewAreFixedAndDoNotGenerateOutput()
    {
        using var documents = new DocumentManager(); var model = new SyntheticGenerationViewModel(documents, null);
        Assert.Equal(SyntheticMessageType.AdtA01, model.MessageType); Assert.Equal("1", model.Count); Assert.Equal("", model.Seed);
        Assert.Equal("2030-01-01T00:00:00+00:00", model.Timestamp); Assert.Equal("SYN", model.Prefix); Assert.False(model.InsertIntoTarget);
        Assert.True(model.CanGenerate); Assert.False(model.HasTarget); model.Count = "1000";
        Assert.Contains("Seed: automatic", model.Preview); model.Seed = "42";
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var preview = model.Preview;
        Assert.Contains("ADT^A01", preview); Assert.Contains("1000 message", preview); Assert.Contains("New unsaved document", preview);
        Assert.Contains("SYN2-0000002A-0001", preview); Assert.Contains("SYN2-0000002A-1000", preview);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - allocated < 128 * 1024); Assert.Empty(documents.Documents);
        model.Seed = ""; Assert.True(model.CanGenerate); Assert.Contains("Seed: automatic", model.Preview); Assert.DoesNotContain("0000002A", model.Preview);
    }
    [Theory]
    [InlineData("count", "0")] [InlineData("count", "1001")] [InlineData("count", "1.5")] [InlineData("count", "-1")]
    [InlineData("seed", "4294967296")] [InlineData("seed", "-1")] [InlineData("prefix", "SYN|INJECT")]
    [InlineData("prefix", "")] [InlineData("timestamp", "2030-01-01T00:00:00")] [InlineData("timestamp", "1800-01-01T00:00:00Z")]
    public async Task InvalidConfigurationDisablesGenerateAndPublishesNothing(string field, string value)
    {
        using var documents = new DocumentManager(); var model = new SyntheticGenerationViewModel(documents, null);
        switch (field) { case "count": model.Count = value; break; case "seed": model.Seed = value; break; case "prefix": model.Prefix = value; break; default: model.Timestamp = value; break; }
        Assert.False(model.CanGenerate); Assert.NotEmpty(model.Preview); await model.GenerateAsync();
        Assert.Null(model.Result); Assert.NotEmpty(model.Status); Assert.Empty(documents.Documents);
    }
    private static async Task InShell(Func<MainWindow, WorkspaceDockFactory, DocumentManager, Task> test)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            Application.Current!.Styles.Add(new Dock.Avalonia.Themes.Fluent.DockFluentTheme());
            Application.Current.DataTemplates.Add(new DockContentTemplate { DockedInspection = true });
            var root = Path.Combine(Path.GetTempPath(), "hl7-generator-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var window = new MainWindow(root) { Width = 1500, Height = 1000 }; window.Show();
            var factory = Assert.IsType<WorkspaceDockFactory>(window.FindControl<DockControl>("WorkspaceDock")!.Layout!.Factory);
            factory.ShowTool(WorkspaceDockFactory.ComparisonId);
            var documents = Assert.IsType<ComparisonViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!.Context).Documents;
            factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!);
            try { Pump(window); await test(window, factory, documents); }
            finally
            {
                var docs = documents.Documents.ToArray(); foreach (var doc in docs) documents.Close(doc.Id, discardChanges: true);
                window.Close(); await Task.WhenAll(docs.Select(d => d.Disposal)); Directory.Delete(root, true);
            }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task MenuAndHomeShareGeneratorCommandAndDialogProducesOneIndependentTab(bool pinned) => InShell(async (window, factory, documents) =>
    {
        var ribbon = Assert.IsType<UtilityRibbon>(window.FindControl<ContentControl>("RibbonHost")!.Content);
        if (pinned) Click(ribbon.Pin); ribbon.SelectTab(0); Pump(window);
        var host = pinned ? (Control)ribbon.PinnedHost.Content! : ribbon.TemporaryPopup.Child!;
        var button = host.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Generate Messages");
        var menu = window.FindControl<MenuItem>("GenerateMessagesMenu")!; Assert.Same(menu.Command, button.Command); Assert.True(menu.Command!.CanExecute(null));
        menu.Command.Execute(null); Pump(window);
        var dialog = window.OwnedWindows.OfType<SyntheticGenerationDialog>().Single();
        Assert.Equal("Generate Messages", dialog.Title); Assert.True(dialog.Generate.IsEnabled);
        Click(dialog.Generate);
        for (var attempt = 0; dialog.Model.IsBusy && attempt < 200; attempt++) await Task.Delay(10, Token);
        for (var attempt = 0; window.FocusManager!.GetFocusedElement() is not AvaloniaEdit.Editing.TextArea && attempt < 200; attempt++) await Task.Delay(10, Token);
        Pump(window); Assert.False(dialog.Model.IsBusy); Assert.NotNull(dialog.Model.Result);
        var doc = Assert.Single(documents.Documents); Assert.Equal("Synthetic ADT A01.hl7", doc.Title); Assert.Null(doc.SavePath); Assert.True(doc.IsDirty);
        var view = window.GetVisualDescendants().OfType<FileDocumentView>().Single(v => ReferenceEquals(v.Document, doc));
        Assert.True(doc.IsEditMode); Assert.Same(view.ActiveView!.Editor.TextArea, window.FocusManager!.GetFocusedElement());
        Assert.False(documents.Preferences.StartInEditMode);
    });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task CancelAndWindowDismissalDuringGenerationPublishNoTab(bool closeWindow) => InShell(async (window, factory, documents) =>
    {
        var dialog = new SyntheticGenerationDialog(documents); dialog.Model.Count = "1000";
        var completion = dialog.ShowDialog<SyntheticGenerationResult?>(window); Click(dialog.Generate); Assert.True(dialog.Model.IsBusy);
        if (closeWindow) dialog.Close(); else Click(dialog.CancelGeneration);
        Assert.Null(await completion); Assert.False(dialog.Model.IsBusy); Assert.Empty(documents.Documents); Assert.Null(dialog.Model.Result);
    });
    [Theory]
    [InlineData(MessageInsertionPosition.BeforeCurrent, 1)] [InlineData(MessageInsertionPosition.AfterCurrent, 2)]
    [InlineData(MessageInsertionPosition.Beginning, 1)] [InlineData(MessageInsertionPosition.End, 2)]
    public Task ExistingDestinationUsesSharedPositionsFocusAndRootHistoryAfterViewReplacement(MessageInsertionPosition position, int first) => InShell(async (window, factory, documents) =>
    {
        var doc = MessageFileReader.Open(documents, "Synthetic source", Hl7Parser.Parse("MSH|^~\\&|SYN-SOURCE\rPID|1\r")); factory.OpenDocument(doc.Id);
        var other = MessageFileReader.Open(documents, "Synthetic other", Hl7Parser.Parse("MSH|^~\\&|SYN-OTHER\r")); factory.OpenDocument(other.Id);
        documents.Activate(doc.Id);
        var model = new SyntheticGenerationViewModel(documents, doc) { Count = "3", InsertIntoTarget = true, Position = position };
        await model.GenerateAsync(); Assert.NotNull(model.Result); await doc.EditRefresh;
        await window.ActivateGeneratedResultAsync(model.Result); Pump(window);
        Assert.Equal(first, doc.MessageNumber); Assert.Equal(model.Result.MessageIds[0], doc.ActiveMessageId); Assert.Equal(4, doc.MessageCount); Assert.Single(other.Messages);
        var view = window.GetVisualDescendants().OfType<FileDocumentView>().Single(v => ReferenceEquals(v.Document, doc));
        Assert.Same(view.ActiveView!.Editor.TextArea, window.FocusManager!.GetFocusedElement());
        // Focus-independent root routing must remove exactly one entire generation.
        doc.IsEditMode = false; Pump(window);
        var navigator = window.GetVisualDescendants().OfType<InspectionPanes>().Single(p => p.Navigator.IsAttachedToVisualTree()).Navigator;
        Assert.True(Assert.IsType<ListBoxItem>(navigator.ContainerFromIndex(0)).Focus());
        window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null); window.KeyRelease(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
        await doc.EditRefresh; Pump(window); Assert.Single(doc.Messages); Assert.False(doc.CanUndo);
        window.KeyPress(Key.Y, RawInputModifiers.Control, PhysicalKey.Y, null); window.KeyRelease(Key.Y, RawInputModifiers.Control, PhysicalKey.Y, null);
        await doc.EditRefresh; Pump(window); Assert.Equal(4, doc.MessageCount); Assert.Equal(model.Result.MessageIds[0], doc.ActiveMessageId); Assert.False(doc.CanRedo);
        var panes = window.GetVisualDescendants().OfType<InspectionPanes>().Single(p => p.Inspector.IsAttachedToVisualTree());
        panes.ShowBlankFields.IsChecked = true;
        var inspector = panes.Inspector;
        Assert.Equal(doc.Inspection.Fields, inspector.ItemsSource);
        // Existing EOF and empty trailing draft reuse remain in the same command path.
        await doc.NavigateAsync(doc.MessageCount, Token); await view.NextAsync(); await doc.EditRefresh; Assert.Equal(5, doc.MessageCount);
        var blank = doc.ActiveMessageId; await view.NextAsync(); Assert.Equal(blank, doc.ActiveMessageId); Assert.Equal(5, doc.MessageCount);
    });
    [Fact]
    public Task InvalidInputAndCancelBeforeGenerateKeepDocumentsUntouched() => InShell(async (window, factory, documents) =>
    {
        var dialog = new SyntheticGenerationDialog(documents); var task = dialog.ShowDialog<SyntheticGenerationResult?>(window);
        dialog.Model.Count = "0"; Assert.False(dialog.Generate.IsEnabled); Click(dialog.CancelGeneration);
        Assert.Null(await task); Assert.Empty(documents.Documents);
    });
}
