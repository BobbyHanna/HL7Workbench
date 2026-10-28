using System.ComponentModel;

namespace HL7Workbench.Desktop.Tests;

internal static class ObservableCompletion
{
    // Subscribe on the UI thread before starting the action. Session.Execution
    // completing only schedules its UI observer; it does not drain that observer.
    // Keep the dispatcher available and wait for the actual visible final state.
    public static async Task WaitAsync(INotifyPropertyChanged model, Func<bool> completed,
        Func<string> diagnostic, CancellationToken token)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(object? sender, PropertyChangedEventArgs args)
        {
            if (completed()) signal.TrySetResult();
        }
        model.PropertyChanged += Changed;
        try
        {
            Changed(null, new(null));
            try { await signal.Task.WaitAsync(TimeSpan.FromSeconds(10), token); }
            catch (TimeoutException)
            { throw new TimeoutException("Final UI completion notification was not observed: " + diagnostic()); }
        }
        finally { model.PropertyChanged -= Changed; }
    }
}
