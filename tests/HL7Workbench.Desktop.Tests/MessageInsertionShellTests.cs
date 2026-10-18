using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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

public sealed class MessageInsertionShellTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MenuAndRibbonShareActiveDocumentScopeAndDismissBeforeFocusingInsertedEditor(bool pinned)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            Application.Current!.Styles.Add(new Dock.Avalonia.Themes.Fluent.DockFluentTheme());
            Application.Current.DataTemplates.Add(new DockContentTemplate { DockedInspection = true });
            var root = Path.Combine(Path.GetTempPath(), "hl7-insertion-shell-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var window = new MainWindow(root) { Width = 1400, Height = 1000 }; window.Show();
            DocumentManager? manager = null;
            try
            {
                var factory = Assert.IsType<WorkspaceDockFactory>(window.FindControl<DockControl>("WorkspaceDock")!.Layout!.Factory);
                factory.ShowTool(WorkspaceDockFactory.ComparisonId);
                manager = Assert.IsType<ComparisonViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!.Context).Documents;
                factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!);
                var menu = window.FindControl<MenuItem>("AddMessageMenu")!;
                Assert.All(menu.Items.OfType<MenuItem>(), m => Assert.False(m.Command!.CanExecute(null)));
                var first = MessageFileReader.Open(manager, "Synthetic first", Hl7Parser.Parse("MSH|^~\\&|SYN-A\r")); factory.OpenDocument(first.Id);
                var second = MessageFileReader.Open(manager, "Synthetic second", Hl7Parser.Parse("MSH|^~\\&|SYN-B\r")); factory.OpenDocument(second.Id);
                var ribbon = Assert.IsType<UtilityRibbon>(window.FindControl<ContentControl>("RibbonHost")!.Content);
                if (pinned) ribbon.Pin.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                ribbon.SelectTab(0); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var host = pinned ? Assert.IsAssignableFrom<Control>(ribbon.PinnedHost.Content) : ribbon.TemporaryPopup.Child!;
                var add = host.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Add Message");
                var flyout = Assert.IsType<MenuFlyout>(add.Flyout);
                Assert.Equal(new[] { "Before Current", "After Current", "Beginning", "End" }, flyout.Items.OfType<MenuItem>().Select(m => m.Header));
                var item = flyout.Items.OfType<MenuItem>().Last();
                Assert.Same(menu.Items.OfType<MenuItem>().Last().Command, item.Command);
                add.Flyout!.ShowAt(add); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Assert.True(item.Focus());
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); await second.EditRefresh;
                Assert.Equal(2, second.MessageCount); Assert.Equal(1, first.MessageCount); Assert.Equal(2, second.MessageNumber);
                Assert.False(ribbon.TemporaryPopup.IsOpen); Assert.True(second.IsEditMode); Assert.False(manager.Preferences.StartInEditMode);
                var view = window.GetVisualDescendants().OfType<FileDocumentView>().Single(v => ReferenceEquals(v.Document, second));
                Assert.Same(view.ActiveView!.Editor.TextArea, window.FocusManager!.GetFocusedElement());
                factory.SetActiveDockable(factory.GetDockable<IDocument>(first.Id)!);
                menu.Items.OfType<MenuItem>().First().Command!.Execute(null); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); await first.EditRefresh;
                Assert.Equal(2, first.MessageCount); Assert.Equal(1, first.MessageNumber); Assert.Equal(2, second.MessageCount);
                Assert.Same(first, manager.ActiveDocument); Assert.True(first.IsEditMode);
            }
            finally
            {
                var docs = manager?.Documents.ToArray() ?? [];
                foreach (var doc in docs) manager!.Close(doc.Id, discardChanges: true);
                window.Close(); await Task.WhenAll(docs.Select(d => d.Disposal)); Directory.Delete(root, true);
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
