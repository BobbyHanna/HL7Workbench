using System.ComponentModel;
using Avalonia;
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
    internal ScriptSemanticHighlighting SymbolHighlighting { get; }
    public Func<Task<UnsavedChangesDecision>> PromptUnsavedAsync { get; set; } = () => Task.FromResult(UnsavedChangesDecision.Cancel);
    public TextEditor Editor { get; } = new() { ShowLineNumbers = true, FontFamily = new("Consolas, monospace") };
    public ComboBox Library { get; } = new() { Width = 200 };
    public TextBox ScriptName { get; } = new() { Width = 180, MaxLength = 120, PlaceholderText = "Script name" };
    public ComboBox ExecutionScope { get; } = new() { ItemsSource = new[] { "Current message", "Whole document" }, Width = 140 };
    public Button New { get; } = new() { Content = "New" };
    public Button Save { get; } = new() { Content = "Save Script" };
    public Button Duplicate { get; } = new() { Content = "Duplicate" };
    public Button Run { get; } = new() { Content = "Run" };
    public Button Cancel { get; } = new() { Content = "Cancel" };
    public Button Review { get; } = new() { Content = "Review Output" };
    public TextBlock Status { get; } = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
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
        this.model = model; PromptUnsavedAsync = prompt ?? (() => model.PromptUnsavedAsync());
        Margin = new(3); RowDefinitions = new("Auto,Auto,*,Auto,Auto");
        MinWidth = 260; MinHeight = 150;
        ScriptEditorPalette.Dark.Apply(Editor);
        SymbolHighlighting = new(Editor, ScriptEditorPalette.Dark);
        LibraryToolbar = Toolbar(Library, New, Duplicate, ScriptName, Save);
        Children.Add(LibraryToolbar);
        ExecutionToolbar = Toolbar(ExecutionScope, Run, Cancel, Review, Target);
        SetRow(ExecutionToolbar, 1); Children.Add(ExecutionToolbar);
        SetRow(Editor, 2); Children.Add(Editor);
        var footer = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new(0, 1, 0, 0) };
        footer.Children.Add(Status);
        var zoomBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1 };
        foreach (var button in new[] { ZoomOut, ZoomReset, ZoomIn })
        { button.Padding = new(5, 0); button.MinHeight = 22; button.FontSize = 12; zoomBar.Children.Add(button); }
        SetColumn(zoomBar, 1); footer.Children.Add(zoomBar);
        SetRow(footer, 4); Children.Add(footer);
        Diagnostics.ItemTemplate = new FuncDataTemplate<HL7Workbench.Scripting.ScriptDiagnostic>((d, _) =>
            new TextBlock { Text = d?.ToString() ?? "", TextWrapping = TextWrapping.Wrap });
        DiagnosticPanel.Content = Diagnostics;
        ToolTip.SetTip(DiagnosticPanel, "Expand diagnostics, then double-click an entry to navigate to its script line.");
        SetRow(DiagnosticPanel, 3); Children.Add(DiagnosticPanel);
        Library.ItemTemplate = new FuncDataTemplate<ScriptLibraryItem>((item, _) => new TextBlock { Text = item?.Name ?? "" });
        ToolTip.SetTip(ScriptName, "Rename this script, then Save Script. Its library identity stays unchanged.");
        ToolTip.SetTip(Save, "Save script text and name locally (Ctrl+S). This does not save the HL7 document.");
        ToolTip.SetTip(Duplicate, "Save an editable copy, including the current draft, while retaining the original saved entry.");
        ToolTip.SetTip(Library, "Script Library — bundled examples can be duplicated and edited.");
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
        ScriptName.TextChanged += (_, _) => { if (!synchronizing) model.Session.Name = ScriptName.Text ?? ""; };
        ExecutionScope.SelectionChanged += (_, _) => { if (!synchronizing) model.Scope = ExecutionScope.SelectedIndex == 1 ? Scripting.ScriptScope.WholeDocument : Scripting.ScriptScope.CurrentMessage; };
        Library.SelectionChanged += async (_, _) =>
        {
            if (synchronizing || model.IsBusy || Library.SelectedItem is not ScriptLibraryItem item || item.Id == model.Session.Id) return;
            await model.Session.OpenAsync(item.Id, PromptUnsavedAsync); Refresh();
        };
        New.Click += async (_, _) => { await model.Session.NewAsync(PromptUnsavedAsync); Refresh(); };
        Duplicate.Click += (_, _) => { model.Session.Duplicate(); Refresh(); };
        Save.Click += (_, _) => { model.Session.Save(); Refresh(); };
        Run.Click += async (_, _) => await model.RunAsync(); Cancel.Click += (_, _) => model.Cancel(); Review.Click += (_, _) => model.Review();
        Diagnostics.DoubleTapped += (_, _) =>
        {
            if (Diagnostics.SelectedItem is Scripting.ScriptDiagnostic { Line: > 0 } diagnostic)
            { var line = Math.Min(diagnostic.Line.Value, Editor.Document.LineCount); Editor.ScrollToLine(line); Editor.TextArea.Caret.Line = line; Editor.Focus(); }
        };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            { e.Handled = true; if (!model.IsBusy && !model.Session.IsBundled) { model.Session.Save(); Refresh(); } }
        }, RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) => { if (!subscribed) { model.PropertyChanged += ModelChanged; subscribed = true; } Refresh(); SymbolHighlighting.Start(); };
        DetachedFromVisualTree += (_, _) => { if (subscribed) { model.PropertyChanged -= ModelChanged; subscribed = false; } SymbolHighlighting.Stop(); };
        SizeChanged += (_, _) => Target.IsVisible = Bounds.Width >= 650;
        Refresh();
    }
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
            ScriptName.Text = model.Session.Name; Editor.IsReadOnly = model.Session.IsBundled || model.IsBusy;
            ScriptName.IsReadOnly = model.Session.IsBundled || model.IsBusy;
            if (!ReferenceEquals(Library.ItemsSource, model.Session.Items)) Library.ItemsSource = model.Session.Items;
            Library.SelectedItem = model.Session.Items.FirstOrDefault(i => i.Id == model.Session.Id);
            Library.IsEnabled = New.IsEnabled = Duplicate.IsEnabled = ExecutionScope.IsEnabled = !model.IsBusy;
            Save.IsEnabled = !model.IsBusy && !model.Session.IsBundled;
            ExecutionScope.SelectedIndex = model.Scope == Scripting.ScriptScope.WholeDocument ? 1 : 0;
            Run.IsEnabled = model.CanRun; Cancel.IsEnabled = model.IsBusy; Review.IsEnabled = model.CanReview;
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
