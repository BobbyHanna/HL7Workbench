namespace HL7Workbench.Desktop.Tests;

/// <summary>A deterministic UI-thread clock, including callbacks already queued when canceled.</summary>
internal sealed class ManualHoverScheduler
{
    private sealed class Entry(Action action, TimeSpan due) : IDisposable
    {
        public Action Action { get; } = action;
        public TimeSpan Due { get; } = due;
        public bool Canceled { get; private set; }
        public void Dispose() => Canceled = true;
    }
    private readonly List<Entry> entries = [];
    private TimeSpan now;
    public int ScheduledCount { get; private set; }
    public List<Action> Callbacks { get; } = [];
    public IDisposable Schedule(Action action, TimeSpan delay)
    {
        ScheduledCount++; Callbacks.Add(action);
        var entry = new Entry(action, now + delay); entries.Add(entry); return entry;
    }
    public void AdvanceBy(TimeSpan elapsed)
    {
        var end = now + elapsed;
        while (entries.Where(e => e.Due <= end).OrderBy(e => e.Due).FirstOrDefault() is { } next)
        {
            entries.Remove(next); now = next.Due;
            if (!next.Canceled) next.Action();
        }
        now = end;
    }
}
