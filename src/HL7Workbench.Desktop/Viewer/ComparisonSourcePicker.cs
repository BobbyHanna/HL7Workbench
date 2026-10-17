using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Compact source affordance. Document selection and temporary editing live in a secondary flyout.</summary>
public sealed class ComparisonSourcePicker : Grid
{
    private readonly ComparisonViewModel model;
    private readonly bool before;
    private readonly ComparisonInput input;
    private bool updating;
    private readonly TextBlock label = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly ComboBox documents = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly NumericUpDown ordinal = new() { Minimum = 1, Maximum = int.MaxValue, Value = 1, FormatString = "0", Width = 100 };
    private readonly StackPanel documentControls = new() { Spacing = 8 };
    private readonly StackPanel pasteControls = new() { Spacing = 8 };
    public Button SourceButton { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
    public Button PasteButton { get; } = new() { Content = "Paste / replace" };
    public ComboBox KindChoice { get; } = new() { ItemsSource = new[] { "Open Message", "Original Message", "Pasted Text (temporary)" }, HorizontalAlignment = HorizontalAlignment.Stretch };
    public TextEditor PasteEditor { get; } = MessageViewer.CreateEditor("");
    public Task ClipboardTask { get; private set; } = Task.CompletedTask;
    public ComparisonSourcePicker(ComparisonViewModel model, bool before)
    {
        this.model = model; this.before = before; input = before ? model.InputA : model.InputB;
        ColumnDefinitions = new("*"); var name = before ? "A" : "B";
        SourceButton.Content = label; Children.Add(SourceButton); ToolTip.SetTip(SourceButton, $"Message {name}: choose a source or edit Pasted Text. Ctrl+V pastes/replaces this side.");
        AutomationProperties.SetName(SourceButton, $"Message {name} source"); AutomationProperties.SetName(KindChoice, $"Message {name} source type");
        AutomationProperties.SetName(documents, $"Message {name} open document"); AutomationProperties.SetName(ordinal, $"Message {name} original ordinal");
        AutomationProperties.SetName(PasteEditor, $"Message {name} temporary pasted HL7");
        PasteEditor.IsReadOnly = false; PasteEditor.Height = 220; PasteEditor.Document.UndoStack.SizeLimit = 0;
        var clear = new Button { Content = "Clear pasted text" }; clear.Click += (_, _) => model.SetPastedText(before, "");
        PasteButton.Click += (_, _) => ClipboardTask = PasteAsync();
        SourceButton.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (!ComparisonClipboard.IsPaste(e)) return; e.Handled = true; ClipboardTask = PasteAsync();
        }, RoutingStrategies.Tunnel);
        ComparisonClipboard.Attach(PasteEditor, model, before, task => ClipboardTask = task);
        PasteEditor.TextChanged += (_, _) => { if (!updating) model.SetPastedText(before, PasteEditor.Text); };
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal, Children = { PasteButton, clear } };
        pasteControls.Children.Add(new TextBlock { Text = "Temporary text only. Paste with Ctrl+V or type below; comparison updates after a short pause.", TextWrapping = TextWrapping.Wrap });
        pasteControls.Children.Add(buttons); pasteControls.Children.Add(PasteEditor);
        pasteControls.Children.Add(new TextBlock { Text = "One message per side · 1 Mi characters · 16 Ki per line. Original line endings are retained.", FontSize = 11, TextWrapping = TextWrapping.Wrap });
        documents.ItemTemplate = new FuncDataTemplate<MessageDocument>((d, _) => new TextBlock { Text = d?.Title, TextTrimming = TextTrimming.CharacterEllipsis });
        var active = new Button { Content = "Use active message" }; active.Click += (_, _) =>
        { if (model.Documents.ActiveDocument is { MessageNumber: > 0 } d) input.SelectDocument(d, d.MessageNumber); };
        documentControls.Children.Add(documents); documentControls.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Original ordinal ", VerticalAlignment = VerticalAlignment.Center }, ordinal, active } });
        documentControls.Children.Add(new TextBlock { Text = "Document sources are read-only. Original Message always uses initially loaded content. Switching sources keeps your pasted draft.", TextWrapping = TextWrapping.Wrap });
        var panel = new StackPanel { Width = 400, MaxWidth = 400, Spacing = 10, Margin = new(12), Children =
        { new TextBlock { Text = $"Message {name} source", FontWeight = FontWeight.SemiBold }, KindChoice, documentControls, pasteControls } };
        var flyout = new Flyout { Content = new ScrollViewer { Content = panel, MaxHeight = 490, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } };
        SourceButton.Flyout = flyout;
        KindChoice.SelectionChanged += (_, _) => { if (!updating && KindChoice.SelectedIndex >= 0) input.Kind = (ComparisonSourceKind)KindChoice.SelectedIndex; };
        documents.SelectionChanged += (_, _) => SelectDocument(); ordinal.ValueChanged += (_, _) => SelectDocument();
        AttachedToVisualTree += (_, _) => { model.PropertyChanged += Changed; ((INotifyCollectionChanged)model.Documents.Documents).CollectionChanged += DocumentsChanged; Refresh(); };
        DetachedFromVisualTree += (_, _) => { flyout.Hide(); model.PropertyChanged -= Changed; ((INotifyCollectionChanged)model.Documents.Documents).CollectionChanged -= DocumentsChanged; };
        Refresh();
    }
    private void SelectDocument()
    { if (!updating && documents.SelectedItem is MessageDocument { MessageCount: > 0 } d) input.SelectDocument(d, Math.Clamp((int)(ordinal.Value ?? 1), 1, d.MessageCount)); }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void DocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        updating = true;
        try
        {
            label.Text = $"{(before ? "A" : "B")} · {input.Label} ▾"; KindChoice.SelectedIndex = (int)input.Kind;
            ToolTip.SetTip(SourceButton, input.Label + "\nChoose a source or edit temporary Pasted Text. Ctrl+V pastes/replaces this side.");
            documentControls.IsVisible = input.Kind != ComparisonSourceKind.PastedText; pasteControls.IsVisible = !documentControls.IsVisible;
            documents.ItemsSource = model.Documents.Documents; documents.SelectedItem = input.Document;
            ordinal.Maximum = input.Document?.MessageCount ?? int.MaxValue; ordinal.Value = input.Ordinal;
            if (PasteEditor.Text != input.PastedText) PasteEditor.Text = input.PastedText;
        }
        finally { updating = false; }
    }
    public async Task PasteAsync()
    {
        var revision = input.Revision;
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
            var text = await clipboard.TryGetTextAsync();
            if (input.Revision != revision) { model.ReportStatus("Source changed while reading the clipboard. Paste again."); return; }
            if (text is null) { model.ReportStatus("The clipboard contains no text. Copy one synthetic HL7 message and paste again."); return; }
            model.SetPastedText(before, text);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or NotSupportedException)
        { model.ReportStatus("Clipboard paste is unavailable. You can type temporary HL7 in the source control."); }
    }
}

