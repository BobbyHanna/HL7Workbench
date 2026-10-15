using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>A lightweight log alert count; it never constructs the optional log view.</summary>
public sealed class ActivityStatusIndicator : Button
{
    private readonly ActivityLog log;
    private int pending;
    private bool attached;
    public ActivityStatusIndicator(ActivityLog log, Action openLog)
    {
        this.log = log; Click += (_, _) => openLog();
        ToolTip.SetTip(this, "Open Activity Log for retained session warnings and errors"); Refresh();
    }
    public void Refresh()
    {
        var entries = log.Snapshot();
        var warnings = entries.Count(e => e.Severity == Hl7DiagnosticSeverity.Warning);
        var errors = entries.Count(e => e.Severity == Hl7DiagnosticSeverity.Error);
        Content = $"Activity Log: {warnings} warning(s), {errors} error(s)";
        IsVisible = warnings + errors > 0;
    }
    private void Changed(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref pending, 1) != 0) return;
        Dispatcher.UIThread.Post(() => { Interlocked.Exchange(ref pending, 0); if (attached) Refresh(); });
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); attached = true; log.Changed += Changed; Refresh(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { attached = false; log.Changed -= Changed; base.OnDetachedFromVisualTree(e); }
}
