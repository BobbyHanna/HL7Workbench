using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class InspectorRedesignViewTests
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261030000000||ADT^A01|SYN-1|T|2.5.1\r";
    private static void Pump(Window w) { w.UpdateLayout(); Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    [Fact]
    public async Task ShellPrepopulatesExistingExtractionWithoutRunningOrChangingSource()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-inspector-shell-" + Guid.NewGuid().ToString("N"));
            var window = new MainWindow(root); window.Show();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var manager = (DocumentManager)typeof(MainWindow).GetField("documents", flags)!.GetValue(window)!;
            var registry = (ToolWindowRegistry)typeof(MainWindow).GetField("registry", flags)!.GetValue(window)!;
            var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse("PID|Example^Avery~Other^Synthetic")); manager.Open(doc);
            try
            {
                var field = doc.Inspection.InspectField(1)!; var component = field.VisibleChildren[1].VisibleChildren[1]; doc.Inspection.SelectNode(component);
                window.RaiseEvent(new InspectionActionRequestEventArgs(new(doc, doc.Inspection, component), InspectorAction.Extract));
                await window.InspectorActionTask;
                var model = Assert.IsType<FieldExtractionViewModel>(registry.GetContent(HL7Workbench.Desktop.Docking.WorkspaceDockFactory.FieldExtractionId));
                Assert.Equal("PID[1]-1[2].2", Assert.Single(model.Draft.Columns).Path); Assert.False(model.Draft.DecodeEscapes);
                Assert.Equal(ExtractionMode.ExpandMatches, model.Draft.Mode); Assert.Same(doc, model.Session.Source);
                Assert.Null(model.Session.Results); Assert.False(doc.IsDirty); Assert.Equal("PID|Example^Avery~Other^Synthetic", doc.EditableText);
            }
            finally { window.Close(); await doc.Disposal; if (Directory.Exists(root)) Directory.Delete(root, true); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Theory]
    [InlineData(360)] [InlineData(1100)]
    public async Task SegmentRowsBlankToggleSchemaAbsenceAndDefaultExpansionUseOneExistingInspector(int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Header + "PID|1||ID||Example^Avery\rPID|2||SECOND\r"));
            var panes = new InspectionPanes(doc.Inspection, doc, InspectionPaneKind.Inspector);
            var window = new Window { Width = width, Height = 800, Content = panes }; window.Show();
            try
            {
                doc.Inspection.SelectSegment(doc.Inspection.Segments[1]); doc.Inspection.InspectField(5); Pump(window);
                Assert.DoesNotContain(panes.Inspector.Items.Cast<InspectionNode>(), n => n.Path.Field == 2);
                var root = Assert.IsType<TreeViewItem>(panes.Inspector.ContainerFromItem(doc.Inspection.Fields[4])); Assert.True(root.IsExpanded);
                Assert.Contains("PID[1]", Assert.IsType<TextBlock>(panes.SegmentPathHeader).Text);
                await doc.ApplySchemaAsync("hl7.v251.ADT.A01"); panes.UpdateInspection(doc.Inspection);
                panes.ShowBlankFields.IsChecked = true; Pump(window);
                var absent = panes.Inspector.Items.Cast<InspectionNode>().First(n => !n.HasContent);
                Assert.Null(absent.Range64); panes.PrepareActions(absent); Assert.False(panes.CopyValue.IsEnabled); Assert.True(panes.ExtractField.IsEnabled);
                Assert.Contains("Absent", panes.ActionStatus.Text);
                doc.Inspection.SelectSegment(doc.Inspection.Segments[2]); Pump(window);
                Assert.All(panes.Inspector.Items.Cast<InspectionNode>(), n => Assert.Equal(2, n.Owner.Occurrence));
                Assert.False(doc.IsDirty); Assert.Contains("Example^Avery", doc.EditableText);
            }
            finally { window.Close(); doc.Dispose(); await doc.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task ContextCopyAndActionsAddressComponentAndRejectChangedSelection()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Header + "PID|||ID||Example^Avery\\F\\Synthetic\r"));
            var panes = new InspectionPanes(doc.Inspection, doc, InspectionPaneKind.Inspector);
            var window = new Window { Content = panes, Width = 800, Height = 700 }; window.Show();
            try
            {
                doc.Inspection.SelectSegment(doc.Inspection.Segments[1]); var field = doc.Inspection.InspectField(5)!;
                var component = field.VisibleChildren[1]; panes.PrepareActions(component);
                Assert.Same(component, doc.Inspection.SelectedNode); Assert.True(panes.CopyValue.IsEnabled); Assert.True(panes.FilterValue.IsEnabled);
                panes.CopyValue.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await panes.CopyPathTask;
                Assert.Equal("Avery\\F\\Synthetic", await window.Clipboard!.TryGetTextAsync());
                panes.CopyLocation.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await panes.CopyPathTask;
                Assert.Equal("PID[1]-5[1].2", await window.Clipboard!.TryGetTextAsync());
                InspectionActionRequestEventArgs? request = null;
                panes.AddHandler(ViewerRequests.InspectionActionEvent, (_, e) => request = e);
                panes.FindOccurrences.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Assert.Same(component, request!.Context.Node);
                request = null; doc.Inspection.SelectNode(field);
                panes.ExtractField.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Assert.Null(request);
                Assert.False(doc.IsDirty);
            }
            finally { window.Close(); doc.Dispose(); await doc.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task ExistingSearchToolFiltersWholeFileAtSelectedComponentAndCancelsCleanly()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var manager = new DocumentManager();
            using var source = new MemoryStream(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 220).Select(i => Header + "PID|||ID||Example^" + (i % 2 == 0 ? "Avery" : "Other") + "\r"))));
            var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", source, TestContext.Current.CancellationToken);
            using var search = new SearchViewModel(manager);
            try
            {
                var inspection = doc.ActiveInspection!; inspection.SelectSegment(inspection.Segments[1]); var field = inspection.InspectField(5)!;
                var component = field.VisibleChildren[1]; inspection.SelectNode(component); var context = new InspectionActionContext(doc, inspection, component);
                await search.StartFromInspectorAsync(context, true, TestContext.Current.CancellationToken);
                Assert.Equal(110, search.Session.ResultCount); Assert.True(search.Session.FilterNavigator);
                Assert.Contains("entire active file", search.ContextDescription); Assert.Contains("case-sensitive", search.ContextDescription);
                Assert.All(search.Session.Results!.Matches, m => Assert.Contains(".2", m.Location));
                using var canceled = new CancellationTokenSource(); canceled.Cancel();
                await search.StartFromInspectorAsync(context, false, canceled.Token);
                Assert.False(search.Session.FilterNavigator); Assert.Null(search.Session.Results); Assert.False(doc.IsDirty);
            }
            finally { manager.Close(doc.Id, true); await doc.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task ActiveDocumentsAndReplacedDockViewsDoNotShareInspectorSelection()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var manager = new DocumentManager();
            var first = new MessageDocument("first", "Synthetic", Hl7Parser.Parse("PID|FIRST^ONE"));
            var second = new MessageDocument("second", "Synthetic", Hl7Parser.Parse("PID|SECOND^TWO|THIRD^FOUR")); manager.Open(first); manager.Open(second);
            using var model = new InspectionToolViewModel(manager, false);
            var host = new InspectionToolHost(model); var window = new Window { Content = host, Width = 700, Height = 650 }; window.Show();
            try
            {
                Pump(window); var panes = Assert.IsType<InspectionPanes>(host.Content); Assert.Same(second.Inspection, panes.Inspection);
                second.Inspection.InspectField(1); second.Inspection.ExpandedInspectorPaths.Add(second.Inspection.Fields[0].Path);
                second.Inspection.ExpandedInspectorPaths.Add(second.Inspection.Fields[1].Path);
                window.Content = null; var floating = new Window { Content = new InspectionToolHost(model), Width = 600, Height = 500 }; floating.Show();
                try
                {
                    Pump(floating); var rebuilt = Assert.IsType<InspectionPanes>(Assert.IsType<InspectionToolHost>(floating.Content).Content);
                    Assert.Same(second.Inspection.SelectedNode, rebuilt.Inspector.SelectedItem);
                    Assert.True(Assert.IsType<TreeViewItem>(rebuilt.Inspector.ContainerFromItem(second.Inspection.Fields[1])).IsExpanded);
                    manager.Activate(first.Id); Pump(floating);
                    Assert.Same(first.Inspection, Assert.IsType<InspectionPanes>(Assert.IsType<InspectionToolHost>(floating.Content).Content).Inspection);
                    Assert.Equal("SECOND^TWO", second.Inspection.SelectedNode!.Raw);
                }
                finally { floating.Close(); }
            }
            finally { window.Close(); manager.Dispose(); await Task.WhenAll(first.Disposal, second.Disposal); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
