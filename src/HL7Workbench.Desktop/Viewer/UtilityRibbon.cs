using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Small utility ribbon. Unpinned commands use a light-dismiss popup so
/// temporary expansion never changes the measured Dock viewport.</summary>
public sealed class UtilityRibbon : Grid
{
    private readonly ApplicationPreferences preferences;
    private readonly ScriptEditorViewModel scripts;
    private readonly IReadOnlyDictionary<string, ICommand> actions;
    private bool subscribed;
    private readonly ContentControl temporaryHost = new();
    public int SelectedTab { get; private set; }
    public ToggleButton[] Tabs { get; } = [new() { Content = "Home" }, new() { Content = "Scripts" }, new() { Content = "HL7 Tools" }, new() { Content = "TCP/MLLP" }];
    public Button Pin { get; } = new();
    public ContentControl PinnedHost { get; } = new();
    public Popup TemporaryPopup { get; } = new() { IsLightDismissEnabled = true, ShouldUseOverlayLayer = true, Placement = PlacementMode.BottomEdgeAlignedLeft };
    public bool IsExpanded => preferences.Current.RibbonExpanded;
    public UtilityRibbon(ApplicationPreferences preferences, ScriptEditorViewModel scripts, IReadOnlyDictionary<string, ICommand> actions)
    {
        this.preferences = preferences; this.scripts = scripts; this.actions = actions;
        RowDefinitions = new("Auto,Auto");
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        for (var i = 0; i < Tabs.Length; i++)
        {
            var index = i; var button = Tabs[i]; button.Padding = new Thickness(12, 3); button.MinHeight = 27;
            ToolTip.SetTip(button, "Show " + button.Content + " commands. Use Left/Right to choose a tab, Down to open, Escape to dismiss.");
            button.Click += (_, _) =>
            {
                if (!IsExpanded && TemporaryPopup.IsOpen && SelectedTab == index) TemporaryPopup.IsOpen = false;
                else SelectTab(index);
            };
            tabs.Children.Add(button);
        }
        Pin.Padding = new Thickness(10, 3); Pin.MinHeight = 27;
        Pin.Click += (_, _) => preferences.Apply(preferences.Current with { RibbonExpanded = !IsExpanded });
        var tabRow = new DockPanel(); DockPanel.SetDock(Pin, Avalonia.Controls.Dock.Right); tabRow.Children.Add(Pin); tabRow.Children.Add(tabs);
        Children.Add(tabRow); SetRow(PinnedHost, 1); Children.Add(PinnedHost);
        // Popup needs the shell's logical tree even though its commands render in
        // an overlay. Otherwise IsOpen can be true without creating a popup host.
        Children.Add(TemporaryPopup);
        TemporaryPopup.PlacementTarget = tabRow;
        // Only the tab/pin strip passes through the light-dismiss overlay. A tab
        // press must reach its button before dismissal; unrelated outside clicks
        // still dismiss and are consumed rather than activating document commands.
        TemporaryPopup.OverlayInputPassThroughElement = tabRow;
        TemporaryPopup.Child = temporaryHost;
        temporaryHost.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            { TemporaryPopup.IsOpen = false; Tabs[SelectedTab].Focus(); e.Handled = true; }
        };
        SizeChanged += (_, _) => TemporaryPopup.Width = Math.Max(400, Bounds.Width);
        tabs.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Left or Key.Right or Key.Home or Key.End)
            {
                var index = e.Key switch { Key.Home => 0, Key.End => Tabs.Length - 1, Key.Left => (SelectedTab + Tabs.Length - 1) % Tabs.Length, _ => (SelectedTab + 1) % Tabs.Length };
                SelectTab(index); Tabs[index].Focus(); e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                SelectTab(SelectedTab);
                Dispatcher.UIThread.Post(() =>
                {
                    var content = IsExpanded ? PinnedHost.Content as Control : TemporaryPopup.Child;
                    content?.UpdateLayout();
                    content?.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c is Avalonia.Controls.Button or ComboBox && c.IsEffectivelyEnabled && c.IsEffectivelyVisible)?.Focus();
                });
                e.Handled = true;
            }
            else if (e.Key == Key.Escape) { TemporaryPopup.IsOpen = false; e.Handled = true; }
        };
        AttachedToVisualTree += (_, _) => { if (!subscribed) { preferences.PropertyChanged += PreferencesChanged; subscribed = true; } Render(); };
        DetachedFromVisualTree += (_, _) => { TemporaryPopup.IsOpen = false; if (subscribed) preferences.PropertyChanged -= PreferencesChanged; subscribed = false; };
        Render();
    }
    public void SelectTab(int index)
    {
        if (index < 0 || index >= Tabs.Length) return;
        SelectedTab = index; Render();
        if (!IsExpanded) TemporaryPopup.IsOpen = true;
    }
    private void PreferencesChanged(object? sender, PropertyChangedEventArgs e) => Render();
    private void Render()
    {
        foreach (var (tab, index) in Tabs.Select((tab, index) => (tab, index))) tab.IsChecked = index == SelectedTab;
        Pin.Content = IsExpanded ? "Unpin ribbon" : "Pin ribbon";
        ToolTip.SetTip(Pin, (IsExpanded ? "Save collapsed ribbon preference." : "Keep commands expanded and save this preference.") + " " + preferences.Status);
        PinnedHost.IsVisible = IsExpanded;
        // Keep Popup.Child stable. Replacing this host's content switches commands
        // while the popup stays open, without recreating its dismissal/focus layer.
        PinnedHost.Content = null; temporaryHost.Content = null;
        var content = Commands();
        if (IsExpanded) { TemporaryPopup.IsOpen = false; PinnedHost.Content = content; }
        else temporaryHost.Content = content;
    }
    private Control Commands()
    {
        var groups = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 5) };
        void Add(string heading, Control commands)
        {
            if (groups.Children.Count > 0)
            {
                var divider = new Border { Width = 1, Margin = new Thickness(9, 0), VerticalAlignment = VerticalAlignment.Stretch };
                BindFluentResource(divider, Border.BackgroundProperty, "SystemControlForegroundBaseLowBrush"); groups.Children.Add(divider);
            }
            var caption = new TextBlock { Text = heading, FontSize = 12, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            caption.Classes.Add("ribbon-group-title"); BindFluentResource(caption, TextBlock.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            groups.Children.Add(new StackPanel { Spacing = 4, Children = { commands, caption } });
        }
        if (SelectedTab == 0)
        {
            Add("Files", Row(Existing("Open File…", "Open", large: true), TwoRows(Existing("Save HL7", "Save"), Existing("Save HL7 As…", "Save"))));
            Add("Workspace", Row(TwoRows(Existing("Save Layout", "Layout"), Existing("Restore Layout", "Restore"),
                Existing("Reset Layout / Recover Windows", "Restore")), Existing("Preferences…", "Settings", large: true)));
        }
        else if (SelectedTab == 1)
        {
            Add("Script Library", Row(CommandButton("New", scripts.NewCommand, "New", large: true), TwoRows(
                CommandButton("Save", scripts.SaveCommand, "Save"), CommandButton("Duplicate", scripts.DuplicateCommand, "Duplicate"),
                CommandButton("Rename", scripts.RenameCommand, "Rename"), CommandButton("Delete", scripts.DeleteCommand, "Delete"),
                CommandButton("Open Script Editor", scripts.OpenCommand, "Source"))));
            var scope = new ComboBox { ItemsSource = new[] { "Current message", "Whole document" }, SelectedIndex = (int)scripts.Scope, Width = 160, Height = 32, FontSize = 13, IsEnabled = scripts.CanManage };
            AutomationProperties.SetName(scope, "Apply to");
            scope.SelectionChanged += (_, _) => { if (scope.SelectedIndex >= 0) scripts.Scope = (Scripting.ScriptScope)scope.SelectedIndex; };
            void Changed(object? sender, PropertyChangedEventArgs e) { scope.SelectedIndex = (int)scripts.Scope; scope.IsEnabled = scripts.CanManage; }
            scope.AttachedToVisualTree += (_, _) => { scripts.PropertyChanged += Changed; Changed(null, new(null)); };
            scope.DetachedFromVisualTree += (_, _) => scripts.PropertyChanged -= Changed;
            var apply = new TextBlock { Text = "Apply to:", FontSize = 13 };
            BindFluentResource(apply, TextBlock.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            Add("Execution", Row(new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center, Children = { apply, scope } },
                CommandButton("Run", scripts.RunCommand, "Run", large: true, accent: true), CommandButton("Cancel", scripts.CancelCommand, "Cancel", large: true)));
            Add("Results", TwoRows(CommandButton("Review Output", scripts.ReviewCommand, "Review"), CommandButton("Reports", scripts.ReportsCommand, "Reports")));
        }
        else if (SelectedTab == 2)
        {
            Add("Navigation", Row(Existing("Message Browser", "Browser", large: true), Existing("Segment Navigator", "Navigator", large: true)));
            Add("Inspection", Row(Existing("Field Inspector", "Inspect", large: true), Existing("Full Source Viewer", "Source", large: true), Existing("Activity Log", "Reports", large: true)));
            Add("Search", TwoRows(Existing("Find Messages", "Inspect"), Existing("Clear Search / Restore Navigator", "Restore")));
        }
        else
        {
            var sender = CommandButton("Sender", null, "Send", large: true); sender.IsEnabled = false;
            var listener = CommandButton("Listener", null, "Listen", large: true); listener.IsEnabled = false;
            AutomationProperties.SetName(sender, "TCP/MLLP Sender — Planned"); AutomationProperties.SetName(listener, "TCP/MLLP Listener — Planned");
            ToolTip.SetTip(sender, "Planned. Network sending is not implemented."); ToolTip.SetTip(listener, "Planned. Network listening is not implemented.");
            Add("TCP/MLLP — Planned", Row(sender, listener));
        }
        var scroll = new ScrollViewer { Content = groups, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var border = new Border { BorderThickness = new Thickness(0, 1, 0, 1), Child = scroll };
        border.Classes.Add("ribbon-surface");
        // Resolve against the attached window/popup and react to theme changes.
        // A constructor-time lookup captures the wrong theme before inheritance.
        BindFluentResource(border, Border.BackgroundProperty, "SystemControlBackgroundChromeMediumLowBrush");
        BindFluentResource(border, Border.BorderBrushProperty, "SystemControlForegroundBaseLowBrush");
        return border;
    }
    private Button Existing(string name, string icon, bool large = false) => CommandButton(name, actions[name], icon, large);
    private Button CommandButton(string label, ICommand? command, string icon, bool large = false, bool accent = false)
    {
        var content = new StackPanel { Orientation = large ? Orientation.Vertical : Orientation.Horizontal, Spacing = large ? 4 : 7,
            HorizontalAlignment = large ? HorizontalAlignment.Center : HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new PathIcon { Data = Icon(icon), Width = large ? 24 : 16, Height = large ? 24 : 16 });
        content.Children.Add(new TextBlock { Text = label, FontSize = 13, HorizontalAlignment = large ? HorizontalAlignment.Center : HorizontalAlignment.Left });
        var button = new Button { Content = content, Command = command, FontSize = 13, Padding = new Thickness(8, 4), MinWidth = large ? 68 : 98,
            Height = large ? 64 : 30, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center };
        button.Classes.Add("ribbon-command"); if (accent) button.Classes.Add("accent");
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => { if (!IsExpanded) TemporaryPopup.IsOpen = false; };
        ToolTip.SetTip(button, label switch
        {
            "Reports" => "Reports / Export CSV — results from the last successful run",
            "Find Messages" => "Open Find Messages and focus the search value (Ctrl+F)",
            _ => label
        }); return button;
    }
    private static void BindFluentResource(AvaloniaObject control, AvaloniaProperty property, string key) => control.Bind(property, new DynamicResourceExtension(key));
    private static StackPanel Row(params Control[] controls)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        foreach (var control in controls) row.Children.Add(control); return row;
    }
    private static Grid TwoRows(params Control[] controls)
    {
        var grid = new Grid { RowDefinitions = new("30,30"), ColumnDefinitions = new(string.Join(",", Enumerable.Repeat("Auto", (controls.Length + 1) / 2))), RowSpacing = 4, ColumnSpacing = 4 };
        for (var i = 0; i < controls.Length; i++) { SetColumn(controls[i], i / 2); SetRow(controls[i], i % 2); grid.Children.Add(controls[i]); }
        return grid;
    }
    // Small code-native vector glyphs share the Fluent control foreground, including
    // disabled/hover/pressed states. No icon font, bitmap assets or extra theme.
    private static Geometry Icon(string name) => Geometry.Parse(name switch
    {
        "New" => "M3,1 H12 L17,6 V19 H3 Z M5,3 V17 H15 V7 H11 V3 Z M7,9 H9 V7 H11 V9 H13 V11 H11 V13 H9 V11 H7 Z",
        "Save" => "M2,2 H15 L18,5 V18 H2 Z M5,3 V8 H14 V3 Z M5,11 V17 H15 V11 Z M11,4 H13 V7 H11 Z",
        "Duplicate" => "M1,1 H13 V3 H3 V13 H1 Z M5,5 H19 V19 H5 Z M7,7 V17 H17 V7 Z",
        "Rename" => "M3,13 L13,3 L17,7 L7,17 L2,18 Z M14,2 L16,0 L20,4 L18,6 Z",
        "Delete" => "M4,5 H16 V19 H4 Z M6,7 V17 H14 V7 Z M2,2 H7 V0 H13 V2 H18 V4 H2 Z",
        "Run" or "Send" => "M4,1 L19,10 L4,19 Z",
        "Cancel" => "M3,3 H17 V17 H3 Z",
        "Review" => "M2,2 H11 V4 H4 V16 H16 V9 H18 V18 H2 Z M11,0 H20 V9 H18 V4 L10,12 L8,10 L16,2 H11 Z",
        "Reports" => "M2,2 H4 V17 H19 V19 H2 Z M6,10 H9 V15 H6 Z M11,6 H14 V15 H11 Z M16,2 H19 V15 H16 Z",
        "Open" => "M1,4 H8 L10,6 H19 V9 H4 L1,18 Z M5,10 H20 L16,18 H2 Z",
        "Layout" => "M1,2 H19 V18 H1 Z M3,4 V16 H12 V4 Z M14,4 V9 H17 V4 Z M14,11 V16 H17 V11 Z",
        "Restore" => "M1,2 H3 V7 L7,3 A8,8 0 1 1 4,16 L6,14 A6,6 0 1 0 9,5 L5,9 H10 V11 H1 Z",
        "Settings" => "M1,4 H7 V2 H10 V4 H19 V6 H10 V8 H7 V6 H1 Z M1,13 H12 V11 H15 V13 H19 V15 H15 V17 H12 V15 H1 Z",
        "Browser" => "M3,1 H19 V17 H17 V3 H3 Z M0,5 H15 V20 H0 Z M2,7 V18 H13 V7 Z",
        "Navigator" => "M1,3 H4 V6 H1 Z M7,3 H19 V6 H7 Z M1,9 H4 V12 H1 Z M7,9 H19 V12 H7 Z M1,15 H4 V18 H1 Z M7,15 H19 V18 H7 Z",
        "Inspect" => "M8,1 A7,7 0 1 1 3,13 A7,7 0 0 1 8,1 Z M8,3 A5,5 0 1 0 8,13 A5,5 0 0 0 8,3 Z M12,13 L14,11 L20,17 L18,19 Z",
        "Source" => "M6,3 L0,10 L6,17 L8,15 L3,10 L8,5 Z M14,3 L12,5 L17,10 L12,15 L14,17 L20,10 Z",
        "Listen" => "M1,10 L16,1 V19 Z M18,2 H20 V18 H18 Z",
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    });
}
