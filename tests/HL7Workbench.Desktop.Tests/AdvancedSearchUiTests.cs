using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class AdvancedSearchUiTests
{
    private const string First = "MSH|^~\\&|SYN||||||ADT^A01|SYN-CONTROL-A|T|2.5\rPID|1||SYN-ONE\r";
    private const string Second = "MSH|^~\\&|SYN||||||ORU^R01|SYN-CONTROL-B|T|2.5\rPID|1||SYN-TWO\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-advanced-search-ui-" + Guid.NewGuid().ToString("N"));
        public string Searches => Path.Combine(Root, "Searches");
        public Files() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }
    private static async Task<MessageDocument> Open(DocumentManager documents, string text = First + Second)
    { using var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)); return await MessageFileReader.OpenFileAsync(documents, "Synthetic input.hl7", bytes, Token); }
    private static void Key(Control control, Avalonia.Input.Key key) => control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
    private static SearchConditionEditor Row(SearchResultsView view, int index = 0) => (SearchConditionEditor)view.ConditionRows.Children[index];
    [Theory]
    [InlineData(false, 400)] [InlineData(true, 900)]
    public async Task NestedBuilderRetainsMeaningOnCollapseAndInvalidatesOnStructuralEdits(bool dark, int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var document = await Open(documents); using var model = new SearchViewModel(documents);
            model.Value = "SYN-ONE"; model.BuilderMode = SearchBuilderMode.Advanced;
            var view = new SearchResultsView(model); var window = new Window { Content = view, Width = width, Height = 600, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                var root = view.RootEditor; Assert.False(root.RemoveGroup.IsVisible);
                root.AddGroup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Contains("empty", view.Status.Text!, StringComparison.OrdinalIgnoreCase);
                var child = Assert.Single(root.Rows.Children.OfType<SearchGroupEditor>()); child.Combination.SelectedIndex = 1;
                child.AddCondition.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); var leaf = Assert.Single(child.Rows.Children.OfType<SearchConditionEditor>());
                leaf.Target.SelectedIndex = (int)SearchTarget.Segment; leaf.Path.Text = "PV1"; Assert.False(leaf.Value.IsVisible); Assert.False(leaf.CaseSensitive.IsVisible);
                Assert.Null(leaf.Validation.Text); Assert.Null(model.ValidationError);
                leaf.Operator.SelectedIndex = 1; model.SearchCommand.Execute(null); await model.SearchingTask; Assert.Single(model.Session.Page); model.Session.FilterNavigator = true;
                var completed = model.SearchingTask; var result = model.Session.Results;
                child.Expansion.IsExpanded = false; Assert.Same(result, model.Session.Results); Assert.Same(completed, model.SearchingTask); Assert.True(model.Session.FilterNavigator);
                window.Content = view = new SearchResultsView(model); root = view.RootEditor; child = Assert.Single(root.Rows.Children.OfType<SearchGroupEditor>());
                Assert.False(child.Expansion.IsExpanded); child.Expansion.IsExpanded = true;
                child.AddGroup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Null(model.Session.Results); Assert.Null(document.NavigationMatches); Assert.Same(completed, model.SearchingTask);
                var level3 = Assert.Single(child.Rows.Children.OfType<SearchGroupEditor>()); level3.AddGroup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var level4 = Assert.Single(level3.Rows.Children.OfType<SearchGroupEditor>()); Assert.False(level4.AddGroup.IsEnabled);
                level4.AddCondition.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(3, model.Conditions.Count);
                level3.RemoveGroup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(2, model.Conditions.Count);
                child.RemoveGroup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Single(model.Conditions); Assert.Empty(model.RootGroup.Children.OfType<SearchGroupViewModel>());
                model.RemoveGroup(model.RootGroup); Assert.Single(model.RootGroup.Children); Assert.Null(model.ValidationError);
                for (var i = 0; i < 15; i++) model.AddGroup(model.RootGroup);
                Assert.False(view.RootEditor.AddGroup.IsEnabled); Assert.Equal(15, model.RootGroup.Children.OfType<SearchGroupViewModel>().Count());
                window.UpdateLayout(); Assert.True(view.RootEditor.Bounds.Width <= view.Bounds.Width); Assert.Equal(window.ActualThemeVariant, view.ActualThemeVariant);
                model.Dispose(); Assert.Empty(model.RootGroup.Children); Assert.Empty(model.Conditions);
            }
            finally { window.Close(); documents.Close(document.Id, true); await document.Disposal; }
            return true;
        }, Token);
    }
    [Fact]
    public async Task InspectorContextMenusCopyOnlyValidStructuralPathsIncludingSubcomponents()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var document = await Open(documents, First + "PID|2||A&B^MR~C&D\rBAD opaque\r");
            var inspection = document.ActiveInspection!; inspection.SelectSegment(inspection.Segments.Last(s => s.Identifier == "PID"));
            var field = inspection.Fields.Single(n => n.Path.Field == 3); var component = field.Children[0].Children[0]; var subcomponent = component.Children[1];
            var view = new InspectionPanes(inspection, document, InspectionPaneKind.Inspector); var window = new Window { Content = view, Width = 700, Height = 650 }; window.Show();
            try
            {
                foreach (var (node, expected) in new[] { (field, "PID-3"), (component, "PID-3.1"), (subcomponent, "PID-3.1.2") })
                {
                    inspection.SelectNode(node); view.Inspector.SelectedItem = node;
                    view.Inspector.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
                    Assert.True(view.CopyPath.IsEnabled); Assert.Equal("Copy Path", view.CopyPath.Header);
                    view.CopyPath.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await view.CopyPathTask;
                    Assert.Equal(expected, await window.Clipboard!.TryGetTextAsync());
                    Assert.Null(new SearchCondition(SearchTarget.Field, SearchOperator.Exists, expected).ValidationError());
                }
                view.SegmentPathHeader.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
                Assert.True(view.CopySegmentPath.IsEnabled); view.CopySegmentPath.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await view.CopyPathTask;
                Assert.Equal("PID", await window.Clipboard!.TryGetTextAsync());
                inspection.InspectField(999); Assert.Null(InspectionSearchPath.For(inspection.SelectedNode)); Assert.Null(InspectionSearchPath.For(new object()));
                view.Inspector.SelectedItem = null; view.Inspector.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent }); Assert.False(view.CopyPath.IsEnabled);
                Assert.Null(InspectionSearchPath.For(inspection.Segments.Last()));
                document.IsEditMode = true; document.ReplaceWorkingText(document.EditableText! + "\r");
                view.SegmentPathHeader.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent }); Assert.False(view.CopySegmentPath.IsEnabled);
                view.CopySegmentPath.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await view.CopyPathTask; Assert.Equal("PID", await window.Clipboard!.TryGetTextAsync());
            }
            finally { window.Close(); documents.Close(document.Id, true); await document.Disposal; }
            return true;
        }, Token);
    }
    [Fact]
    public async Task ThemePreferencesApplyImmediatelyAcrossWindowsAndRetainExplicitApplyCancelSemantics()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var files = new Files(); var path = Path.Combine(files.Root, "editor-preferences.json"); var preferences = new ApplicationPreferences(path);
            Assert.Equal(ApplicationThemePreference.FollowSystem, preferences.Current.Theme);
            var main = new Window { Content = new TextBox() }; var floating = new Window { Content = new Button { Content = "Synthetic" } }; main.Show(); floating.Show();
            var settings = new PreferencesWindow(preferences); settings.Show();
            try
            {
                Assert.Equal(0, settings.ThemeSelection.SelectedIndex); settings.Categories.SelectedIndex = 2; Assert.True(settings.ThemeSelection.IsEffectivelyVisible);
                foreach (var (index, variant) in new[] { (1, ThemeVariant.Light), (2, ThemeVariant.Dark), (1, ThemeVariant.Light) })
                {
                    settings.ThemeSelection.SelectedIndex = index; Assert.True(settings.Apply()); Dispatcher.UIThread.RunJobs();
                    Assert.Equal(variant, Application.Current!.RequestedThemeVariant); Assert.Equal(variant, main.ActualThemeVariant); Assert.Equal(variant, floating.ActualThemeVariant); Assert.Equal(variant, settings.ActualThemeVariant);
                    Assert.Equal((ApplicationThemePreference)index, EditorPreferences.Load(path).Theme);
                }
                settings.ThemeSelection.SelectedIndex = 0; Assert.True(settings.Apply()); Assert.Equal(ThemeVariant.Default, Application.Current!.RequestedThemeVariant);
                settings.ThemeSelection.SelectedIndex = 2; settings.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(ApplicationThemePreference.FollowSystem, EditorPreferences.Load(path).Theme); Assert.Equal(ThemeVariant.Default, Application.Current!.RequestedThemeVariant);
            }
            finally { settings.Close(); main.Close(); floating.Close(); ApplicationTheme.Apply(ApplicationThemePreference.FollowSystem); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false, 900)] [InlineData(true, 900)] [InlineData(false, 400)] [InlineData(true, 400)]
    public async Task BuilderSwitchesEditsOperatorsKeyboardAndClearStayPredictable(bool dark, int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var document = await Open(documents); using var model = new SearchViewModel(documents);
            var view = new SearchResultsView(model); var window = new Window { Content = view, Width = width, Height = 650, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Assert.Equal(0, view.BuilderMode.SelectedIndex); view.Value.Text = "SYN-ONE"; Key(view.Value, Avalonia.Input.Key.Enter); await model.SearchingTask;
                Assert.Equal(1, model.Session.ResultCount); view.Filter.IsChecked = true; var completed = model.SearchingTask;
                view.BuilderMode.SelectedIndex = 1; Assert.Equal(SearchBuilderMode.Advanced, model.BuilderMode);
                Assert.Null(model.Session.Results); Assert.Null(document.NavigationMatches); Assert.Same(completed, model.SearchingTask); Assert.False(model.Session.IsBusy);
                var first = Row(view); first.Target.SelectedIndex = 1; first.Path.Text = "PID-3.1"; first.Operator.SelectedIndex = 1; first.Value.Text = "SYN-ONE";
                Assert.Equal(SearchOperator.Equals, model.Conditions[0].Operator);
                view.AddCondition.Command!.Execute(null); var second = Row(view, 1); second.Target.SelectedIndex = 2; second.Path.Text = "AL1"; second.Operator.SelectedIndex = 1;
                Assert.False(second.Value.IsVisible); Assert.False(second.CaseSensitive.IsVisible); Assert.True(second.Path.IsVisible);
                view.Combination.SelectedIndex = 1; Assert.Equal(SearchCombination.Any, model.Combination);
                Key(first.Value, Avalonia.Input.Key.Enter); await model.SearchingTask; Assert.Equal(2, model.Session.ResultCount);
                var anyCompleted = model.SearchingTask;
                view.Combination.SelectedIndex = 0; Assert.Null(model.Session.Results); Assert.Same(anyCompleted, model.SearchingTask);
                Key(first.Value, Avalonia.Input.Key.Enter); await model.SearchingTask; Assert.Equal(1, model.Session.ResultCount);
                Assert.Equal(2, Assert.Single(model.Session.Page).MatchCount);
                view.Filter.IsChecked = true; first.Value.Text = "syn-one"; first.CaseSensitive.IsChecked = true;
                Assert.Null(model.Session.Results); Assert.Null(document.NavigationMatches); Key(first.Value, Avalonia.Input.Key.Enter); await model.SearchingTask; Assert.Equal(0, model.Session.ResultCount);
                first.CaseSensitive.IsChecked = false; Key(first.Value, Avalonia.Input.Key.Enter); await model.SearchingTask; Assert.Equal(1, model.Session.ResultCount);
                view.Clear.Command!.Execute(null); Assert.Equal("syn-one", first.Value.Text); Assert.Equal(2, model.Conditions.Count); Assert.Null(model.Session.Results);
                model.NotifyShown(); Dispatcher.UIThread.RunJobs(); Assert.True(first.Value.IsFocused);
                Assert.Equal("Condition 1 value", AutomationProperties.GetName(first.Value)); Assert.True(first.Value.Focusable); Assert.True(first.Target.Focusable);
                window.UpdateLayout(); Assert.True(first.Bounds.Width <= view.Bounds.Width);
                Assert.Equal(window.ActualThemeVariant, view.ActualThemeVariant);
                second.Remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Single(model.Conditions);
                first.Remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Empty(model.Conditions); Assert.Contains("1 and 16", view.Status.Text!);
                model.SearchCommand.Execute(null); await model.SearchingTask; Assert.False(model.Session.IsBusy); Assert.Null(model.Session.Results);
                for (var i = 0; i < MessageSearchQuery.MaximumConditions; i++) model.AddConditionCommand.Execute(null);
                Assert.Equal(16, model.Conditions.Count); Assert.False(view.AddCondition.IsEffectivelyEnabled);
                window.Content = new SearchResultsView(model); Assert.Equal(16, model.Conditions.Count); Assert.Equal(SearchBuilderMode.Advanced, model.BuilderMode);
                Assert.False(document.IsDirty); Assert.False(document.IsEditMode);
            }
            finally { window.Close(); documents.Close(document.Id, true); await document.Disposal; }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LibraryExplicitSaveRestartLoadRenameUpdateDeleteNeverPersistDiscovery(bool advanced)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); var document = await Open(documents);
            var model = new SearchViewModel(documents, new(files.Searches)); await model.LibraryTask;
            Assert.Contains("unencrypted JSON", SearchViewModel.StorageDisclosure);
            var view = new SearchResultsView(model); var window = new Window { Content = view, Width = 850, Height = 650 }; window.Show();
            MessageDocument? other = null; SearchViewModel? restarted = null;
            try
            {
                Assert.False(Directory.Exists(files.Searches)); view.Value.Text = "SYN-ONE";
                if (advanced) view.BuilderMode.SelectedIndex = 1;
                model.SearchCommand.Execute(null); await model.SearchingTask; view.Filter.IsChecked = true;
                var results = model.Session.Results; var searchTask = model.SearchingTask;
                view.LibraryPanel.IsExpanded = true; view.SearchName.Text = "Synthetic search"; Assert.True(view.SaveSearch.IsEffectivelyEnabled);
                view.SaveSearch.Command!.Execute(null); await model.LibraryTask;
                Assert.Single(model.SavedSearches); Assert.Same(results, model.Session.Results); Assert.True(model.Session.FilterNavigator); Assert.Same(searchTask, model.SearchingTask);
                var id = model.SelectedSearchId!.Value; var path = Path.Combine(files.Searches, id.ToString("N") + ".json");
                var json = await File.ReadAllTextAsync(path, Token);
                Assert.DoesNotContain("SYN-CONTROL", json); Assert.DoesNotContain("Synthetic input", json); Assert.DoesNotContain("MessageNumber", json); Assert.DoesNotContain("Results", json);
                model.Dispose(); restarted = new(documents, new(files.Searches)); await restarted.LibraryTask;
                Assert.Contains("locally", restarted.LibraryStatus);
                view = new(restarted); window.Content = view; view.LibraryPanel.IsExpanded = true;
                var idle = restarted.SearchingTask; view.SavedSearches.SelectedIndex = 0;
                Assert.Empty(restarted.Value); Assert.Null(restarted.Session.Results); Assert.Same(idle, restarted.SearchingTask);
                view.LoadSearch.Command!.Execute(null); await restarted.LibraryTask;
                Assert.Equal(advanced ? SearchBuilderMode.Advanced : SearchBuilderMode.Simple, restarted.BuilderMode);
                Assert.Same(idle, restarted.SearchingTask); Assert.False(restarted.Session.IsBusy); Assert.Null(restarted.Session.Results);
                other = await Open(documents, Second + First); restarted.SearchCommand.Execute(null); await restarted.SearchingTask;
                Assert.Equal(2, Assert.Single(restarted.Session.Page).MessageNumber);
                view.SearchName.Text = "Synthetic renamed"; view.RenameSearch.Command!.Execute(null); await restarted.LibraryTask;
                Assert.Equal("Synthetic renamed", new SearchLibrary(files.Searches).Open(id).Name);
                if (advanced) Row(view).Value.Text = "SYN-TWO"; else view.Value.Text = "SYN-TWO";
                Assert.Null(restarted.Session.Results); view.UpdateSearch.Command!.Execute(null); await restarted.LibraryTask;
                var saved = new SearchLibrary(files.Searches).Open(id); Assert.Equal("SYN-TWO", advanced ? saved.Query.Conditions[0].Value : saved.Query.Value);
                // Editing alone must not update the saved record.
                if (advanced) Row(view).Value.Text = "SYN-UNSAVED"; else view.Value.Text = "SYN-UNSAVED";
                Assert.Equal(saved.Revision, new SearchLibrary(files.Searches).Open(id).Revision);
                restarted.ConfirmDeleteAsync = _ => Task.FromResult(false); view.DeleteSearch.Command!.Execute(null); await restarted.LibraryTask; Assert.True(File.Exists(path));
                restarted.ConfirmDeleteAsync = _ => Task.FromResult(true); view.DeleteSearch.Command!.Execute(null); await restarted.LibraryTask;
                Assert.False(File.Exists(path)); Assert.Empty(restarted.SavedSearches); Assert.Equal("SYN-UNSAVED", advanced ? restarted.Conditions[0].Value : restarted.Value);
                Assert.False(document.IsDirty); Assert.False(other.IsDirty); Assert.Empty(Directory.GetFiles(files.Searches, "*.tmp"));
            }
            finally
            {
                window.Close(); model.Dispose(); restarted?.Dispose();
                foreach (var doc in documents.Documents.ToArray()) { documents.Close(doc.Id, true); await doc.Disposal; }
            }
            return true;
        }, Token);
    }
    [Fact]
    public async Task StorageDisclosureIsNonmodalAndDeleteDefaultsToSafeCancellation()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var owner = new Window(); owner.Show();
            try
            {
                foreach (var dialog in new[] { ConfirmationDialog.DeleteSearch("Synthetic") })
                {
                    var pending = dialog.ShowDialog<bool>(owner); Dispatcher.UIThread.RunJobs();
                    var cancel = dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel")); Assert.True(cancel.IsFocused);
                    Assert.DoesNotContain("Do not save PHI", SearchViewModel.StorageDisclosure);
                    Key(dialog, Avalonia.Input.Key.Escape); Assert.False(await pending);
                }
            }
            finally { owner.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task CanceledStorageAndCorruptRecordsDoNotCreateHistoryOrLeakValues()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); var document = await Open(documents);
            using var model = new SearchViewModel(documents, new(files.Searches)); await model.LibraryTask;
            model.Value = "SYN-PRIVATE-VALUE"; model.SearchName = "Synthetic";
            Assert.False(Directory.Exists(files.Searches)); Assert.Empty(model.SavedSearches);
            await model.SaveSearchAsync();
            await model.SaveSearchAsync(); Assert.Contains("name", model.LibraryStatus); Assert.DoesNotContain("PRIVATE", model.LibraryStatus);
            var bad = Path.Combine(files.Searches, Guid.NewGuid().ToString("N") + ".json"); await File.WriteAllTextAsync(bad, "{SYN-PRIVATE-BROKEN", Token);
            using var restarted = new SearchViewModel(documents, new(files.Searches)); await restarted.LibraryTask;
            Assert.Single(restarted.SavedSearches); Assert.Contains("1 malformed", restarted.LibraryStatus); Assert.DoesNotContain("PRIVATE", restarted.LibraryStatus);
            documents.Close(document.Id, true); await document.Disposal; return true;
        }, Token);
    }
    [Fact]
    public async Task AdvancedQueryAndLibraryRemainSessionStateAcrossDockFloatRestoreAndReset()
    {
        using var files = new Files(); using var documents = new DocumentManager(); var document = await Open(documents);
        var preferencesPath = Path.Combine(files.Root, "editor-preferences.json");
        using var model = new SearchViewModel(documents, new(files.Searches), new(preferencesPath)); await model.LibraryTask;
        model.SaveBuilderFraction(.78);
        model.Value = "SYN-PRIVATE-QUERY"; model.BuilderMode = SearchBuilderMode.Advanced;
        var registry = new ToolWindowRegistry(); registry.Register(new(WorkspaceDockFactory.SearchResultsId, "Find Messages", () => model, Optional: true, RestoreVisibility: true));
        using var factory = new WorkspaceDockFactory(documents, registry) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.SearchResultsId); var tool = factory.GetDockable<ITool>("tool.search-results")!;
        factory.FloatDockable(tool); var encoded = LayoutCodec.Encode(factory.Capture()); Assert.DoesNotContain("PRIVATE", encoded); Assert.False(tool.CanDockAsDocument);
        factory.CloseFloatingWindows(); factory.Restore(LayoutCodec.Decode(encoded), [new(0, 0, 1920, 1080)]);
        Assert.Same(model, factory.GetDockable<ITool>("tool.search-results")!.Context); Assert.Equal(SearchBuilderMode.Advanced, model.BuilderMode);
        factory.CloseFloatingWindows(); factory.CreateLayout(); factory.ShowTool("tool.search-results"); Assert.Equal("SYN-PRIVATE-QUERY", model.Conditions[0].Value);
        Assert.Equal(.78, model.BuilderFraction); Assert.Equal(.78, EditorPreferences.Load(preferencesPath).SearchLayout!.AdvancedBuilderFraction);
        Assert.False(Directory.Exists(files.Searches)); documents.Close(document.Id, true); await document.Disposal;
    }
}
