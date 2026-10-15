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
    // Preserve the shipped identity so old Diagnostics placements become Activity Log.
    public const string ActivityLogId = DiagnosticsId;
    public const string FullRawId = "tool.full-raw-inspector";
    public const string NavigatorId = "tool.segment-navigator";
    public const string FieldInspectorId = "tool.field-inspector";
    public const string ScriptEditorId = "tool.script-editor";
    private readonly DocumentManager documents;
    private readonly ToolWindowRegistry tools;
    private readonly Dictionary<string, IDockable> items = [];
    private readonly Dictionary<string, ToolPlacement> placements = [];
    private IReadOnlyList<ScreenArea> screens = [];
    public IRootDock Root { get; private set; } = null!;
    public bool HasInspectionTools => items.ContainsKey(NavigatorId) && items.ContainsKey(FieldInspectorId);
    internal bool HasReplaceableContent(string id) => tools.Definitions.Any(t => t.Id == id && (t.Optional || t.ReleaseOnClose));
    private bool disposed;
    private readonly HashSet<MessageDocument> observedDocuments = [];
    private readonly HashSet<string> closingDocuments = [];
    private readonly HashSet<string> approvedCloses = [];
    public Func<MessageDocument, Task<bool>>? ConfirmDocumentCloseAsync { get; set; }
    public Func<Task<bool>>? ConfirmScriptCloseAsync { get; set; }

    public WorkspaceDockFactory(DocumentManager documents, ToolWindowRegistry tools)
    {
        this.documents = documents;
        this.tools = tools;
        HideToolsOnClose = true;
        HostWindowLocator = new() { [nameof(IDockWindow)] = () => new HostWindow() };
        ActiveDockableChanged += ActiveDockableChangedHandler;
        DockableClosed += DockableClosedHandler;
        DockableClosing += DockableClosingHandler;
    }

    private void ActiveDockableChangedHandler(object? sender, Dock.Model.Core.Events.ActiveDockableChangedEventArgs e) => TrackDocument(e.Dockable);
    private void DockableClosingHandler(object? sender, Dock.Model.Core.Events.DockableClosingEventArgs e)
    {
        if (e.Dockable is ITool tool) Remember(tool);
        if (e.Dockable is ITool { Id: ScriptEditorId } scriptTool && !approvedCloses.Contains(ScriptEditorId) && ConfirmScriptCloseAsync is not null)
        {
            e.Cancel = true;
            if (closingDocuments.Add(ScriptEditorId)) _ = ConfirmScriptCloseAsyncCore(scriptTool);
            return;
        }
        if (e.Dockable?.Context is not MessageDocument document || approvedCloses.Contains(document.Id)) return;
        if (document.IsSaving) { e.Cancel = true; return; }
        if (!document.IsDirty) return;
        e.Cancel = true;
        if (ConfirmDocumentCloseAsync is not null && closingDocuments.Add(document.Id)) _ = ConfirmCloseAsync(document, e.Dockable);
    }
    private async Task ConfirmScriptCloseAsyncCore(ITool tool)
    {
        try
        {
            if (ConfirmScriptCloseAsync is not null && await ConfirmScriptCloseAsync() && !disposed &&
                items.TryGetValue(ScriptEditorId, out var current) && ReferenceEquals(tool, current))
            {
                approvedCloses.Add(ScriptEditorId);
                try { CloseDockable(tool); }
                finally { approvedCloses.Remove(ScriptEditorId); }
            }
        }
        finally { closingDocuments.Remove(ScriptEditorId); }
    }
    private async Task ConfirmCloseAsync(MessageDocument document, IDockable item)
    {
        try
        {
            if (ConfirmDocumentCloseAsync is not null && await ConfirmDocumentCloseAsync(document) && !disposed && documents.Documents.Contains(document))
            {
                approvedCloses.Add(document.Id);
                try { CloseDockable(item); }
                finally { approvedCloses.Remove(document.Id); }
            }
        }
        finally { closingDocuments.Remove(document.Id); }
    }
    private void Observe(MessageDocument document)
    { if (observedDocuments.Add(document)) document.PropertyChanged += DocumentTitleChanged; }
    private void DocumentTitleChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { if (sender is MessageDocument document && items.TryGetValue(document.Id, out var item)) item.Title = document.DisplayTitle; }
    private void DockableClosedHandler(object? sender, Dock.Model.Core.Events.DockableClosedEventArgs e)
    {
        if (e.Dockable?.Context is MessageDocument document && e.Dockable.Id is { } id)
        {
            document.PropertyChanged -= DocumentTitleChanged; observedDocuments.Remove(document);
            documents.Close(id, discardChanges: true);
            items.Remove(id);
            DockableLocator?.Remove(id);
        }
        else if (e.Dockable is ITool && e.Dockable.Id is { } toolId && tools.Definition(toolId).ReleaseOnClose)
        { e.Dockable.Context = null; tools.Release(toolId); }
    }

    private void TrackDocument(IDockable? item)
    {
        if (item is IDocument && documents.Documents.Any(d => d.Id == item.Id)) documents.Activate(item.Id!);
    }

    private void CreateItems()
    {
        items.Clear();
        foreach (var document in documents.Documents)
        {
            Observe(document);
            items.Add(document.Id, new Document { Id = document.Id, Title = document.DisplayTitle, Context = document,
                CanFloat = false, CanClose = true, CanPin = false });
        }
        foreach (var tool in tools.Definitions)
            items.Add(tool.Id, new Tool { Id = tool.Id, Title = tool.Title, Context = tool.Optional ? null : tools.GetContent(tool.Id),
                CanDockAsDocument = false, CanFloat = true, CanClose = true, CanPin = false });
        DockableLocator = items.ToDictionary(p => p.Key, p => (Func<IDockable?>)(() => p.Value));
    }

    public override IRootDock CreateLayout()
    {
        placements.Clear();
        foreach (var tool in tools.Definitions.Where(t => t.ReleaseOnClose)) tools.Release(tool.Id);
        CreateItems();
        var documentDock = new DocumentDock { Id = "documents", IsCollapsable = false, CanCreateDocument = false,
            VisibleDockables = CreateList(documents.Documents.Select(d => items[d.Id]).ToArray()),
            ActiveDockable = documents.ActiveDocument is { } active ? items[active.Id] : null };
        var visible = new HashSet<string>();
        ToolDock Group(string id, string toolId, Alignment alignment, double proportion)
        {
            visible.Add(toolId); items[toolId].Context ??= tools.GetContent(toolId);
            return new() { Id = id, Alignment = alignment, Proportion = proportion,
                VisibleDockables = CreateList(items[toolId]), ActiveDockable = items[toolId] };
        }
        var rightGroups = new List<IDockable>();
        foreach (var id in new[] { NavigatorId, FieldInspectorId }.Where(items.ContainsKey))
        {
            if (rightGroups.Count > 0) rightGroups.Add(new ProportionalDockSplitter());
            rightGroups.Add(Group(id == NavigatorId ? "navigator-dock" : "field-inspector-dock", id, Alignment.Right, .5));
        }
        // Relative proportions are initial preferences. Dock splitters remain free
        // to resize, collapse and reparent independent tool groups at any window size.
        IDockable upper = documentDock;
        if (rightGroups.Count > 0)
        {
            documentDock.Proportion = .78;
            var right = new ProportionalDock { Id = "inspection-stack", Orientation = Orientation.Vertical,
                Proportion = .22, VisibleDockables = CreateList(rightGroups.ToArray()) };
            upper = new ProportionalDock { Id = "workspace-content", Orientation = Orientation.Horizontal,
                VisibleDockables = CreateList<IDockable>(documentDock, new ProportionalDockSplitter(), right) };
        }
        IDockable split = upper;
        if (items.ContainsKey(ActivityLogId) && tools.Definition(ActivityLogId) is { } activity && (!activity.Optional || activity.RestoreVisibility))
        {
            upper.Proportion = .73;
            split = new ProportionalDock { Id = "workspace-main", Orientation = Orientation.Vertical,
                VisibleDockables = CreateList(upper, new ProportionalDockSplitter(), Group("diagnostics-dock", ActivityLogId, Alignment.Bottom, .27)) };
        }
        Root = new RootDock { Id = "workspace", IsCollapsable = false, VisibleDockables = CreateList<IDockable>(split),
            ActiveDockable = split, DefaultDockable = split, HiddenDockables = CreateList(tools.Definitions.Where(t => !visible.Contains(t.Id)).Select(t => items[t.Id]).ToArray()),
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
            Observe(document);
            var item = new Document { Id = id, Title = document.DisplayTitle, Context = document,
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
        foreach (var document in observedDocuments) document.PropertyChanged -= DocumentTitleChanged;
        observedDocuments.Clear(); ConfirmDocumentCloseAsync = null; ConfirmScriptCloseAsync = null;
        ActiveDockableChanged -= ActiveDockableChangedHandler;
        DockableClosed -= DockableClosedHandler;
        DockableClosing -= DockableClosingHandler;
        foreach (var tool in tools.Definitions.Where(t => t.ReleaseOnClose)) tools.Release(tool.Id);
    }
    public void ShowTool(string id)
    {
        var item = items[id];
        item.Context ??= tools.GetContent(id);
        var alreadyVisible = item.Owner is IToolDock dockOwner && dockOwner.VisibleDockables?.Contains(item) == true;
        if (!alreadyVisible && placements.TryGetValue(id, out var placement))
        {
            Root.HiddenDockables?.Remove(item);
            var target = FindGroup(placement.GroupId) ?? placement.Peers.Select(peer => items.GetValueOrDefault(peer)?.Owner).OfType<IToolDock>()
                .FirstOrDefault(d => d.VisibleDockables?.Any(t => t.Id != id) == true);
            if (target is null && placement.Floating is null) target = RecreateGroup(placement);
            if (target is not null)
            {
                if (item.Owner is IDock owner && owner.VisibleDockables?.Contains(item) == true) RemoveDockable(item, false);
                AddDockable(target, item);
                var tabs = target.VisibleDockables!; tabs.Remove(item); tabs.Insert(Math.Min(placement.TabIndex, tabs.Count), item);
                SetActiveDockable(item); ActivateWindow(item); return;
            }
        }
        RestoreDockable(item);
        // Hidden layouts have no serialized OriginalOwner. Supply a valid tool group.
        if (item.Owner is not IToolDock || item.Owner is IDocumentDock ||
            ((IDock)item.Owner).VisibleDockables?.Contains(item) != true)
        {
            if (item.Owner is IDock owner && owner.VisibleDockables?.Contains(item) == true) RemoveDockable(item, false);
            Root.HiddenDockables?.Remove(item);
            var target = FindDockable(Root, d => d is IToolDock { Alignment: Alignment.Bottom }) as IToolDock ??
                FindDockable(Root, d => d is IToolDock && d is not IDocumentDock) as IToolDock;
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
        if (placements.TryGetValue(id, out var floatingPlacement) && floatingPlacement.Floating is { } bounds &&
            !IsFloating(item))
        {
            FloatDockable(item);
            var window = FindWindow(item);
            if (window is not null)
            {
                var position = screens.Count > 0 ? WindowRecovery.Recover(bounds.X, bounds.Y, screens) : (bounds.X, bounds.Y);
                window.X = position.Item1; window.Y = position.Item2; window.Width = bounds.Width; window.Height = bounds.Height;
                window.Host?.SetPosition(window.X, window.Y); window.Host?.SetSize(bounds.Width, bounds.Height);
                if (item.Owner is IToolDock group) group.Id = floatingPlacement.GroupId;
            }
        }
        ActivateWindow(item);
    }
    private static string Identity(IDockable item)
    {
        if (string.IsNullOrWhiteSpace(item.Id)) item.Id = "dock." + Guid.NewGuid().ToString("N");
        return item.Id;
    }
    private IDockWindow? FindWindow(IDockable item)
    {
        IDockable root = item; while (root.Owner is { } owner) root = owner;
        return Root.Windows?.FirstOrDefault(w => ReferenceEquals(w.Layout, root));
    }
    private bool IsFloating(IDockable item) => FindWindow(item) is not null;
    private IToolDock? FindGroup(string id) => FindDockable(Root, d => d is IToolDock && d.Id == id) as IToolDock ??
        Root.Windows?.Select(w => w.Layout is null ? null : FindDockable(w.Layout, d => d is IToolDock && d.Id == id)).OfType<IToolDock>().FirstOrDefault();
    private void Remember(ITool item)
    {
        NormalizeGroupIdentities();
        if (item.Owner is not IToolDock group || group.VisibleDockables?.Contains(item) != true) return;
        var containers = new List<ContainerSlot>();
        IDockable current = group;
        while (current.Owner is IProportionalDock parent)
        {
            containers.Add(new(Identity(parent), parent.Orientation == Orientation.Vertical,
                double.IsFinite(parent.Proportion) && parent.Proportion > 0 ? parent.Proportion : null,
                (parent.Owner as IDock)?.VisibleDockables?.IndexOf(parent) ?? 0)); current = parent;
        }
        var window = FindWindow(item); window?.Save();
        placements[item.Id!] = new(item.Id!, Identity(group), group.Alignment.ToString(),
            double.IsFinite(group.Proportion) && group.Proportion > 0 ? group.Proportion : null,
            group.VisibleDockables!.IndexOf(item), group.VisibleDockables.OfType<ITool>().Where(t => t != item).Select(t => t.Id!).ToArray(),
            containers.ToArray(), window is null ? null : new(window.X, window.Y, window.Width, window.Height),
            (group.Owner as IDock)?.VisibleDockables?.IndexOf(group) ?? 0);
    }
    private void NormalizeGroupIdentities()
    {
        var used = items.Keys.ToHashSet(StringComparer.Ordinal);
        void Visit(IDockable item)
        {
            if (item is not ITool and not IDocument && (string.IsNullOrWhiteSpace(item.Id) || !used.Add(item.Id)))
            { item.Id = "dock." + Guid.NewGuid().ToString("N"); used.Add(item.Id); }
            if (item is IDock dock) foreach (var child in dock.VisibleDockables ?? []) Visit(child);
        }
        Visit(Root);
        foreach (var window in Root.Windows ?? []) if (window.Layout is { } root) Visit(root);
    }
    private IToolDock RecreateGroup(ToolPlacement place)
    {
        IDock parent = Root;
        foreach (var slot in place.Containers.Reverse())
        {
            var existing = FindDockable(Root, d => d is IProportionalDock && d.Id == slot.Id) as IProportionalDock;
            if (existing is not null) { parent = existing; continue; }
            var container = new ProportionalDock { Id = slot.Id, Orientation = slot.Vertical ? Orientation.Vertical : Orientation.Horizontal,
                Proportion = slot.Proportion ?? double.NaN, VisibleDockables = CreateList<IDockable>() };
            // Dock collapses a split by promoting its surviving child at the former slot.
            // Replace that slot directly: RemoveDockable would collapse the parent again.
            var children = parent.VisibleDockables;
            if (children is { Count: > 0 })
            {
                var survivorIndex = Math.Min(slot.Index, children.Count - 1);
                while (survivorIndex > 0 && children[survivorIndex] is IProportionalDockSplitter) survivorIndex--;
                var survivor = children[survivorIndex]; children[survivorIndex] = container;
                InitDockable(container, parent); AddDockable(container, survivor);
                if (parent.ActiveDockable == survivor) parent.ActiveDockable = container;
                if (parent is IRootDock root && root.DefaultDockable == survivor) root.DefaultDockable = container;
            }
            else AddDockable(parent, container);
            parent = container;
        }
        var group = new ToolDock { Id = place.GroupId, Alignment = Enum.Parse<Alignment>(place.Alignment),
            Proportion = place.Proportion ?? .25, VisibleDockables = CreateList<IDockable>() };
        var separator = new ProportionalDockSplitter();
        AddDockable(parent, group); AddDockable(parent, separator);
        var list = parent.VisibleDockables!; list.Remove(group); list.Remove(separator);
        var index = Math.Min(place.GroupIndex, list.Count);
        if (index == 0) { list.Insert(0, group); if (list.Count > 1) list.Insert(1, separator); }
        else { list.Insert(index, separator); list.Insert(index + 1, group); }
        return group;
    }

    public WorkspaceLayout Capture()
    {
        LayoutNode Node(IDockable item)
        {
            var kind = item switch { IRootDock => "root", IDocumentDock => "documents", IToolDock => "tools",
                IProportionalDock => "split", IProportionalDockSplitter => "splitter", ITool => "tool", IDocument => "document",
                _ => throw new InvalidDataException("Unsupported dock type.") };
            var proportion = double.IsFinite(item.Proportion) && item.Proportion > 0 ? item.Proportion : (double?)null;
            return new(kind, Identity(item), proportion,
                (item as IDock)?.ActiveDockable?.Id, (item as IToolDock)?.Alignment.ToString(),
                (item as IDock)?.VisibleDockables?.Select(Node).ToArray(),
                item is IProportionalDock split && split.Orientation == Orientation.Vertical);
        }
        foreach (var window in Root.Windows ?? []) window.Save();
        foreach (var tool in items.Values.OfType<ITool>()) Remember(tool);
        return new(2, Node(Root), (Root.Windows ?? []).Where(w => w.Layout != null).Select(w =>
            new FloatingLayout(Node(w.Layout!), w.X, w.Y, w.Width, w.Height)).ToArray(),
            (Root.HiddenDockables ?? []).OfType<ITool>().Select(t => t.Id!).ToArray(), documents.ActiveDocument?.Id, placements.Values.ToArray());
    }

    public IRootDock Restore(WorkspaceLayout layout, IReadOnlyList<ScreenArea> screens)
    {
        // Validate before constructing a live graph. Unknown identities are omitted.
        LayoutCodec.Encode(layout);
        this.screens = screens; placements.Clear();
        foreach (var place in layout.ToolPlacements ?? []) if (tools.Definitions.Any(t => t.Id == place.Id)) placements[place.Id] = place;
        // Older Diagnostics layouts predate closed-tool placements. Retain the old
        // slot before rebuilding the graph, including later close/reopen recovery.
        void RememberLegacyActivity(LayoutNode node, ContainerSlot[] ancestors, int groupIndex, FloatingBounds? floating)
        {
            if (node.Kind == "tools" && node.Children?.Any(c => c.Kind == "tool" && c.Id == ActivityLogId) == true &&
                tools.Definitions.Any(t => t.Id == ActivityLogId && t.Optional) && !placements.ContainsKey(ActivityLogId))
                placements[ActivityLogId] = new(ActivityLogId, node.Id ?? "dock.legacy-activity", node.Alignment ?? "Bottom", node.Proportion,
                    Array.FindIndex(node.Children, c => c.Id == ActivityLogId), node.Children.Where(c => c.Kind == "tool" && c.Id != ActivityLogId).Select(c => c.Id!).ToArray(),
                    ancestors.Reverse().ToArray(), floating, groupIndex);
            var next = node.Kind == "split" && node.Id is { } id ? ancestors.Append(new ContainerSlot(id, node.Vertical, node.Proportion, groupIndex)).ToArray() : ancestors;
            for (var i = 0; i < (node.Children?.Length ?? 0); i++) RememberLegacyActivity(node.Children![i], next, i, floating);
        }
        RememberLegacyActivity(layout.Root, [], 0, null);
        foreach (var window in layout.Windows) RememberLegacyActivity(window.Root, [], 0, new(window.X, window.Y, window.Width, window.Height));
        foreach (var tool in tools.Definitions.Where(t => t.ReleaseOnClose)) tools.Release(tool.Id);
        CreateItems();
        var used = new HashSet<string>();
        var newDefaultTools = new List<string>();
        IDockable? Node(LayoutNode node)
        {
            if (node.Kind is "tool" or "document")
            {
                if (!items.TryGetValue(node.Id!, out var item)) return null;
                if ((node.Kind == "document") != (item.Context is MessageDocument)) throw new InvalidDataException("Identity kind mismatch.");
                if (node.Kind == "tool")
                {
                    var definition = tools.Definition(node.Id!);
                    if (definition.Optional && !definition.RestoreVisibility) return null;
                    item.Context ??= tools.GetContent(node.Id!);
                }
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
            result.Id = node.Id ?? (node.Kind == "tools" && node.Children?.Any(c => c.Id == ActivityLogId) == true ? "dock.legacy-activity" : "dock." + Guid.NewGuid().ToString("N"));
            if (result is IDock dock)
            {
                var children = (node.Children ?? []).Select(Node).OfType<IDockable>().ToArray();
                if (node.Kind == "documents" && children.Any(c => c.Context is not MessageDocument) ||
                    node.Kind == "tools" && children.Any(c => c is not ITool)) throw new InvalidDataException("Invalid tab group.");
                dock.VisibleDockables = CreateList(children);
                if (node.Kind == "tools" && children.Length == 0) return null;
                if (node.Kind == "split")
                {
                    // Removing optional slots must not leave a floating empty host or dangling splitter.
                    var filtered = children.Where((c, i) => c is not IProportionalDockSplitter || i > 0 && i < children.Length - 1 && children[i - 1] is not IProportionalDockSplitter && children[i + 1] is not IProportionalDockSplitter).ToArray();
                    dock.VisibleDockables = CreateList(filtered); children = filtered;
                }
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
            if (!HasTools(floatingRoot)) continue;
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
            if (used.Add(tool.Id))
            {
                Root.HiddenDockables.Add(items[tool.Id]);
                // A layout from before structural tools existed did not close them.
                // Migrate only absent identities; explicit hidden tools remain hidden.
                if (tool.Id is NavigatorId or FieldInspectorId) newDefaultTools.Add(tool.Id);
            }
        InitLayout(Root);
        foreach (var id in newDefaultTools) ShowTool(id);
        if (layout.ActiveDocumentId is { } active && documents.Documents.Any(d => d.Id == active)) OpenDocument(active);
        return Root;
    }
    private static bool HasTools(IDockable item) => item is ITool || item is IDock dock && (dock.VisibleDockables?.Any(HasTools) ?? false);
}
