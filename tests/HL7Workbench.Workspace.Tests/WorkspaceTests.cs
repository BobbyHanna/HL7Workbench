using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class WorkspaceTests
{
    [Fact]
    public void ClosingActiveDocumentSelectsRemainingDocumentThenClearsContext()
    {
        var manager = new DocumentManager();
        manager.Open(new("one", "Synthetic one", Hl7Parser.Parse("PID|1")));
        manager.Open(new("two", "Synthetic two", Hl7Parser.Parse("PID|2")));
        manager.Close("two");
        Assert.Equal("one", manager.ActiveDocument?.Id);
        manager.Close("one");
        Assert.Null(manager.ActiveDocument);
    }
    [Fact]
    public void DuplicateDocumentIdentityIsRejectedWithoutChangingSelection()
    {
        var manager = new DocumentManager();
        var document = new MessageDocument("one", "Synthetic", Hl7Parser.Parse("PID|1"));
        manager.Open(document);
        Assert.Throws<ArgumentException>(() => manager.Open(document));
        Assert.Single(manager.Documents);
        Assert.Same(document, manager.ActiveDocument);
    }

    [Fact]
    public void RepeatedActivationIsIdempotentAndClosingInactiveDocumentRetainsContext()
    {
        var manager = new DocumentManager();
        manager.Open(new("one", "Synthetic one", Hl7Parser.Parse("PID|1")));
        manager.Open(new("two", "Synthetic two", Hl7Parser.Parse("PID|2")));
        var changes = 0;
        manager.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(manager.ActiveDocument)) changes++; };

        for (var i = 0; i < 50; i++) manager.Activate("two");
        manager.Close("one");

        Assert.Equal(0, changes);
        Assert.Equal("two", manager.ActiveDocument?.Id);
        Assert.Single(manager.Documents);
    }
    [Fact]
    public void ToolsAreCreatedLazilyAndRetainTheirContent()
    {
        var count = 0;
        var registry = new ToolWindowRegistry();
        registry.Register(new("tool.synthetic", "Synthetic", () => { count++; return new object(); }));
        Assert.Equal(0, count);
        Assert.Same(registry.GetContent("tool.synthetic"), registry.GetContent("tool.synthetic"));
        Assert.Equal(1, count);
    }
    [Fact]
    public void LayoutRoundTripContainsMetadataOnly()
    {
        var layout = new WorkspaceLayout(1, new("root", Children: [new("documents", Children: [new("document", "synthetic.one")])]),
            [new(new("root", Children: [new("tools", Children: [new("tool", "tool.diagnostics")])]), -1920, 40, 600, 400)], [], "synthetic.one");
        var json = LayoutCodec.Encode(layout);
        Assert.Equal(json, LayoutCodec.Encode(LayoutCodec.Decode(json)));
        Assert.DoesNotContain("MSH", json);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Version\":99}")]
    [InlineData("{broken")]
    public void InvalidLayoutIsRejected(string json) => Assert.ThrowsAny<Exception>(() => LayoutCodec.Decode(json));
    [Fact]
    public void OversizedAndRepeatedIdentityLayoutsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => LayoutCodec.Decode(new string(' ', LayoutCodec.MaxBytes + 1)));
        var layout = new WorkspaceLayout(1, new("root", Children: [new("document", "same"), new("document", "same")]), [], [], null);
        Assert.Throws<InvalidDataException>(() => LayoutCodec.Encode(layout));
    }
    [Theory]
    [InlineData(-1800, 40, -1800, 40)]
    [InlineData(10000, 10000, 40, 40)]
    [InlineData(1910, 1070, 40, 40)]
    public void RecoveryPreservesReachableNegativeMonitorCoordinatesAndRecoversMissingMonitors(double x, double y, double expectedX, double expectedY)
    {
        var result = WindowRecovery.Recover(x, y, [new(0, 0, 1920, 1080), new(-1920, 0, 1920, 1080)]);
        Assert.Equal((expectedX, expectedY), result);
    }
    [Fact]
    public void WorkspaceHasNoDockingOrUiAssemblyDependencies()
    {
        Assert.DoesNotContain(typeof(DocumentManager).Assembly.GetReferencedAssemblies(),
            a => a.Name!.StartsWith("Dock.", StringComparison.Ordinal) || a.Name.StartsWith("Avalonia", StringComparison.Ordinal));
    }
}
