using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Automation;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>A contiguous, bounded source window. Its session borrows the document's existing snapshot.</summary>
public sealed class FullRawInspectorView : Grid, IDisposable
{
    private readonly RawInspectorSession session;
    private CancellationTokenSource? request;
    private long generation;
    private bool disposed;
    private readonly FieldToolTipLifecycle toolTips;
    public TextEditor Editor { get; } = MessageViewer.CreateEditor("");
    public TextBlock Status { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public NumericUpDown Offset { get; } = new() { Minimum = 0, Width = 150, FormatString = "0", Value = 0 };
    public long Position { get; private set; }
    public Task Ready { get; private set; } = Task.CompletedTask;
    public FullRawInspectorView(RawInspectorSession session)
        : this(session, null) { }
    internal FullRawInspectorView(RawInspectorSession session, Func<Action, TimeSpan, IDisposable>? scheduleHover)
    {
        this.session = session;
        toolTips = new(Editor, local => FieldToolTipTarget.From(
            session.Inspection?.ResolveOffset(session.Scope.Offset + Position + local)), scheduleHover);
        AutomationProperties.SetName(this, "Full Source Viewer"); AutomationProperties.SetName(Editor, "Original source within selected scope");
        Position = session.WindowOffset;
        RowDefinitions = new("Auto,*,Auto");
        var title = new TextBlock { Text = $"Full Source Viewer · {session.Label} · source UTF-16 {session.Scope.Offset:N0}–{session.Scope.End:N0}", Margin = new(4), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        Children.Add(title); SetRow(Editor, 1); Children.Add(Editor);
        var controls = new WrapPanel { Orientation = Orientation.Horizontal };
        void Button(string title, string tip, Func<long> position, bool align = false)
        { var button = new Button { Content = title, Margin = new(2) }; ToolTip.SetTip(button, tip); button.Click += async (_, _) => await LoadAsync(position(), align); controls.Children.Add(button); }
        Button("Previous Chunk", "Previous chunk within the displayed source scope", () => Math.Max(0, Position - RawInspectorSession.ChunkCharacters), true);
        Button("Next Chunk", "Next chunk within the displayed source scope", () => Math.Min(session.Scope.Length, Position + Editor.Document.TextLength));
        Offset.Maximum = session.Scope.Length; controls.Children.Add(new TextBlock { Text = "Scope UTF-16 offset", VerticalAlignment = VerticalAlignment.Center }); controls.Children.Add(Offset);
        ToolTip.SetTip(Offset, "UTF-16 offset relative to the start of this source scope");
        Button("Go to Offset", "Jump to offset within the displayed source scope", () => Offset.Value is { } value && decimal.Truncate(value) == value ? checked((long)value) : -1);
        var export = new Button { Content = "Export Original…", IsEnabled = session.CanExport, Margin = new(2) };
        var cancel = new Button { Content = "Cancel export", IsVisible = false, Margin = new(2) };
        ToolTip.SetTip(export, "Export the displayed scope as original source bytes"); ToolTip.SetTip(cancel, "Cancel original-source export");
        CancellationTokenSource? exportRequest = null;
        cancel.Click += (_, _) => exportRequest?.Cancel();
        export.Click += async (_, _) =>
        {
            exportRequest = new(); export.IsEnabled = false; cancel.IsVisible = true;
            try { var exported = await ViewerRequests.ExportAsync(this, session, exportRequest.Token); if (!disposed) Status.Text = exported ? "Original encoded/source content exported; no attachment decoding." : "Export canceled or destination unavailable."; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException)
            { if (!disposed) Status.Text = "Export canceled or failed; no partial output was published."; }
            finally { exportRequest.Dispose(); exportRequest = null; if (!disposed) { cancel.IsVisible = false; export.IsEnabled = session.CanExport; } }
        };
        controls.Children.Add(export); controls.Children.Add(cancel); controls.Children.Add(Status); SetRow(controls, 2); Children.Add(controls);
        session.Closed += SessionClosed;
        Editor.TextArea.TextView.LineTransformers.Add(new SourceColorizer(session.Structure, () => session.Scope.Offset + Position, () => Hl7EditorTheme.PaletteFor(Editor)));
        Editor.TextArea.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.ClickCount != 2 || !e.GetCurrentPoint(Editor).Properties.IsLeftButtonPressed) return;
            if (Editor.GetPositionFromPoint(e.GetPosition(Editor)) is not { } position) return;
            var raw = session.Scope.Offset + Position + Editor.Document.GetOffset(position.Location);
            e.Handled = true; session.Inspection?.SelectDeepestOffset(raw);
        }, RoutingStrategies.Tunnel);
    }
    public async Task LoadAsync(long offset, bool align = false)
    {
        if (disposed) return;
        if (offset < 0 || offset > session.Scope.Length) { Status.Text = "Enter a whole UTF-16 offset inside this scope."; return; }
        var version = ++generation; request?.Cancel(); request?.Dispose(); request = new();
        try
        {
            var window = await session.ReadAsync(offset, request.Token, align);
            if (window is null || disposed || version != generation) return;
            Editor.Document = new TextDocument(window.Text); Editor.ScrollToHome();
            Position = window.ScopeOffset; Offset.Value = Position;
            session.WindowOffset = Position;
            Status.Text = $"Scope UTF-16 {Position:N0}–{Position + window.Text.Length:N0} / {session.Scope.Length:N0}; " +
                $"message UTF-16 {window.MessageOffset:N0}–{window.MessageOffset + window.Text.Length:N0} / {session.MessageLength:N0}; " +
                (window.MessageBytes is { } bytes ? $"original message bytes {bytes.Offset:N0}–{bytes.End:N0}." : "Original byte range unavailable.");
        }
        catch (ArgumentOutOfRangeException) { if (!disposed) Status.Text = "Offset falls inside a UTF-16 surrogate pair."; }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or IOException)
        { if (!disposed && version == generation) { Status.Text = "Source read canceled or unavailable."; if (e is IOException) session.ReportReadFailure(); } }
    }
    private void SessionClosed(object? sender, EventArgs e) => Dispose();
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); if (!disposed) Ready = LoadAsync(Position); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { generation++; request?.Cancel(); request?.Dispose(); request = null; CloseTip(); Editor.Document = new TextDocument(); base.OnDetachedFromVisualTree(e); }
    private void CloseTip() => toolTips.Close();
    internal void ReleaseView()
    {
        if (disposed) return; disposed = true; generation++; request?.Cancel(); request?.Dispose(); request = null;
        session.Closed -= SessionClosed; CloseTip(); Editor.Document = new TextDocument(); Editor.TextArea.TextView.LineTransformers.Clear();
        Children.Clear();
    }
    public void Dispose() { ReleaseView(); session.Dispose(); }
}
