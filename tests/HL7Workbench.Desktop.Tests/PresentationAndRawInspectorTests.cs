using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class PresentationAndRawInspectorTests
{
    [Fact]
    public async Task ManySmallValuesScrollThroughVirtualizedBoundedEditorsAndNavigateToTail()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager();
            var text = "MSH|^~\\&|SYN\rZXY|" + string.Join('|', Enumerable.Repeat(new string('V', 1000), 2000)) + "\rNTE|1||TAIL";
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
            var view = Assert.IsType<MessageInspectionView>(MessageViewer.Create(doc, true) is FileDocumentView file ? file.ActiveView : null);
            var window = new Window { Width = 1200, Height = 800, Content = view }; window.Show();
            try
            {
                Assert.True(view.PresentationView!.IsVirtualized); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                await view.PresentationView.Ready; window.UpdateLayout();
                var editors = view.GetVisualDescendants().OfType<AvaloniaEdit.TextEditor>().ToArray();
                Assert.True(editors.Length < 60); Assert.All(editors, editor => Assert.True(editor.Document.TextLength <= 16384));
                doc.Inspection.SelectSegment(doc.Inspection.Segments[^1]); doc.Inspection.InspectField(3);
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); await view.PresentationView.Ready; window.UpdateLayout();
                Assert.Contains(view.GetVisualDescendants().OfType<AvaloniaEdit.TextEditor>(), editor => editor.Text.Contains("TAIL", StringComparison.Ordinal));
                Assert.Equal("TAIL", doc.Inspection.SelectedNode!.Preview);
            }
            finally { window.Close(); manager.Close(doc.Id); await doc.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task MarkersHaveDistinctHitsAndCopyAndFollowingFieldsRemainMapped()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rOBX|1|TX|||" + new string('A', 20000) + "|AFTER\rNTE|1||NOTE"));
            var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
            var host = new FileDocumentView(doc); var window = new Window { Width = 1300, Height = 900, Content = host }; window.Show();
            try
            {
                var view = host.ActiveView!; await view.PresentationView!.Ready;
                Assert.DoesNotContain(view.RawPane.GetVisualDescendants().OfType<Button>(), b => b.Content is "First text" or "Next text" or "Go to text offset");
                Assert.Contains("|AFTER\rNTE", view.Editor.Text);
                var p = doc.Presentation!; var marker = Assert.Single(p.Fragments, f => f.Marker is not null);
                view.Editor.Select((int)marker.Display.Offset, (int)marker.Display.Length);
                var copy = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.C, KeyModifiers = KeyModifiers.Control };
                view.Editor.TextArea.RaiseEvent(copy); Assert.True(copy.Handled);
                Assert.Contains("Copy Visible Preview", Assert.IsType<string>(ToolTip.GetTip(view.Editor)));
                ToolTip.SetIsOpen(view.Editor, false);
                var after = view.Editor.Text.IndexOf("AFTER", StringComparison.Ordinal);
                Assert.Equal("OBX[1]-6", doc.Inspection.ResolveOffset(p.SourceOffset(after)!.Value)!.Path.ToString());
                var textView = view.Editor.TextArea.TextView; var markerLocation = view.Editor.Document.GetLocation((int)marker.Display.Offset);
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                view.Editor.ScrollTo(markerLocation.Line, markerLocation.Column); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var scroller = view.Editor.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "PART_ScrollViewer");
                scroller.Offset = new Vector(3000, scroller.Offset.Y); window.UpdateLayout();
                var point = textView.GetVisualPosition(new AvaloniaEdit.TextViewPosition(view.Editor.Document.GetLocation((int)marker.Display.Offset)), AvaloniaEdit.Rendering.VisualYPosition.LineMiddle) - textView.ScrollOffset;
                Assert.True(textView.Bounds.WithX(0).WithY(0).Contains(point + new Vector(2, 0)), $"Marker point {point}, bounds {textView.Bounds}, scroll {textView.ScrollOffset}");
                var hit = textView.TranslatePoint(point + new Vector(2, 0), window)!.Value;
                window.MouseMove(new Point(1, 1)); window.MouseMove(hit); await Task.Delay(650, TestContext.Current.CancellationToken);
                Assert.Contains("Display-only omission", Assert.IsType<string>(ToolTip.GetTip(textView)));
                window.MouseDown(hit, MouseButton.Left); window.MouseUp(hit, MouseButton.Left); window.MouseDown(hit, MouseButton.Left); window.MouseUp(hit, MouseButton.Left);
                Assert.Equal(5, doc.Inspection.SelectedNode!.Path.Field);
                var menus = ((IEnumerable<MenuItem>)view.Editor.ContextMenu!.ItemsSource!).ToArray();
                Assert.Contains(menus, m => Equals(m.Header, "View Full Value")); Assert.Contains(menus, m => Equals(m.Header, "View Full Message Source"));
            }
            finally { window.Close(); manager.Close(doc.Id); await doc.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task RawInspectorChunksAreContiguousAndReleaseOnClosureAndMessageChange()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); var value = new string('B', 70000) + "TAIL";
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rNTE|1||" + value + "|NEXT\rMSH|^~\\&|OTHER\rPID|2"));
            var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
            var marker = Assert.Single(doc.Presentation!.Fragments, f => f.Marker is not null);
            using var raw = new RawInspectorSession(doc, marker.Omitted, "NTE-3");
            using var view = new FullRawInspectorView(raw); var window = new Window { Width = 1100, Height = 800, Content = view }; window.Show();
            try
            {
                await view.Ready; Assert.Equal(16384, view.Editor.Document.TextLength); Assert.DoesNotContain("omitted", view.Editor.Text);
                await view.LoadAsync(16384); Assert.Equal(16384, view.Position);
                await view.LoadAsync(0); Assert.Equal(0, view.Position);
                await view.LoadAsync(value.Length - 4); Assert.Equal("TAIL", view.Editor.Text);
                Assert.Contains("message UTF-16", view.Status.Text!); Assert.Contains("Scope UTF-16", view.Status.Text!);
                await view.LoadAsync(-1); Assert.Contains("whole UTF-16", view.Status.Text!);
                ToolTip.SetTip(view.Editor.TextArea.TextView, "Synthetic path"); ToolTip.SetIsOpen(view.Editor.TextArea.TextView, true);
                await doc.NavigateAsync(2, TestContext.Current.CancellationToken); Assert.True(raw.IsClosed); Assert.Equal(0, view.Editor.Document.TextLength);
                Assert.False(ToolTip.GetIsOpen(view.Editor.TextArea.TextView)); Assert.Null(ToolTip.GetTip(view.Editor.TextArea.TextView));
                Assert.Same(doc, manager.ActiveDocument);
            }
            finally { window.Close(); manager.Close(doc.Id); await doc.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
