using System.Collections.ObjectModel;
using Avalonia.Threading;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>A disposable projection of session history. Closing a tool never clears the service.</summary>
public sealed class ActivityLogViewModel : IDisposable
{
    private readonly ActivityLog log;
    private readonly ObservableCollection<ActivityEntry> entries = [];
    private int pending;
    private bool disposed;
    private Hl7DiagnosticSeverity? severity;
    public ReadOnlyObservableCollection<ActivityEntry> Entries { get; }
    // Opaque session sequence numbers only; no views or copied log text survive Dock retemplating.
    internal long[] SelectedSequences { get; set; } = [];
    public event EventHandler? Updating;
    public event EventHandler? Updated;
    public Hl7DiagnosticSeverity? Severity
    {
        get => severity;
        set { if (severity == value) return; severity = value; Refresh(); }
    }
    public ActivityLogViewModel(ActivityLog log)
    { this.log = log; Entries = new(entries); log.Changed += Changed; Refresh(); }
    private void Changed(object? sender, EventArgs e)
    {
        // Coalesce diagnostic bursts into one bounded UI update, even if a worker
        // reports an event. Neither the worker nor retained entries reference a view.
        if (Interlocked.Exchange(ref pending, 1) != 0) return;
        Dispatcher.UIThread.Post(() => { Interlocked.Exchange(ref pending, 0); if (!disposed) Refresh(); });
    }
    public void Refresh()
    {
        if (disposed) return;
        Updating?.Invoke(this, EventArgs.Empty);
        var next = log.Snapshot().Where(e => severity is null || e.Severity == severity).ToArray();
        var identities = next.Select(e => e.Sequence).ToHashSet();
        SelectedSequences = SelectedSequences.Where(identities.Contains).ToArray();
        for (var i = entries.Count - 1; i >= 0; i--) if (!identities.Contains(entries[i].Sequence)) entries.RemoveAt(i);
        for (var i = 0; i < next.Length; i++)
            if (i >= entries.Count || entries[i].Sequence != next[i].Sequence) entries.Insert(i, next[i]);
        Updated?.Invoke(this, EventArgs.Empty);
    }
    public void Clear() { SelectedSequences = []; log.Clear(); }
    public static string CopyText(IEnumerable<ActivityEntry> selected) => string.Join(Environment.NewLine, selected.OrderBy(e => e.Sequence));
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        log.Changed -= Changed; Updating = null; Updated = null; entries.Clear(); SelectedSequences = [];
    }
}
