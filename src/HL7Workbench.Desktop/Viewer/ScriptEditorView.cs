using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Ordinary tool content. The retained session survives Dock rebuilding/floating this control.</summary>
public sealed class ScriptEditorView : Grid
{
    private readonly ScriptEditorViewModel model;
    private bool synchronizing;
    private bool subscribed;
    private bool wasRenaming;
    private IReadOnlyList<ScriptLibraryItem>? libraryItems;
    private ScriptLibraryItem? libraryDraft;
    private IReadOnlyList<ScriptLibraryItem> displayedLibrary = [];
    internal ScriptSemanticHighlighting SymbolHighlighting { get; }
    public Func<Task<UnsavedChangesDecision>> PromptUnsavedAsync { get => model.PromptUnsavedAsync; set => model.PromptUnsavedAsync = value; }
    public TextEditor Editor { get; } = new() { ShowLineNumbers = true, FontFamily = new("Consolas, monospace") };
    public ComboBox Library { get; } = new() { Width = 200 };
    public TextBox ScriptName { get; } = new() { Width = 180, MaxLength = 120, PlaceholderText = "Script name" };
    public Button NameDisplay { get; } = new() { Width = 180, HorizontalContentAlignment = HorizontalAlignment.Left };
    public Button Actions { get; } = new() { Content = "⋯" };
    public ComboBox ExecutionScope { get; } = new() { ItemsSource = new[] { "Current message", "Whole document" }, Width = 140 };
    public TextBlock ScopeLabel { get; } = new() { Text = "Apply to:", VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
    public Button New { get; } = new() { Content = "New" };
    public Button Save { get; } = new() { Content = "Save Script" };
    public MenuItem Duplicate { get; } = new() { Header = "Duplicate Script" };
    public MenuItem Rename { get; } = new() { Header = "Rename Script" };
    public MenuItem Delete { get; } = new() { Header = "Delete Script…" };
    public MenuItem Reports { get; } = new() { Header = "Reports / Export CSV…" };
    public Button Run { get; } = new() { Content = "Run" };
    public Button Cancel { get; } = new() { Content = "Cancel" };
    public MenuItem Review { get; } = new() { Header = "Review Output" };
    public TextBlock ExecutionStatus { get; } = new() { MaxWidth = 170, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
    public TextBlock Status { get; } = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
    public TextBlock OutputAttribution { get; } = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    public TextBlock Target { get; } = new() { MaxWidth = 300, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
    public ListBox Diagnostics { get; } = new() { MaxHeight = 96 };
    public Expander DiagnosticPanel { get; } = new() { IsExpanded = false, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Stretch };
    public Button ZoomOut { get; } = new() { Content = "−" };
    public Button ZoomReset { get; } = new();
    public Button ZoomIn { get; } = new() { Content = "+" };
    public ScrollViewer LibraryToolbar { get; }
    public ScrollViewer ExecutionToolbar { get; }
    public ScriptEditorView(ScriptEditorViewModel model, Func<Task<UnsavedChangesDecision>>? prompt = null)
    {
        this.model = model; if (prompt is not null) PromptUnsavedAsync = prompt;
        Margin = new(3); RowDefinitions = new("Auto,Auto,*,Auto,Auto,Auto");
        MinWidth = 260; MinHeight = 150;
        ScriptEditorPalette.Dark.Apply(Editor);
        SymbolHighlighting = new(Editor, ScriptEditorPalette.Dark);
        AutomationProperties.SetName(New, "New script");
        New.Command = model.NewCommand; Save.Command = model.SaveCommand; Duplicate.Command = model.DuplicateCommand;
        Rename.Command = NameDisplay.Command = model.RenameCommand; Delete.Command = model.DeleteCommand;
        Run.Command = model.RunCommand; Cancel.Command = model.CancelCommand; Review.Command = model.ReviewCommand; Reports.Command = model.ReportsCommand;
        var actions = new MenuFlyout();
        foreach (var item in new[] { Duplicate, Rename, Delete, Review, Reports }) actions.Items.Add(item);
        Actions.Flyout = actions; AutomationProperties.SetName(Actions, "Script actions");
        var nameHost = new Grid(); nameHost.Children.Add(NameDisplay); nameHost.Children.Add(ScriptName);
        LibraryToolbar = Toolbar(Library, nameHost, New, Save, Actions);
        Children.Add(LibraryToolbar);
        ExecutionToolbar = Toolbar(ScopeLabel, ExecutionScope, Run, Cancel, ExecutionStatus);
        SetRow(ExecutionToolbar, 1); Children.Add(ExecutionToolbar);
        SetRow(Editor, 2); Children.Add(Editor);
        SetRow(OutputAttribution, 3); Children.Add(OutputAttribution);
        var footer = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new(0, 1, 0, 0) };
        footer.Children.Add(Status);
        var zoomBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1 };
        foreach (var button in new[] { ZoomOut, ZoomReset, ZoomIn })
        { button.Padding = new(5, 0); button.MinHeight = 22; button.FontSize = 12; zoomBar.Children.Add(button); }
        SetColumn(zoomBar, 1); footer.Children.Add(zoomBar);
        SetRow(footer, 5); Children.Add(footer);
        Diagnostics.ItemTemplate = new FuncDataTemplate<HL7Workbench.Scripting.ScriptDiagnostic>((d, _) =>
            new TextBlock { Text = d is null ? "" : model.FormatDiagnostic(d), TextWrapping = TextWrapping.Wrap });
        DiagnosticPanel.Content = Diagnostics;
        ToolTip.SetTip(DiagnosticPanel, "Expand diagnostics, then double-click an entry to navigate to its script line.");
        SetRow(DiagnosticPanel, 4); Children.Add(DiagnosticPanel);
        Library.ItemTemplate = new FuncDataTemplate<ScriptLibraryItem>((item, _) => new TextBlock { Text = item is null ? "" : (item.Obsolete ? "[Needs update] " : "") + item.Name });
        ToolTip.SetTip(New, "Create one C# script with void Main() for either scope. Top-level state initializes once; optional void Complete() runs after all messages.");
        ToolTip.SetTip(NameDisplay, "Click to rename. Enter confirms; Escape cancels. Save Script persists the name and code. Duplicate bundled examples first.");
        ToolTip.SetTip(ScriptName, "Enter confirms the name; Escape cancels. Invalid names leave the previous name intact.");
        ToolTip.SetTip(Save, "Save script text and name locally (Ctrl+S). This does not save the HL7 document.");
        ToolTip.SetTip(Duplicate, "Save an editable copy, including the current draft, while retaining the original saved entry.");
        ToolTip.SetTip(Run, "Run trusted local C# with your account permissions. Results open as a new unsaved HL7 document.");
        ToolTip.SetTip(ZoomIn, "Zoom in: Ctrl+MouseWheel, Ctrl++ or Ctrl+NumPad Add in the code editor.");
        ToolTip.SetTip(ZoomOut, "Zoom out: Ctrl+MouseWheel, Ctrl+− or Ctrl+NumPad Subtract in the code editor.");
        ZoomOut.Click += (_, _) => model.Zoom(-1); ZoomIn.Click += (_, _) => model.Zoom(1);
        ZoomReset.Click += (_, _) => model.SetFontSizePoints(ScriptEditorPreferences.DefaultPoints);
        // Handle at the editor before its ScrollViewer consumes the wheel. Ordinary
        // scrolling and shortcuts in library/name controls keep their normal behavior.
        Editor.AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (e.KeyModifiers == KeyModifiers.Control && e.Delta.Y != 0)
            { model.Zoom(Math.Sign(e.Delta.Y)); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
        Editor.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Control && e.KeyModifiers != (KeyModifiers.Control | KeyModifiers.Shift)) return;
            if (e.Key is Key.OemPlus or Key.Add) { model.Zoom(1); e.Handled = true; }
            else if (e.Key is Key.OemMinus or Key.Subtract) { model.Zoom(-1); e.Handled = true; }
            else if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.D0 or Key.NumPad0)
            { model.SetFontSizePoints(ScriptEditorPreferences.DefaultPoints); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
        Editor.TextChanged += (_, _) =>
        {
            if (synchronizing) return;
            if (Editor.Text.Length > HL7Workbench.Scripting.ScriptLimits.ScriptCharacters)
            {
                // Routed input still has an open native undo group here. Replacing Text now
                // would clear that stack mid-transaction (the accepted Stage 4 crash invariant).
                var rejected = Editor.Text;
                Dispatcher.UIThread.Post(() =>
                {
                    if (Editor.Text != rejected) return;
                    synchronizing = true;
                    try { Editor.Text = model.Session.Source; }
                    finally { synchronizing = false; }
                    Status.Text = "Script limit is 65,536 characters; previous draft retained.";
                });
                return;
            }
            model.Session.Source = Editor.Text;
        };
        // TextChanged is deferred by the control. Track the property synchronously
        // so Enter/focus loss cannot confirm the value from the previous key event.
        ScriptName.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && !synchronizing && model.IsRenaming) model.RenameDraft = ScriptName.Text ?? ""; };
        ScriptName.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { model.CommitRename(); Editor.Focus(); e.Handled = true; }
            else if (e.Key == Key.Escape) { model.CancelRename(); Editor.Focus(); e.Handled = true; }
        };
        ScriptName.LostFocus += (_, _) => { if (model.IsRenaming) model.CommitRename(); };
        ExecutionScope.SelectionChanged += (_, _) => { if (!synchronizing) model.Scope = ExecutionScope.SelectedIndex == 1 ? Scripting.ScriptScope.WholeDocument : Scripting.ScriptScope.CurrentMessage; };
        Library.SelectionChanged += async (_, _) =>
        {
            if (synchronizing || !model.CanManage || Library.SelectedItem is not ScriptLibraryItem item || item.Id == model.Session.Id) return;
            await model.OpenLibraryAsync(item.Id); Refresh();
        };
        Diagnostics.DoubleTapped += (_, _) =>
        {
            if (Diagnostics.SelectedItem is Scripting.ScriptDiagnostic { Line: > 0 } diagnostic)
            { var line = Math.Min(diagnostic.Line.Value, Editor.Document.LineCount); Editor.ScrollToLine(line); Editor.TextArea.Caret.Line = line; Editor.Focus(); }
        };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            { e.Handled = true; model.SaveCommand.Execute(null); }
        }, RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) => { if (!subscribed) { model.PropertyChanged += ModelChanged; model.EditorShown += FocusEditor; subscribed = true; } Refresh(); SymbolHighlighting.Start(); };
        DetachedFromVisualTree += (_, _) => { if (subscribed) { model.PropertyChanged -= ModelChanged; model.EditorShown -= FocusEditor; subscribed = false; } SymbolHighlighting.Stop(); };
        SizeChanged += (_, _) => Target.IsVisible = Bounds.Width >= 650;
        Refresh();
    }
    private void FocusEditor()
    { if (model.IsRenaming) { ScriptName.Focus(); ScriptName.SelectAll(); } else Editor.Focus(); }
    private static ScrollViewer Toolbar(params Control[] controls)
    {
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        foreach (var control in controls)
        {
            if (control is TemplatedControl item)
            { item.FontSize = 12; item.MinHeight = 27; item.Padding = new(7, 2); }
            strip.Children.Add(control);
        }
        // Horizontal scrolling keeps small tool hosts from growing extra toolbar rows
        // until they consume the code area. Existing placements/splitters stay intact.
        return new ScrollViewer { Content = strip, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new(0, 0, 0, 2) };
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        synchronizing = true;
        try
        {
            if (Editor.Text != model.Session.Source) Editor.Text = model.Session.Source;
            var startingRename = model.IsRenaming && !wasRenaming;
            wasRenaming = model.IsRenaming;
            ScriptName.IsVisible = model.IsRenaming; NameDisplay.IsVisible = !model.IsRenaming;
            ScriptName.Text = model.IsRenaming ? model.RenameDraft : model.Session.Name;
            NameDisplay.Content = new TextBlock { Text = model.Session.DisplayName, TextTrimming = TextTrimming.CharacterEllipsis };
            Editor.IsReadOnly = model.Session.IsBundled || !model.CanManage;
            ScriptName.IsReadOnly = model.Session.IsBundled || !model.CanManage;
            if (startingRename) Dispatcher.UIThread.Post(() => { if (model.IsRenaming && TopLevel.GetTopLevel(ScriptName) is not null) { ScriptName.Focus(); ScriptName.SelectAll(); } });
            var draft = new ScriptLibraryItem(model.Session.Id, model.Session.DisplayName, model.Session.IsBundled, model.Session.IsObsolete);
            if (!ReferenceEquals(libraryItems, model.Session.Items) || libraryDraft != draft)
            {
                libraryItems = model.Session.Items; libraryDraft = draft;
                // Unsaved names/dirty state and obsolete-record warnings belong to this
                // transient projection, never to a rewritten library record on load.
                var items = libraryItems.Select(i => i.Id == draft.Id ? draft : i).ToList();
                if (!items.Any(i => i.Id == draft.Id)) items.Insert(0, draft);
                displayedLibrary = items;
                Library.ItemsSource = displayedLibrary;
            }
            Library.SelectedItem = displayedLibrary.First(i => i.Id == model.Session.Id);
            ToolTip.SetTip(Library, model.Session.IsObsolete ? ScriptEditorSession.MigrationNotice :
                $"Script Library — {model.Session.DisplayName}. One Main script works with either Apply to scope. Bundled examples require Duplicate before editing.");
            Library.IsEnabled = ExecutionScope.IsEnabled = model.CanManage;
            New.IsEnabled = model.NewCommand.CanExecute(null); Save.IsEnabled = model.SaveCommand.CanExecute(null);
            Duplicate.IsEnabled = model.DuplicateCommand.CanExecute(null); Rename.IsEnabled = NameDisplay.IsEnabled = model.RenameCommand.CanExecute(null);
            Delete.IsEnabled = model.DeleteCommand.CanExecute(null); Reports.IsEnabled = model.ReportsCommand.CanExecute(null);
            Run.IsEnabled = model.RunCommand.CanExecute(null); Cancel.IsEnabled = model.CancelCommand.CanExecute(null); Review.IsEnabled = model.ReviewCommand.CanExecute(null);
            ExecutionScope.SelectedIndex = model.Scope == Scripting.ScriptScope.WholeDocument ? 1 : 0;
            var origin = model.ReportOrigin?.Description ?? "No CSV reports from a successful run are available.";
            ToolTip.SetTip(Reports, origin + " Existing files require explicit replacement confirmation.");
            OutputAttribution.IsVisible = model.OutputOrigin is not null;
            OutputAttribution.Text = model.OutputOrigin is { } output ? $"Last output · {output.ScriptName} · {output.SourceDescription}" : "";
            ToolTip.SetTip(OutputAttribution, model.OutputOrigin?.Description);
            ExecutionStatus.Text = model.IsBusy ? "Running…" : model.Diagnostics.Count > 0 ? "Run failed" : model.Reports.Count > 0 ? $"{model.Reports.Count} reports available" : "Ready";
            ToolTip.SetTip(ExecutionStatus, model.Status);
            Target.Text = model.Target;
            ToolTip.SetTip(ExecutionScope, model.Target);
            Status.Text = (model.Session.IsDirty ? "* Unsaved script · " : "") +
                (model.Session.HasError ? model.Session.Status : model.SettingsStatus.Length > 0 ? model.SettingsStatus : model.Status);
            ToolTip.SetTip(Status, model.Session.DisplayName + "\n" + model.Session.Status + "\n" + model.Target + "\n" + model.Status +
                (model.SettingsStatus.Length > 0 ? "\n" + model.SettingsStatus : ""));
            Diagnostics.ItemsSource = model.Diagnostics;
            DiagnosticPanel.IsVisible = model.Diagnostics.Count > 0;
            DiagnosticPanel.Header = $"Diagnostics ({model.Diagnostics.Count})";
            Editor.FontSize = model.FontSize;
            ZoomReset.Content = $"{model.FontSizePoints} pt";
            ToolTip.SetTip(ZoomReset, "Reset to 12 pt (Ctrl+0 in code editor). Font size is saved across restarts.");
            ZoomOut.IsEnabled = model.FontSizePoints > ScriptEditorPreferences.MinimumPoints;
            ZoomIn.IsEnabled = model.FontSizePoints < ScriptEditorPreferences.MaximumPoints;
        }
        finally { synchronizing = false; }
    }
}