/// <summary>Insert exact clipboard characters through TextDocument rather than the editor's newline-normalizing paste path.</summary>
internal static class ComparisonClipboard
{
    internal static bool IsPaste(KeyEventArgs e) => e.Key == Key.V && e.KeyModifiers == KeyModifiers.Control || e.Key == Key.Insert && e.KeyModifiers == KeyModifiers.Shift;
    internal static void Attach(TextEditor editor, ComparisonViewModel model, bool before, Action<Task> pending)
    {
        editor.TextArea.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        { if (!editor.IsReadOnly && IsPaste(e)) { e.Handled = true; pending(InsertAsync(editor, model, before)); } }, RoutingStrategies.Tunnel);
        var paste = new MenuItem { Header = "Paste exact clipboard text" };
        paste.Click += (_, _) => { if (!editor.IsReadOnly) pending(InsertAsync(editor, model, before)); };
        var menu = editor.ContextMenu ??= new(); var items = menu.ItemsSource?.Cast<MenuItem>().ToList() ?? []; items.Add(paste); menu.ItemsSource = items;
    }
    private static async Task InsertAsync(TextEditor editor, ComparisonViewModel model, bool before)
    {
        var input = before ? model.InputA : model.InputB; var revision = input.Revision;
        var start = editor.SelectionStart; var length = editor.SelectionLength; var original = editor.Text;
        try
        {
            if (TopLevel.GetTopLevel(editor)?.Clipboard is not { } clipboard) return;
            var text = await clipboard.TryGetTextAsync(); if (text is null || editor.IsReadOnly) return;
            if (input.Revision != revision || editor.Text != original) { model.ReportStatus("Source changed while reading the clipboard. Paste again."); return; }
            if ((long)original.Length - length + text.Length > ComparisonInput.MaximumPastedCharacters) throw new ComparisonInputException("Pasted Text is limited to 1 Mi UTF-16 characters. The previous draft was retained.");
            ComparisonInput.ValidatePastedText(original.Remove(start, length).Insert(start, text));
            editor.Document.Replace(start, length, text); editor.CaretOffset = start + text.Length;
        }
        catch (ComparisonInputException e) { model.ReportStatus(e.Message); }
        catch (Exception e) when (e is IOException or InvalidOperationException or NotSupportedException) { model.ReportStatus("Clipboard paste is unavailable. The previous draft was retained."); }
    }
}
