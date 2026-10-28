using System.ComponentModel;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ObservableCompletionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public int Subscribers => PropertyChanged?.GetInvocationList().Length ?? 0;
        public bool Final { get; set; }
        public void Notify() => PropertyChanged?.Invoke(this, new(null));
    }

    [Fact]
    public async Task CompletedOperationDoesNotCompleteWaitUntilFinalNotification()
    {
        var model = new Model(); var execution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = ObservableCompletion.RunAsync(model, () => execution.Task, () => model.Final, () => "synthetic pending", Token);
        model.Notify(); Assert.False(waiting.IsCompleted); execution.SetResult(); await execution.Task;
        Assert.False(waiting.IsCompleted); Assert.Equal(1, model.Subscribers);
        model.Final = true; model.Notify(); await waiting.WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.Equal(0, model.Subscribers);
    }

    [Fact]
    public async Task ActionFailureRemovesSubscriptionWithoutAwaitingAnotherNotification()
    {
        var model = new Model(); var error = new InvalidOperationException("Synthetic action failure");
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => ObservableCompletion.RunAsync(model,
            () => Task.FromException(error), () => false, () => "synthetic pending", Token)));
        Assert.Equal(0, model.Subscribers);
    }

    [Fact]
    public async Task ActionCancellationIsNotRelabeledAsAnObservationTimeout()
    {
        var model = new Model(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ObservableCompletion.RunAsync(model,
            () => Task.FromCanceled(cancellation.Token), () => false, () => "synthetic pending", Token));
        Assert.Equal(0, model.Subscribers);
    }

    [Fact]
    public async Task CallerCancellationDuringActionRemovesSubscriptionAndPreservesCancellation()
    {
        var model = new Model(); using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var execution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = ObservableCompletion.RunAsync(model, () => execution.Task, () => model.Final, () => "synthetic pending", cancellation.Token);
        Assert.Equal(1, model.Subscribers); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5), Token));
        Assert.Equal(0, model.Subscribers); execution.SetResult();
    }
}
