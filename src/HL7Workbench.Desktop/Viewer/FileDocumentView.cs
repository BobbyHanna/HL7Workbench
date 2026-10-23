using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Stable file tab; message changes replace only document-internal content.</summary>
public sealed class FileDocumentView : ContentControl
{
    private readonly MessageDocument document;
    private readonly bool dockedInspection;
    private readonly Func<Action, TimeSpan, IDisposable>? scheduleHover;
    private readonly WrapPanel toolbar = new() { Orientation = Orientation.Horizontal, Margin = new(4) };
    private readonly StackPanel navigation = new() { Spacing = 4 };
    private readonly List<Button> directions = [];
    private readonly TextBlock count = new();
    private readonly TextBlock status = new();
    private readonly Button go = new() { Content = "Go" };
    private readonly Button retry = new() { Content = "Retry" };
    private readonly Button cancel = new() { Content = "Cancel scan" };
    public Button AddMessage { get; } = new() { Content = "Add Message", Margin = new(2) };
    public Button ExtractFields { get; } = new() { Content = "Extract fields", Margin = new(2) };
    public UiCommand NextMessageCommand { get; }
    public IReadOnlyDictionary<MessageInsertionPosition, UiCommand> InsertionCommands { get; }
    public MessageDocument Document => document;
    public DeidentificationReportIndicator DeidentificationIndicator { get; }
    public GenerationReportIndicator GenerationIndicator { get; }
    public string? NavigationStatus => status.Text;
    private readonly ComboBox regions = new() { Width = 190 };
    private readonly Expander regionInspector = new() { Header = "Inspect source bytes" };
    private bool subscribed;
    private bool updatingNavigation;
    public ComboBox FilteredMessages { get; } = new() { Width = 175, PlaceholderText = "Matching positions", IsVisible = false,
        MaxDropDownHeight = 256, ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() => new VirtualizingStackPanel()) };
    private MessageId? displayedId;
    public TextBox MessageNumberInput { get; } = new() { Width = 90 };
    public MessageInspectionView? ActiveView { get; private set; }
    public FileDocumentView(MessageDocument document, bool dockedInspection = false)
        : this(document, dockedInspection, null) { }
    internal FileDocumentView(MessageDocument document, bool dockedInspection, Func<Action, TimeSpan, IDisposable>? scheduleHover)
    {
        this.scheduleHover = scheduleHover;
        this.document = document;
        this.dockedInspection = dockedInspection;
        NextMessageCommand = new(NextAsync, CanGoNext);
        InsertionCommands = MessageInsertionWorkflow.Choices.ToDictionary(c => c.Position,
            c => new UiCommand(async () => { await InsertMessageAsync(c.Position); }, () => MessageInsertionWorkflow.CanInsert(document, c.Position)));
        var addMenu = new MenuFlyout();
        foreach (var item in MessageInsertionWorkflow.MenuItems(p => InsertionCommands[p])) addMenu.Items.Add(item);
        AddMessage.Flyout = addMenu;
        void Button(string title, string tip, Func<int> position)
        {
            var button = new Button { Content = title, Margin = new(2) };
            ToolTip.SetTip(button, tip);
            if (title == ">") button.Command = NextMessageCommand;
            else button.Click += async (_, _) => await NavigateAsync(position());
            directions.Add(button); toolbar.Children.Add(button);
        }
        Button("|<", "First matching/logical message", () => NavigatorTarget(0)); Button("<", "Previous matching/logical message", () => NavigatorTarget(1));
        ToolTip.SetTip(MessageNumberInput, "Message number within this file"); ToolTip.SetTip(go, "Jump to message number");
        ToolTip.SetTip(retry, "Retry preparing the selected message"); ToolTip.SetTip(cancel, "Cancel preparing the selected message");
        ToolTip.SetTip(regions, "Select a retained unclassified source region"); ToolTip.SetTip(regionInspector, "Inspect original bytes outside classified messages");
        toolbar.Children.Add(MessageNumberInput); toolbar.Children.Add(count);
        toolbar.Children.Add(FilteredMessages);
        FilteredMessages.SelectionChanged += async (_, _) =>
        { if (!updatingNavigation && FilteredMessages.SelectedItem is int number && number != document.MessageNumber) await NavigateAsync(number); };
        MessageNumberInput.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await JumpAsync(); } };
        go.Click += async (_, _) => await JumpAsync(); toolbar.Children.Add(go);
        Button(">", "Next matching/logical message", () => NavigatorTarget(2)); Button(">|", "Last matching/logical message", () => NavigatorTarget(3));
        toolbar.Children.Add(AddMessage);
        ExtractFields.Click += (_, _) => RaiseEvent(new RawInspectorRequestEventArgs(document, null, null) { RoutedEvent = ViewerRequests.FieldExtractionEvent });
        toolbar.Children.Add(ExtractFields);
        retry.Click += async (_, _) => await NavigateAsync(document.MessageNumber);
        toolbar.Children.Add(retry);
        cancel.Click += (_, _) => document.CancelScan(); toolbar.Children.Add(cancel);
        regions.ItemsSource = document.Index?.Regions.Where(r => r.MessageNumber is null).ToArray();
        regions.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SourceRegion>((r, _) => new TextBlock { Text = r?.Reason ?? "" });
        regions.SelectionChanged += (_, _) =>
        {
            if (regions.SelectedItem is SourceRegion region && document.Snapshot is { } snapshot)
            { regionInspector.Content = new SourceRegionInspector(snapshot, document.Index!.Encoding, region, document); regionInspector.IsExpanded = true; }
        };
        if (regions.ItemCount > 0)
        { toolbar.Children.Add(new TextBlock { Text = "Source regions", VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(regions); }
        navigation.Children.Add(status); navigation.Children.Add(regionInspector); navigation.Children.Add(toolbar);
        DeidentificationIndicator = new(document); navigation.Children.Add(DeidentificationIndicator);
        GenerationIndicator = new(document); navigation.Children.Add(GenerationIndicator);
        Refresh();
    }
    public async Task JumpAsync()
    {
        if (!int.TryParse(MessageNumberInput.Text, out var number) || number < 1 || number > document.MessageCount)
        { status.Text = $"Enter a whole message number from 1 to {document.MessageCount}."; return; }
        await NavigateAsync(number);
    }
    public async Task NavigateAsync(int number)
    {
        if (document.IsSaving || document.IsPreparingInsertion || number < 1 || number > document.MessageCount) return;
        var id = document.GetMessageId(number);
        if (!document.IsNavigationMatch(id))
        { status.Text = "This message is outside the search filter. Choose a matching position or clear the filter."; return; }
        try { await document.NavigateAsync(id); }
        catch (ObjectDisposedException) { }
    }
    /// <summary>Filtered navigation never appends; only unfiltered logical EOF offers a blank insertion.</summary>
    public async Task NextAsync()
    {
        if (document.IsSaving || document.IsPreparingInsertion || document.IsCapturingScript || document.IsLoading) return;
        if (document.NavigationMatches is not null || document.MessageNumber < document.MessageCount)
        { await NavigateAsync(NavigatorTarget(2)); return; }
        if (document.MessageNumber == 0) return;
        if (document.IsEmptyTrailingInsertion)
        {
            status.Text = "The trailing draft is blank. Paste or type HL7 here before adding another with Next Message.";
            var blankId = document.ActiveMessageId!.Value;
            await Dispatcher.UIThread.InvokeAsync(() => FocusInsertedMessage(blankId), DispatcherPriority.Loaded);
            return;
        }
        await InsertMessageAsync(MessageInsertionPosition.End);
    }
    private bool CanGoNext() => !document.IsLoading && !document.IsSaving && !document.IsPreparingInsertion && !document.IsCapturingScript &&
        (NavigatorTarget(2) is > 0 and var target && target <= document.MessageCount ||
         document.NavigationMatches is null && document.MessageNumber > 0 && document.MessageNumber == document.MessageCount &&
         MessageInsertionWorkflow.CanInsert(document, MessageInsertionPosition.End));
    public async Task<bool> InsertMessageAsync(MessageInsertionPosition position)
    {
        if (!MessageInsertionWorkflow.TryInsert(document, position, out var id, out var feedback))
        { status.Text = feedback; return false; }
        status.Text = feedback;
        // Run after menu/popup dismissal. Insertion mounts the bounded blank editor
        // synchronously; parsing may still be pending and must not delay paste input.
        await Dispatcher.UIThread.InvokeAsync(() => FocusInsertedMessage(id), DispatcherPriority.Loaded);
        return true;
    }
    internal void FocusInsertedMessage(MessageId id)
    {
        if (IsEffectivelyVisible && this.IsAttachedToVisualTree() && document.ActiveMessageId == id && document.CanEditText && ActiveView is { } view)
            view.Editor.TextArea.Focus();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!subscribed) { subscribed = true; document.PropertyChanged += Changed; Refresh(); }
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        document.PropertyChanged -= Changed; subscribed = false;
        base.OnDetachedFromVisualTree(e);
    }
    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MessageDocument.SaveProgress) && document.SaveProgress is { } save)
        { status.Text = $"{save.Phase}: {save.CompletedBytes:N0} / {save.TotalBytes:N0} bytes"; return; }
        if (e.PropertyName == nameof(MessageDocument.NavigationMatches)) { UpdateNavigator(); return; }
        if (e.PropertyName == nameof(MessageDocument.PreparedBytes)) status.Text = $"Preparing message {document.MessageNumber}; {document.PreparedBytes:N0} bytes decoded…";
        else if (ActiveView is not null && displayedId == document.ActiveMessageId && !document.IsLoading && document.Source is null)
        { UpdateNavigator(); status.Text = document.Failure ?? ""; }
        else if (e.PropertyName == "ActiveContent") Refresh();
    }
    private void Refresh()
    {
        UpdateNavigator();
        MessageNumberInput.IsEnabled = document.MessageCount > 0;
        go.IsEnabled = document.MessageCount > 0;
        retry.IsVisible = document.Failure is not null && document.MessageCount > 0;
        cancel.IsVisible = document.IsLoading;
        status.Text = document.IsLoading ? $"Preparing message {document.MessageNumber}…" : document.Failure ??
            (regions.ItemCount > 0 ? "Source regions are retained for byte inspection." : "");
        regionInspector.IsVisible = regions.ItemCount > 0 || document.Failure is not null;
        // Reparent the fixed navigation controls only after the old view is detached.
        if (navigation.Parent is Panel parent) parent.Children.Remove(navigation);
        Content = null;
        displayedId = document.ActiveMessageId;
        if (!document.IsLoading && (document.EditableText is not null || document.Source is not null))
        {
            ActiveView = (MessageInspectionView)MessageViewer.CreateActive(document, dockedInspection, scheduleHover);
            Grid.SetRow(navigation, 2); ActiveView.RawPane.Children.Add(navigation); Content = ActiveView;
        }
        else
        {
            ActiveView = null;
            var panel = new Grid { RowDefinitions = new("*,Auto") };
            panel.Children.Add(new TextBlock { Margin = new(16), Text = document.IsLoading ? "Scanning active message…" : document.Failure ?? "No classified messages. Inspect retained source regions." });
            Grid.SetRow(navigation, 1); panel.Children.Add(navigation); Content = panel;
            if (document.Failure is not null && document.Index is { } index && document.Snapshot is { } snapshot && document.ActiveOriginalMessage is { } entry)
            {
                regionInspector.Content = new SourceRegionInspector(snapshot, index.Encoding, new(entry.SourceRange, null, "Active message bytes"), document);
                regionInspector.IsExpanded = true;
            }
        }
    }
    private static int LowerBound(IReadOnlyList<int> values, int number)
    {
        var low = 0; var high = values.Count;
        while (low < high) { var middle = low + (high - low) / 2; if (values[middle] < number) low = middle + 1; else high = middle; }
        return low;
    }
    private int NavigatorTarget(int direction)
    {
        if (document.NavigationMatches is not { } matches)
            return direction switch { 0 => 1, 1 => document.MessageNumber - 1, 2 => document.MessageNumber + 1, _ => document.MessageCount };
        if (matches.Count == 0) return 0;
        var at = LowerBound(matches, document.MessageNumber);
        return direction switch
        {
            0 => matches[0], 3 => matches[^1],
            1 => at > 0 ? matches[at - 1] : 0,
            _ => at < matches.Count && matches[at] == document.MessageNumber ? (at + 1 < matches.Count ? matches[at + 1] : 0) : (at < matches.Count ? matches[at] : 0)
        };
    }
    private void UpdateNavigator()
    {
        updatingNavigation = true;
        try
        {
            MessageNumberInput.Text = document.MessageNumber.ToString();
            count.Text = $"/ {document.MessageCount:N0}";
            var matches = document.NavigationMatches; FilteredMessages.IsVisible = matches is not null;
            if (!ReferenceEquals(FilteredMessages.ItemsSource, matches)) FilteredMessages.ItemsSource = matches;
            if (matches is not null)
            {
                var at = LowerBound(matches, document.MessageNumber); var included = at < matches.Count && matches[at] == document.MessageNumber;
                FilteredMessages.SelectedItem = included ? document.MessageNumber : null;
                count.Text += $" · {matches.Count:N0} matches (logical positions)" + (included ? "" : " · current message excluded");
            }
            for (var i = 0; i < directions.Count; i++)
            {
                var target = NavigatorTarget(i);
                directions[i].IsEnabled = i == 2 ? CanGoNext() : !document.IsLoading && !document.IsSaving && !document.IsPreparingInsertion && !document.IsCapturingScript &&
                    target > 0 && target <= document.MessageCount && target != document.MessageNumber;
            }
            NextMessageCommand.Refresh();
            foreach (var command in InsertionCommands.Values) command.Refresh();
            AddMessage.IsEnabled = MessageInsertionWorkflow.CanInsert(document, MessageInsertionPosition.End);
            ToolTip.SetTip(AddMessage, document.InsertionUnavailableReason ?? "Insert a blank draft before/after the current message, at the beginning or at the end.");
            if (directions.Count > 2) ToolTip.SetTip(directions[2], document.NavigationMatches is not null ? "Next matching message (never appends while filtered)" :
                document.MessageNumber == document.MessageCount ? document.InsertionUnavailableReason ?? "Add a blank message at document EOF; an empty trailing draft is reused." : "Next logical message");
        }
        finally { updatingNavigation = false; }
    }
}
