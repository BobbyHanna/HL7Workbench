using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Dock.Model.Controls;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;
using IDocument = Dock.Model.Controls.IDocument;
using Avalonia;
using Avalonia.Headless;

namespace HL7Workbench.Desktop.Tests;

public sealed class ViewerTests
{
    [Theory]
    [InlineData("MSH|^~\\&|SYN\rPID|1||Synthetic^Value~Other&Sub|\\Zhas^delimiter\\\r\n\n")]
    [InlineData("MSH*$%!@*SYN\rPID*1**Synthetic$Value%Other@Sub*!Zhas$delimiter!\n")]
    [InlineData("")]
    [InlineData("bad line\rPID|1")]
    public async Task ActualDockContentIsReadOnlyAndRetainsEveryCharacter(string text)
    {
        // Dispatch completion can resume on the UI worker; asynchronous disposal avoids
        // waiting synchronously for that same worker to exit.
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var message = Hl7Parser.Parse(text);
            var editor = Assert.IsType<FileDocumentView>(new DockContentTemplate().Build(
                new Dock.Model.Mvvm.Controls.Document { Context = new MessageDocument("synthetic", "Synthetic", message) })).ActiveView!.Editor;
            Assert.True(editor.IsReadOnly);
            Assert.Equal(text, editor.Document.Text);
            editor.Select(0, text.Length);
            Assert.Equal(text, editor.SelectedText);
            Assert.False(editor.TextArea.ReadOnlySectionProvider.CanInsert(0));
            Assert.Empty(editor.TextArea.ReadOnlySectionProvider.GetDeletableSegments(new SimpleSegment(0, text.Length)));
            editor.TextArea.PerformTextInput("replacement");
            Assert.Equal(text, editor.Document.Text);
            Assert.Equal(text, message.RawText);
            Assert.False(message.IsModified);
            Assert.Equal(2, editor.TextArea.TextView.LineTransformers.OfType<AvaloniaEdit.Rendering.DocumentColorizingTransformer>().Count());
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InvalidBytesShowUsefulNotice()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var message = Hl7Parser.Parse(new byte[] { 0xFF });
            var notice = Assert.IsType<TextBlock>(MessageViewer.Create(new("id", "Synthetic", message)));
            Assert.Contains("Text unavailable", notice.Text!);
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("MSH|^~\\&|SYN\rPID|A^B~C&D|\\Zhas^delimiter\\", '|', '^', '~', '&', '\\')]
    [InlineData("MSH*$%!@*SYN\rPID*A$B%C@D*!Zhas$delimiter!", '*', '$', '%', '@', '!')]
    public void HighlightingUsesCoreDelimitersAndNeverSplitsEscapedValues(string text, char field, char component, char repetition, char subcomponent, char escape)
    {
        var map = new Hl7HighlightMap(Hl7Parser.Parse(text));
        foreach (var (kind, character) in new[] { (Hl7HighlightKind.Field, field), (Hl7HighlightKind.Component, component),
            (Hl7HighlightKind.Repetition, repetition), (Hl7HighlightKind.Subcomponent, subcomponent), (Hl7HighlightKind.Escape, escape) })
        {
            var spans = map.Spans.Where(s => s.Kind == kind).ToArray();
            Assert.NotEmpty(spans);
            Assert.All(spans, s => Assert.Equal(character, text[s.Offset]));
        }
        Assert.Equal(new[] { "MSH", "PID" }, map.Spans.Where(s => s.Kind == Hl7HighlightKind.Segment).Select(s => text.Substring(s.Offset, s.Length)));
        var escapedDelimiter = text.LastIndexOf(component);
        Assert.DoesNotContain(map.Spans, s => s.Offset == escapedDelimiter);
        Assert.Equal(map.Spans.Where(s => s.Offset >= text.IndexOf("PID", StringComparison.Ordinal)).ToArray(),
            map.Intersecting(text.IndexOf("PID", StringComparison.Ordinal), text.Length).ToArray());
    }

    [Theory]
    [InlineData("MSH|^~|SYN\rPID|A\\B&C^D~E")]
    [InlineData("bad line\rPID|A^B\r")]
    [InlineData("MSH|^^^^|SYN\rPID|A^B")]
    [InlineData("MSH|^~\\&|SYN\rPID|A\\broken^B~C")]
    public void ProvisionalOrOlderDelimitersAndMalformedContentStaySafe(string text)
    {
        var message = Hl7Parser.Parse(text);
        var map = new Hl7HighlightMap(message);
        Assert.All(map.Spans, s => Assert.InRange(s.Offset + s.Length, 1, text.Length));
        if (message.Delimiters.Escape is null) Assert.DoesNotContain(map.Spans, s => s.Kind == Hl7HighlightKind.Escape);
        Assert.Equal(text, message.RawText);
    }

    [Fact]
    public void NewFilesJoinExistingDockWithoutRebuildingToolsIncludingAfterFinalCloseAndRestore()
    {
        var documents = new DocumentManager();
        var tools = new ToolWindowRegistry();
        tools.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
        tools.Register(new(WorkspaceDockFactory.DiagnosticsId, "Diagnostics", () => new object()));
        using var factory = new WorkspaceDockFactory(documents, tools);
        factory.CreateLayout();
        var root = factory.Root;
        var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId);
        var first = MessageFileReader.Open(documents, "synthetic.hl7", Hl7Parser.Parse("PID|1"));
        factory.OpenDocument(first.Id);
        var second = MessageFileReader.Open(documents, "synthetic.hl7", Hl7Parser.Parse("PID|2"));
        factory.OpenDocument(second.Id);
        Assert.Same(root, factory.Root);
        Assert.Same(tool, factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId));
        Assert.Same(second, documents.ActiveDocument);
        factory.CloseDockable(factory.GetDockable<IDocument>(first.Id)!);
        factory.CloseDockable(factory.GetDockable<IDocument>(second.Id)!);
        Assert.Empty(documents.Documents);
        factory.Restore(factory.Capture(), [new(0, 0, 1920, 1080)]);
        var third = MessageFileReader.Open(documents, "synthetic.dat", Hl7Parser.Parse("PID|3"));
        factory.OpenDocument(third.Id);
        Assert.NotNull(factory.GetDockable<IDocument>(third.Id)!.Owner);
        Assert.Same(third, documents.ActiveDocument);
        Assert.DoesNotContain("synthetic.dat", LayoutCodec.Encode(factory.Capture()));
    }
}

public sealed class ViewerTestApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://HL7Workbench.Desktop/"))
        { Source = new Uri("avares://HL7Workbench.Desktop/Branding.axaml") });
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://HL7Workbench.Desktop.Tests/"))
        { Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml") });
        ApplicationTheme.InitializeResources(this);
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ViewerTestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
