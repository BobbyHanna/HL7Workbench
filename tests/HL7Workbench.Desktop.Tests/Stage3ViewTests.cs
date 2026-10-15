using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Tests;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class Stage3ViewTests
{
    [Fact]
    public async Task FileNavigationUpdatesSameTabDiagnosticsAndRejectsInvalidNumbers()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rPID|ONE\rMSH|^^^^|SYN\rPID|TWO"));
            var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
            using var diagnostics = new DiagnosticsViewModel(manager);
            var updates = 0; diagnostics.PropertyChanged += (_, _) => updates++;
            var host = Assert.IsType<FileDocumentView>(MessageViewer.Create(document));
            var window = new Window { Width = 1400, Height = 1000, Content = host }; window.Show();
            try
            {
                var original = host.ActiveView!;
                original.Navigator.SelectedItem = document.Inspection.Segments[1]; document.Inspection.InspectField(1);
                host.MessageNumberInput.Text = "invalid"; await host.JumpAsync(); Assert.Equal(1, document.MessageNumber);
                host.MessageNumberInput.Text = "3"; await host.JumpAsync(); Assert.Equal(1, document.MessageNumber);
                host.MessageNumberInput.Text = "2"; await host.JumpAsync();
                Assert.Same(document, manager.ActiveDocument); Assert.Single(manager.Documents);
                Assert.NotSame(original, host.ActiveView);
                Assert.Null(document.Inspection.SelectedSegment); Assert.Null(document.Inspection.SelectedNode);
                Assert.Equal(0, host.ActiveView!.Editor.SelectionLength);
                Assert.Equal(0, host.ActiveView.Editor.HorizontalOffset); Assert.Equal(0, host.ActiveView.Editor.VerticalOffset);
                Assert.Contains(diagnostics.Diagnostics, d => d.Code == "InvalidDelimiters"); Assert.True(updates > 0);
                Assert.All(host.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text is not null && t.Text.Contains(" / ")),
                    t => Assert.DoesNotContain("MSH", t.Text!));
                await host.NavigateAsync(1); Assert.DoesNotContain(diagnostics.Diagnostics, d => d.Code == "InvalidDelimiters");
            }
            finally { window.Close(); manager.Close(document.Id); await document.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Theory]
    [InlineData("PID|SYN", 1)] [InlineData("arbitrary content", 0)]
    public async Task SingleAndZeroMessageToolbarsRemainVisibleWithDisabledDirections(string text, int expected)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
            var host = new FileDocumentView(document); var window = new Window { Width = 1300, Height = 1000, Content = host }; window.Show();
            try
            {
                window.UpdateLayout();
                var directions = host.GetVisualDescendants().OfType<Button>().Where(b => b.Content is "|<" or "<" or ">" or ">|").ToArray();
                Assert.Equal(4, directions.Length); Assert.All(directions, b => { Assert.True(b.IsVisible); Assert.False(b.IsEnabled); });
                Assert.Equal(expected.ToString(), host.MessageNumberInput.Text);
            }
            finally { window.Close(); manager.Close(document.Id); await document.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task LargePresentationPreservesPathsSelectionAndFollowingStructure()
    {
        var path = await LargeSyntheticFile.CreateAsync(256 * 1024, TestContext.Current.CancellationToken);
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); await using var input = File.OpenRead(path);
            var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
            Assert.NotNull(document.Source);
            var host = new FileDocumentView(document); var window = new Window { Width = 1600, Height = 1000, Content = host }; window.Show();
            try
            {
                var view = host.ActiveView!; await view.PresentationView!.Ready;
                Assert.True(view.Editor.Document.TextLength <= 16386);
                var state = document.Inspection;
                state.SelectSegment(state.Segments.Single(s => s.Identifier == "OBX"));
                var data = state.InspectField(5)!.Children[0].Children[4]; state.SelectNode(data);
                Assert.Same(data, state.SelectedNode);
                Assert.Contains("omitted", view.Editor.Text);
                Assert.True(view.Editor.SelectionLength <= 16386);
                var offset = data.Range64!.Value.Offset + 8;
                Assert.Equal("OBX[1]-5[1].5", state.ResolveOffset(offset)!.Path.ToString());
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var textView = view.Editor.TextArea.TextView;
                var displayedOffset = (int)view.PresentationView.Presentation.DisplayOffset(offset);
                var point = textView.GetVisualPosition(new AvaloniaEdit.TextViewPosition(view.Editor.Document.GetLocation(displayedOffset)), AvaloniaEdit.Rendering.VisualYPosition.LineMiddle) - textView.ScrollOffset;
                var hit = textView.TranslatePoint(point + new Vector(2, 0), window)!.Value;
                window.MouseMove(new Point(1, 1)); window.MouseMove(hit);
                await Task.Delay(650, TestContext.Current.CancellationToken);
                Assert.True(ToolTip.GetIsOpen(textView));
                Assert.Contains("OBX[1]-5[1].5", Assert.IsType<string>(ToolTip.GetTip(textView)));
                window.MouseDown(hit, MouseButton.Left); window.MouseUp(hit, MouseButton.Left);
                window.MouseDown(hit, MouseButton.Left); window.MouseUp(hit, MouseButton.Left);
                Assert.Equal(data.Path, state.SelectedNode!.Path);
                Assert.False(ToolTip.GetIsOpen(textView));
                await view.LoadPreviewAsync(data.Range64.Value.Length - 8);
                Assert.Contains("262144", view.ValueStatus.Text!);
                Assert.Equal(data.Range64.Value.Length - 8, state.PreviewOffset);
                state.SelectSegment(state.Segments.Single(s => s.Identifier == "NTE")); state.InspectField(3);
                Assert.Contains("AFTER-SEGMENT", view.Editor.Text);
                Assert.Contains("AFTER-SEGMENT", view.Editor.SelectedText);
                await host.NavigateAsync(2); Assert.Null(document.Inspection.SelectedSegment);
            }
            finally { window.Close(); manager.Close(document.Id); await document.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task ClipboardGuardBlocksHugeMaterialization()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(() =>
        {
            var editor = new AvaloniaEdit.TextEditor { IsReadOnly = true, Text = new string('X', BoundedCopy.MaximumCharacters + 1) };
            BoundedCopy.Attach(editor); editor.SelectAll();
            var e = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.C, KeyModifiers = KeyModifiers.Control };
            editor.TextArea.RaiseEvent(e); Assert.True(e.Handled);
            Assert.Contains("limited", Assert.IsType<string>(ToolTip.GetTip(editor)));
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task MeasureHundredMiBAbbreviatedPresentationAndStructuralNavigationRendering()
    {
        // A separate filename avoids racing the Workspace generator when projects run together.
        var path = await LargeSyntheticFile.CreateAsync(100L * 1024 * 1024 + 4, TestContext.Current.CancellationToken);
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); await using var input = File.OpenRead(path);
            var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic render", input, TestContext.Current.CancellationToken);
            var timer = Stopwatch.StartNew();
            var host = new FileDocumentView(document); var window = new Window { Width = 1400, Height = 1000, Content = host }; window.Show();
            try
            {
                await host.ActiveView!.PresentationView!.Ready; window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var firstRenderMs = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); document.Inspection.SelectSegment(document.Inspection.Segments.Single(s => s.Identifier == "NTE")); document.Inspection.InspectField(3); window.UpdateLayout();
                var tailRenderMs = timer.Elapsed.TotalMilliseconds;
                Assert.Contains("AFTER-SEGMENT", host.ActiveView.Editor.Text);
                var report = new { encodedCharacters = 100L * 1024 * 1024 + 4, firstRenderMs, tailRenderMs, backend = "Avalonia.Headless", displayCharacters = host.ActiveView.Editor.Document.TextLength };
                await File.WriteAllTextAsync(Path.Combine(LargeSyntheticFile.ArtifactDirectory, "render-100MiB.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
            }
            finally { window.Close(); manager.Close(document.Id); await document.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
