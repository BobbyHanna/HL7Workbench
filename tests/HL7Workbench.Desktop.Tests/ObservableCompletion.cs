using System.ComponentModel;

namespace HL7Workbench.Desktop.Tests;

internal static class ObservableCompletion
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    // Subscribe on the UI thread before starting the action. Session.Execution
    // completing only schedules its UI observer; it does not drain that observer.
    // Keep the dispatcher available and wait for the actual visible final state.
    public static Task WaitAsync(INotifyPropertyChanged model, Func<bool> completed,
        Func<string> diagnostic, CancellationToken token) =>
        RunAsync(model, () => Task.CompletedTask, completed, diagnostic, token);

    // One deadline owns both the action and observation. If the action fails or
    // times out, finally removes the subscription without leaving a waiting task.
    // The fixture still owns cancellation and draining of its underlying run.
    public static async Task RunAsync(INotifyPropertyChanged model, Func<Task> action,
        Func<bool> completed, Func<string> diagnostic, CancellationToken token)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(Deadline);
        var started = false;
        void Changed(object? sender, PropertyChangedEventArgs args)
        {
            if (started && completed()) signal.TrySetResult();
        }
        model.PropertyChanged += Changed;
        try
        {
            var operation = action();
            started = true;
            Changed(null, new(null));
            await operation.WaitAsync(deadline.Token);
            Changed(null, new(null));
            await signal.Task.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !token.IsCancellationRequested)
        { throw new TimeoutException("Final UI completion notification was not observed: " + diagnostic()); }
        finally { model.PropertyChanged -= Changed; }
    }

    public static async Task TaskAsync(Task task, string phase, CancellationToken token)
    {
        try { await task.WaitAsync(Deadline, token); }
        catch (TimeoutException) { throw new TimeoutException("Lifecycle task did not complete: " + phase); }
    }

    // Listener receipt/client snapshots have no change event. Poll their actual
    // condition with the existing 5 s budget, rather than delaying assertions.
    public static async Task UntilAsync(Func<bool> completed, Func<string> diagnostic, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        using var poll = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
        try { while (!completed()) await poll.WaitForNextTickAsync(deadline.Token); }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !token.IsCancellationRequested)
        { throw new TimeoutException("Lifecycle condition was not observed: " + diagnostic()); }
    }

    public static async Task CloseAsync(Avalonia.Controls.Window window, CancellationToken token)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Closed(object? sender, EventArgs args) => closed.TrySetResult();
        window.Closed += Closed;
        try { window.Close(); await TaskAsync(closed.Task, "Window.Closed", token); }
        finally { window.Closed -= Closed; }
    }
}
