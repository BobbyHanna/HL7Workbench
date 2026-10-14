using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Dock.Model.Mvvm.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Docking;

public sealed class WorkspaceDockFactory : Factory, IDisposable
{
    public const string BrowserId = "tool.message-browser";
    public const string DiagnosticsId = "tool.diagnostics";
    private readonly DocumentManager documents;
    private readonly ToolWindowRegistry tools;
    private readonly Dictionary<string, IDockable> items = [];
    public IRootDock Root { get; private set; } = null!;
    private bool disposed;

    public WorkspaceDockFactory(DocumentManager documents, ToolWindowRegistry tools)
    {
        this.documents = documents;
        this.tools = tools;
        HideToolsOnClose = true;
        HostWindowLocator = new() { [nameof(IDockWindow)] = () => new HostWindow() };
        ActiveDockableChanged += ActiveDockableChangedHandler;
        DockableClosed += DockableClosedHandler;
    }

    private void ActiveDockableChangedHandler(object? sender, Dock.Model.Core.Events.ActiveDockableChangedEventArgs e) => TrackDocument(e.Dockable);
    private void DockableClosedHandler(object? sender, Dock.Model.Core.Events.DockableClosedEventArgs e)
    {
        if (e.Dockable?.Context is MessageDocument && e.Dockable.Id is { } id)
        {
            documents.Close(id);
            items.Remove(id);
            DockableLocator?.Remove(id);
        }
    }

    private void TrackDocument(IDockable? item)
    {
        if (item is IDocument && documents.Documents.Any(d => d.Id == item.Id)) documents.Activate(item.Id!);
    }

    private void CreateItems()
    {
        items.Clear();
        foreach (var document in documents.Documents)
            items.Add(document.Id, new Document { Id = document.Id, Title = document.Title, Context = document,
                CanFloat = false, CanClose = true, CanPin = false });
        foreach (var tool in tools.Definitions)
            items.Add(tool.Id, new Tool { Id = tool.Id, Title = tool.Title, Context = tools.GetContent(tool.Id),
                CanDockAsDocument = false, CanFloat = true, CanClose = true, CanPin = false });
        DockableLocator = items.ToDictionary(p => p.Key, p => (Func<IDockable?>)(() => p.Value));
    }

    public override IRootDock CreateLayout()
    {
        CreateItems();
        var documentDock = new DocumentDock { Id = "documents", IsCollapsable = false, CanCreateDocument = false,
            VisibleDockables = CreateList(documents.Documents.Select(d => items[d.Id]).ToArray()),
            ActiveDockable = documents.ActiveDocument is { } active ? items[active.Id] : null };
        var browser = new ToolDock { Id = "browser-dock", Alignment = Alignment.Left, Proportion = .22,
            VisibleDockables = CreateList(items[BrowserId]), ActiveDockable = items[BrowserId] };
        var diagnostics = new ToolDock { Id = "diagnostics-dock", Alignment = Alignment.Bottom, Proportion = .24,
            VisibleDockables = CreateList(items[DiagnosticsId]), ActiveDockable = items[DiagnosticsId] };
        var center = new ProportionalDock { Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(documentDock, new ProportionalDockSplitter(), diagnostics) };
        var split = new ProportionalDock { Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(browser, new ProportionalDockSplitter(), center) };
        Root = new RootDock { Id = "workspace", IsCollapsable = false, VisibleDockables = CreateList<IDockable>(split),
            ActiveDockable = split, DefaultDockable = split, HiddenDockables = CreateList<IDockable>(),
            Windows = CreateList<IDockWindow>() };
        InitLayout(Root);
        return Root;
    }

    public void OpenDocument(string id)
    {
        if (!documents.Documents.Any(d => d.Id == id)) return;
        if (!items.ContainsKey(id))
        {
            // Insert into the live document group so opening a file retains tool geometry
            // and native floating hosts, rather than reconstructing the accepted layout.
            var document = documents.Documents.Single(d => d.Id == id);
            var item = new Document { Id = id, Title = document.Title, Context = document,
                CanFloat = false, CanClose = true, CanPin = false };
            var dock = FindDockable(Root, d => d is IDocumentDock) as IDocumentDock
                ?? throw new InvalidOperationException("Missing document workspace.");
            items.Add(id, item);
            DockableLocator![id] = () => item;
            AddDockable(dock, item);
        }
        SetActiveDockable(items[id]);
        documents.Activate(id);
    }
    public void CloseFloatingWindows()
    {
        foreach (var window in Root.Windows?.ToArray() ?? []) window.Exit();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ActiveDockableChanged -= ActiveDockableChangedHandler;
        DockableClosed -= DockableClosedHandler;
    }
    public void ShowTool(string id)
    {
        var item = items[id];
        RestoreDockable(item);
        // Hidden layouts have no serialized OriginalOwner. Supply a valid tool group.
        if (item.Owner is not IToolDock || item.Owner is IDocumentDock ||
            ((IDock)item.Owner).VisibleDockables?.Contains(item) != true)
        {
            if (item.Owner is IDock owner && owner.VisibleDockables?.Contains(item) == true) RemoveDockable(item, false);
            Root.HiddenDockables?.Remove(item);
            var target = FindDockable(Root, d => d is IToolDock && d is not IDocumentDock) as IToolDock;
            if (target is null)
            {
                target = new ToolDock { Alignment = Alignment.Right, Proportion = .25, VisibleDockables = CreateList<IDockable>() };
                var split = FindDockable(Root, d => d is IProportionalDock) as IProportionalDock;
                if (split is null)
                {
                    split = new ProportionalDock { Orientation = Orientation.Horizontal,
                        VisibleDockables = CreateList((Root.VisibleDockables ?? []).ToArray()) };
                    Root.VisibleDockables = CreateList<IDockable>(split);
                    Root.DefaultDockable = split;
                    Root.ActiveDockable = split;
                    InitDockable(split, Root);
                }
                AddDockable(split, new ProportionalDockSplitter());
                AddDockable(split, target);
            }
            AddDockable(target, item);
        }
        SetActiveDockable(item);
        ActivateWindow(item);
    }

