namespace HL7Workbench.Querying.Tests;

// A controlled one-shot timer source, not a replacement for query/operation
// clocks. Startup may take real time while this session clock remains frozen.
internal sealed class ManualDeadlineClock : TimeProvider
{
    private readonly object gate = new();
    private long ticks;
    private Timer? timer;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (gate) return ticks; }
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());
    public TimeSpan DueTime { get { lock (gate) return timer?.Due ?? throw new InvalidOperationException("No timer registered"); } }
    public bool TimerDisposed { get { lock (gate) return timer?.Disposed == true; } }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (gate)
        {
            if (timer is not null || period != Timeout.InfiniteTimeSpan) throw new InvalidOperationException("One one-shot timer expected");
            return timer = new(this, callback, state, dueTime);
        }
    }
    public void Expire()
    {
        Timer firing;
        lock (gate)
        {
            firing = timer ?? throw new InvalidOperationException("No timer registered");
            if (firing.Disposed || firing.Fired || firing.Due < TimeSpan.Zero) throw new InvalidOperationException("Timer unavailable");
            ticks += firing.Due.Ticks; firing.Fired = true;
        }
        firing.Callback(firing.State);
    }
    private sealed class Timer(ManualDeadlineClock owner, TimerCallback callback, object? state, TimeSpan due) : ITimer
    {
        public TimerCallback Callback { get; } = callback;
        public object? State { get; } = state;
        public TimeSpan Due { get; private set; } = due;
        public bool Disposed { get; private set; }
        public bool Fired { get; set; }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        { lock (owner.gate) { if (Disposed) return false; Due = dueTime; return true; } }
        public void Dispose() { lock (owner.gate) Disposed = true; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
