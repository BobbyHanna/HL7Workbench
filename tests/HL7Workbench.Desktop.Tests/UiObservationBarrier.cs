namespace HL7Workbench.Desktop.Tests;

// With these fixtures' synchronous consent replies, Start suspends only its run
// observer. Hold that captured continuation. Real Workspace
// execution and TCP cancellation continue normally. This makes the distinction
// between engine completion and UI observation testable without sleeps or hooks
// in production. Release always forwards to the original dispatcher context.
internal sealed class UiObservationBarrier : SynchronizationContext, IDisposable
{
    private readonly SynchronizationContext dispatcher = Current ?? throw new InvalidOperationException("UI context required");
    private readonly object gate = new();
    private readonly Queue<(SendOrPostCallback Callback, object? State)> pending = new();
    private readonly TaskCompletionSource queued = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool released;
    public Task Queued => queued.Task;

    public Task Start(Func<Task> action)
    {
        var previous = Current;
        SetSynchronizationContext(this);
        try { return action(); }
        finally { SetSynchronizationContext(previous); }
    }

    public override void Post(SendOrPostCallback callback, object? state)
    {
        lock (gate)
        {
            if (!released) { pending.Enqueue((callback, state)); queued.TrySetResult(); return; }
        }
        dispatcher.Post(callback, state);
    }

    public void Dispose()
    {
        (SendOrPostCallback Callback, object? State)[] work;
        lock (gate) { released = true; work = pending.ToArray(); pending.Clear(); }
        foreach (var item in work) dispatcher.Post(item.Callback, item.State);
    }
}