    public WorkspaceLayout Capture()
    {
        LayoutNode Node(IDockable item)
        {
            var kind = item switch { IRootDock => "root", IDocumentDock => "documents", IToolDock => "tools",
                IProportionalDock => "split", IProportionalDockSplitter => "splitter", ITool => "tool", IDocument => "document",
                _ => throw new InvalidDataException("Unsupported dock type.") };
            var proportion = double.IsFinite(item.Proportion) && item.Proportion > 0 ? item.Proportion : (double?)null;
            return new(kind, item is IDocument or ITool ? item.Id : null, proportion,
                (item as IDock)?.ActiveDockable?.Id, (item as IToolDock)?.Alignment.ToString(),
                (item as IDock)?.VisibleDockables?.Select(Node).ToArray(),
                item is IProportionalDock split && split.Orientation == Orientation.Vertical);
        }
        foreach (var window in Root.Windows ?? []) window.Save();
        return new(1, Node(Root), (Root.Windows ?? []).Where(w => w.Layout != null).Select(w =>
            new FloatingLayout(Node(w.Layout!), w.X, w.Y, w.Width, w.Height)).ToArray(),
            (Root.HiddenDockables ?? []).OfType<ITool>().Select(t => t.Id!).ToArray(), documents.ActiveDocument?.Id);
    }

    public IRootDock Restore(WorkspaceLayout layout, IReadOnlyList<ScreenArea> screens)
    {
        // Validate before constructing a live graph. Unknown identities are omitted.
        LayoutCodec.Encode(layout);
        CreateItems();
        var used = new HashSet<string>();
        IDockable? Node(LayoutNode node)
        {
            if (node.Kind is "tool" or "document")
            {
                if (!items.TryGetValue(node.Id!, out var item)) return null;
                if ((node.Kind == "document") != (item.Context is MessageDocument)) throw new InvalidDataException("Identity kind mismatch.");
                used.Add(node.Id!);
                return item;
            }
            IDockable result = node.Kind switch {
                "root" => new RootDock { IsCollapsable = false },
                "documents" => new DocumentDock { IsCollapsable = false, CanCreateDocument = false },
                "tools" => new ToolDock { Alignment = Enum.TryParse<Alignment>(node.Alignment, out var a) ? a : Alignment.Left },
                "split" => new ProportionalDock { Orientation = node.Vertical ? Orientation.Vertical : Orientation.Horizontal },
                "splitter" => new ProportionalDockSplitter(),
                _ => throw new InvalidDataException("Unsupported node.") };
            result.Proportion = node.Proportion ?? double.NaN;
            if (result is IDock dock)
            {
                var children = (node.Children ?? []).Select(Node).OfType<IDockable>().ToArray();
                if (node.Kind == "documents" && children.Any(c => c.Context is not MessageDocument) ||
                    node.Kind == "tools" && children.Any(c => c is not ITool)) throw new InvalidDataException("Invalid tab group.");
                dock.VisibleDockables = CreateList(children);
                dock.ActiveDockable = children.FirstOrDefault(c => c.Id == node.ActiveId) ?? children.FirstOrDefault();
                if (dock is IRootDock root) root.DefaultDockable = dock.ActiveDockable;
            }
            return result;
        }
        Root = Node(layout.Root) as IRootDock ?? throw new InvalidDataException("Missing root.");
        Root.Id = "workspace";
        Root.HiddenDockables = CreateList<IDockable>();
        Root.Windows = CreateList<IDockWindow>();
        foreach (var saved in layout.Windows)
        {
            var floatingRoot = Node(saved.Root) as IRootDock ?? throw new InvalidDataException("Missing floating root.");
            var position = WindowRecovery.Recover(saved.X, saved.Y, screens);
            Root.Windows.Add(new DockWindow { Layout = floatingRoot, X = position.X, Y = position.Y,
                Width = saved.Width, Height = saved.Height, Title = "HL7 Workbench" });
        }
        foreach (var id in layout.HiddenTools)
            if (items.TryGetValue(id, out var tool) && tool is ITool && used.Add(id)) Root.HiddenDockables.Add(tool);
        // Documents are session-owned; layout files cannot discard current documents.
        var documentDock = FindDockable(Root, d => d is IDocumentDock) as IDocumentDock;
        if (documentDock is null) throw new InvalidDataException("Missing document workspace.");
        foreach (var document in documents.Documents)
            if (used.Add(document.Id)) documentDock.VisibleDockables!.Add(items[document.Id]);
        foreach (var tool in tools.Definitions)
            if (used.Add(tool.Id)) Root.HiddenDockables.Add(items[tool.Id]);
        InitLayout(Root);
        if (layout.ActiveDocumentId is { } active && documents.Documents.Any(d => d.Id == active)) OpenDocument(active);
        return Root;
    }
}
