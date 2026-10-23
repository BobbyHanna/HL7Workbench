using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

public sealed class RawInspectorRequestEventArgs(MessageDocument document, CharacterRange? range, string? path) : RoutedEventArgs(ViewerRequests.OpenInspectorEvent)
{
    public MessageDocument Document { get; } = document;
    public CharacterRange? Range { get; } = range;
    public string? Path { get; } = path;
}

public sealed class GenerationReportRequestEventArgs(MessageDocument document) : RoutedEventArgs(ViewerRequests.GenerationReportEvent)
{
    public MessageDocument Document { get; } = document;
}
public sealed class DeidentificationReportRequestEventArgs(MessageDocument document) : RoutedEventArgs(ViewerRequests.DeidentificationReportEvent)
{
    public MessageDocument Document { get; } = document;
}

/// <summary>View-to-shell intent, without coupling viewer controls to Dock.</summary>
public static class ViewerRequests
{
    public static readonly RoutedEvent<DeidentificationReportRequestEventArgs> DeidentificationReportEvent =
        RoutedEvent.Register<Control, DeidentificationReportRequestEventArgs>("DeidentificationReport", RoutingStrategies.Bubble);
    public static readonly RoutedEvent<GenerationReportRequestEventArgs> GenerationReportEvent =
        RoutedEvent.Register<Control, GenerationReportRequestEventArgs>("GenerationReport", RoutingStrategies.Bubble);
    public static readonly RoutedEvent<RawInspectorRequestEventArgs> FieldExtractionEvent =
        RoutedEvent.Register<Control, RawInspectorRequestEventArgs>("FieldExtraction", RoutingStrategies.Bubble);
    public static readonly RoutedEvent<RawInspectorRequestEventArgs> CompareOriginalEvent =
        RoutedEvent.Register<Control, RawInspectorRequestEventArgs>("CompareOriginal", RoutingStrategies.Bubble);
    public static void AttachComparisonMenu(Control control, MessageDocument document)
    {
        var compare = new MenuItem { Header = "Compare With Original" };
        compare.Click += (_, _) => control.RaiseEvent(new RawInspectorRequestEventArgs(document, null, null) { RoutedEvent = CompareOriginalEvent });
        var menu = control.ContextMenu ??= new();
        var items = menu.ItemsSource?.Cast<MenuItem>().ToList() ?? [];
        items.Add(compare); menu.ItemsSource = items;
    }
    public static readonly RoutedEvent<RawInspectorRequestEventArgs> OpenInspectorEvent =
        RoutedEvent.Register<Control, RawInspectorRequestEventArgs>("OpenInspector", RoutingStrategies.Bubble);
    public static void Open(Control control, MessageDocument document, CharacterRange? range = null, string? path = null) =>
        control.RaiseEvent(new RawInspectorRequestEventArgs(document, range, path));
    public static async Task<bool> ExportAsync(Control control, RawInspectorSession session, CancellationToken token)
    {
        var top = TopLevel.GetTopLevel(control);
        if (top?.StorageProvider.CanSave != true) return false;
        if (top is not Window owner) throw new IOException("Export requires a window owner.");
        var destination = await SaveFilePicker.ChooseAsync(owner, new FilePickerSaveOptions
        { Title = "Export original encoded/source content", SuggestedFileName = "original-source.hl7" }, token);
        if (destination is null) return false;
        await session.ExportAsync(destination, token);
        return true;
    }
    public static void AttachMenu(TextEditorTarget target)
    {
        var editor = target.Editor;
        var messageId = target.Document.ActiveMessageId;
        bool Current() => messageId == target.Document.ActiveMessageId && target.Document.HasOriginalMessage && !target.Document.IsLoading &&
            !target.Document.IsRefreshingEdit && !target.Document.HasWorkingSourceChanges;
        CharacterRange? value = null; string? path = null;
        var inspect = new MenuItem { Header = "View Full Value" };
        var export = new MenuItem { Header = "Export Original Value…" };
        var full = new MenuItem { Header = "View Full Message Source" };
        var preview = new MenuItem { Header = "Copy Visible Preview" };
        var cancelExport = new MenuItem { Header = "Cancel export", IsVisible = false };
        ToolTip.SetTip(inspect, "View the complete original structural value in Full Source Viewer");
        ToolTip.SetTip(full, "View complete original HL7 message");
        ToolTip.SetTip(export, "Export selected value as original source bytes");
        ToolTip.SetTip(preview, "Copy displayed preview text, including omission markers");
        ToolTip.SetTip(cancelExport, "Cancel original-source export");
        CancellationTokenSource? exportCancellation = null;
        var menu = editor.ContextMenu!;
        var entries = ((IEnumerable<MenuItem>)menu.ItemsSource!).ToList();
        entries.RemoveAll(item => Equals(item.Header, "Copy Visible Preview"));
        entries.Add(inspect); entries.Add(preview); entries.Add(export); entries.Add(cancelExport); entries.Add(full); menu.ItemsSource = entries;
        AttachComparisonMenu(editor, target.Document);
        editor.AddHandler(Control.ContextRequestedEvent, (_, e) =>
        {
            if (!Current()) { inspect.IsEnabled = export.IsEnabled = full.IsEnabled = false; return; }
            var local = editor.CaretOffset;
            if (e.TryGetPosition(editor, out var point) && editor.GetPositionFromPoint(point) is { } position) local = editor.Document.GetOffset(position.Location);
            var display = target.Origin + local;
            var f = target.Presentation?.Hit(display);
            var raw = target.Presentation is null ? display : target.Presentation.SourceOffset(display) ?? f?.Omitted?.Offset;
            full.IsEnabled = true;
            var node = raw is { } offset ? (target.Document.ActiveInspection ?? target.Inspection).ResolveOffset(offset) : null;
            value = f?.Omitted ?? node?.Range64;
            path = node?.DisplayPath ?? (f?.IsOpaque == true ? "Opaque source region" : null);
            inspect.IsEnabled = value is not null; export.IsEnabled = value is not null && target.Document.Snapshot is not null;
        }, RoutingStrategies.Tunnel);
        full.Click += (_, _) => { if (Current()) Open(editor, target.Document); };
        inspect.Click += (_, _) => { if (Current() && value is { } range) Open(editor, target.Document, range, path); };
        preview.Click += async (_, _) =>
        {
            var start = value is { } range && target.Presentation is { } p ? Math.Clamp(p.DisplayOffset(range.Offset) - target.Origin, 0, editor.Document.TextLength) : editor.SelectionStart;
            var end = value is { } r && target.Presentation is { } projection ? Math.Clamp(projection.DisplayOffset(r.End) - target.Origin, start, editor.Document.TextLength) : start + editor.SelectionLength;
            if (end - start > BoundedCopy.MaximumCharacters) return;
            if (TopLevel.GetTopLevel(editor)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(editor.Document.GetText((int)start, (int)(end - start)));
        };
        cancelExport.Click += (_, _) => exportCancellation?.Cancel();
        editor.DetachedFromVisualTree += (_, _) => exportCancellation?.Cancel();
        export.Click += async (_, _) =>
        {
            if (!Current() || value is not { } range || exportCancellation is not null) return;
            using var session = new RawInspectorSession(target.Document, range, path);
            exportCancellation = new(); export.IsEnabled = false; cancelExport.IsVisible = true;
            try { await ExportAsync(editor, session, exportCancellation.Token); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException)
            { ToolTip.SetTip(editor, "Export canceled or unavailable. Original source remains unchanged."); ToolTip.SetIsOpen(editor, true); }
            finally { exportCancellation.Dispose(); exportCancellation = null; export.IsEnabled = true; cancelExport.IsVisible = false; }
        };
    }
}
public sealed record TextEditorTarget(AvaloniaEdit.TextEditor Editor, MessageDocument Document, MessageInspection Inspection,
    MessagePresentation? Presentation = null, long Origin = 0);
