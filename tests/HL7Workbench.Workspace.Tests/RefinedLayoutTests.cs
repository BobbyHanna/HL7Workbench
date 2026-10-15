using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class RefinedLayoutTests
{
    [Fact]
    public void VersionTwoContainsOnlyBoundedPlacementMetadataAndVersionOneStillLoads()
    {
        var layout = new WorkspaceLayout(2, new("root", "workspace", Children: [new("documents", "documents")]), [], ["tool.raw"], null,
            [new("tool.raw", "group.bottom", "Bottom", .3, 2, ["tool.fields"], [new("split.center", true, .5, 0)])]);
        var json = LayoutCodec.Encode(layout); var restored = LayoutCodec.Decode(json);
        Assert.Equal(json, LayoutCodec.Encode(restored)); Assert.Equal("group.bottom", Assert.Single(restored.ToolPlacements!).GroupId);
        Assert.DoesNotContain("Source", json); Assert.DoesNotContain("Snapshot", json); Assert.DoesNotContain("Session", json);
        var old = layout with { Version = 1, ToolPlacements = null }; Assert.Equal(1, LayoutCodec.Decode(LayoutCodec.Encode(old)).Version);
    }
    [Fact]
    public void InvalidRememberedCoordinatesIndicesAndExcessiveMetadataAreRejected()
    {
        var place = new ToolPlacement("tool.raw", "group", "Bottom", .25, 0, [], []);
        var layout = new WorkspaceLayout(2, new("root", Children: [new("documents")]), [], [], null, [place]);
        foreach (var invalid in new[] { place with { TabIndex = -1 }, place with { Proportion = double.NaN }, place with { Containers = [new("x", false, null, -1)] },
            place with { Floating = new(0, 0, double.PositiveInfinity, 100) }, place with { GroupIndex = 300 }, place with { GroupId = "" } })
            Assert.Throws<InvalidDataException>(() => LayoutCodec.Encode(layout with { ToolPlacements = [invalid] }));
        Assert.Throws<InvalidDataException>(() => LayoutCodec.Encode(layout with { ToolPlacements = Enumerable.Range(0, 65).Select(i => place with { Id = $"tool.{i}" }).ToArray() }));
    }
    [Fact]
    public void ReleasableToolCanBeCreatedClosedAndCreatedAgain()
    {
        var registry = new ToolWindowRegistry(); var count = 0;
        registry.Register(new("tool.optional", "Optional", () => { count++; return new Probe(); }, true, true));
        Assert.False(registry.IsCreated("tool.optional")); var first = Assert.IsType<Probe>(registry.GetContent("tool.optional"));
        registry.Release("tool.optional"); Assert.True(first.Disposed); Assert.False(registry.IsCreated("tool.optional"));
        Assert.NotSame(first, registry.GetContent("tool.optional")); Assert.Equal(2, count); registry.Release("tool.optional");
    }
    private sealed class Probe : IDisposable { public bool Disposed { get; private set; } public void Dispose() => Disposed = true; }
}
