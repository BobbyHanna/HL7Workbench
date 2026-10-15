using System.Text.Json;

namespace HL7Workbench.Workspace;

// Application-owned schema: never serialize parser objects, feature content, or filenames.
public sealed record LayoutNode(string Kind, string? Id = null, double? Proportion = null,
    string? ActiveId = null, string? Alignment = null, LayoutNode[]? Children = null, bool Vertical = false);
public sealed record FloatingLayout(LayoutNode Root, double X, double Y, double Width, double Height);
public sealed record FloatingBounds(double X, double Y, double Width, double Height);
public sealed record ContainerSlot(string Id, bool Vertical, double? Proportion, int Index);
/// <summary>Placement only: no source identity or feature state. Containers permit reconstruction of collapsed groups.</summary>
public sealed record ToolPlacement(string Id, string GroupId, string Alignment, double? Proportion, int TabIndex,
    string[] Peers, ContainerSlot[] Containers, FloatingBounds? Floating = null, int GroupIndex = 0);
public sealed record WorkspaceLayout(int Version, LayoutNode Root, FloatingLayout[] Windows,
    string[] HiddenTools, string? ActiveDocumentId, ToolPlacement[]? ToolPlacements = null);

public static class LayoutCodec
{
    public const int MaxBytes = 256 * 1024;
    private static readonly JsonSerializerOptions Options = new() { MaxDepth = 24, WriteIndented = true };
    public static string Encode(WorkspaceLayout layout)
    {
        Validate(layout);
        var json = JsonSerializer.Serialize(layout, Options);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException("Layout is too large.");
        return json;
    }
    public static WorkspaceLayout Decode(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException("Layout is too large.");
        var layout = JsonSerializer.Deserialize<WorkspaceLayout>(json, Options) ?? throw new InvalidDataException("Empty layout.");
        Validate(layout);
        return layout;
    }
    private static void Validate(WorkspaceLayout layout)
    {
        if (layout.Version is not (1 or 2) || layout.Root is null || layout.Windows is null || layout.HiddenTools is null ||
            layout.Windows.Length > 16 || layout.HiddenTools.Length > 64) throw new InvalidDataException("Unsupported layout.");
        var count = 0;
        var ids = new HashSet<string>();
        bool InvalidId(string? id) => string.IsNullOrWhiteSpace(id) || id.Length > 128;
        bool InvalidProportion(double? p) => p is { } number && (!double.IsFinite(number) || number <= 0 || number > 1);
        void Visit(LayoutNode node, int depth)
        {
            if (node is null || ++count > 256 || depth > 16 ||
                node.Kind is not ("root" or "split" or "splitter" or "documents" or "tools" or "document" or "tool") ||
                InvalidProportion(node.Proportion) || node.Id is { Length: > 128 } ||
                node.Alignment is not (null or "Left" or "Right" or "Top" or "Bottom"))
                throw new InvalidDataException("Invalid layout node.");
            if (node.Kind is "tool" or "document" && (string.IsNullOrWhiteSpace(node.Id) || node.Id.Length > 128 || !ids.Add(node.Id)))
                throw new InvalidDataException("Invalid or repeated identity.");
            if (node.Kind is not ("tool" or "document") && node.Id is { } groupId && (InvalidId(groupId) || !ids.Add(groupId)))
                throw new InvalidDataException("Invalid or repeated group identity.");
            foreach (var child in node.Children ?? []) Visit(child, depth + 1);
        }
        Visit(layout.Root, 0);
        foreach (var window in layout.Windows)
        {
            if (window is null || !double.IsFinite(window.X) || !double.IsFinite(window.Y) ||
                !double.IsFinite(window.Width) || !double.IsFinite(window.Height) || window.Width < 160 || window.Width > 20000 ||
                window.Height < 100 || window.Height > 20000) throw new InvalidDataException("Invalid window geometry.");
            Visit(window.Root, 0);
        }
        if (layout.HiddenTools.Any(InvalidId) || layout.ToolPlacements?.Length > 64) throw new InvalidDataException("Invalid tool metadata.");
        var placementIds = new HashSet<string>();
        foreach (var place in layout.ToolPlacements ?? [])
        {
            if (place is null || InvalidId(place.Id) || !placementIds.Add(place.Id) || InvalidId(place.GroupId) ||
                place.Alignment is not ("Left" or "Right" or "Top" or "Bottom") || InvalidProportion(place.Proportion) ||
                place.TabIndex is < 0 or > 64 || place.GroupIndex is < 0 or > 256 || place.Peers is null || place.Peers.Length > 64 || place.Peers.Any(InvalidId) ||
                place.Containers is null || place.Containers.Length > 16 || place.Containers.Any(c => c is null || InvalidId(c.Id) || c.Index is < 0 or > 256 || InvalidProportion(c.Proportion)))
                throw new InvalidDataException("Invalid remembered placement.");
            if (place.Floating is { } f && (!double.IsFinite(f.X) || !double.IsFinite(f.Y) || !double.IsFinite(f.Width) || !double.IsFinite(f.Height) ||
                f.Width is < 160 or > 20000 || f.Height is < 100 or > 20000)) throw new InvalidDataException("Invalid remembered floating geometry.");
        }
    }
}

public readonly record struct ScreenArea(double X, double Y, double Width, double Height);
public static class WindowRecovery
{
    // Coordinates are screen pixels; window dimensions are device-independent units.
    public static (double X, double Y) Recover(double x, double y, IReadOnlyList<ScreenArea> screens)
    {
        if (screens.Count == 0) return (40, 40);
        if (screens.Any(s => x >= s.X && x + 120 <= s.X + s.Width && y >= s.Y && y + 40 <= s.Y + s.Height)) return (x, y);
        return (screens[0].X + 40, screens[0].Y + 40);
    }
}
