using DevFlowMonitor.Wpf.Command;

namespace DevFlowMonitor.Tests;

public class AsyncRelayCommandTests
{
    [Fact]
    public async Task ExecuteAsync_PropagatesExceptionToAwaitingCallerAndResetsState()
    {
        var expected = new InvalidOperationException("boom");
        var command = new AsyncRelayCommand(() => Task.FromException(expected));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => command.ExecuteAsync());

        Assert.Same(expected, actual);
        Assert.False(command.IsExecuting);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task ICommandExecute_ReportsExceptionWithoutThrowingOnDispatcher()
    {
        var expected = new InvalidOperationException("boom");
        var command = new AsyncRelayCommand(() => Task.FromException(expected));
        var completion = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        command.ExecutionFailed += (_, args) => completion.TrySetResult(args.Exception);

        command.Execute(null);

        var actual = await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Same(expected, actual);
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public async Task ExecuteAsync_NotifiesWhenExecutionStateChanges()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedStates = new List<bool>();
        var command = new AsyncRelayCommand(async () =>
        {
            started.SetResult();
            await release.Task;
        });
        command.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(command.IsExecuting))
                observedStates.Add(command.IsExecuting);
        };

        var execution = command.ExecuteAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(command.IsExecuting);
        release.SetResult();
        await execution;

        Assert.Equal([true, false], observedStates);
    }

    [Fact]
    public async Task GenericExecuteAsync_ExposesExecutionStateForBinding()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new AsyncRelayCommand<int>(_ => release.Task);

        var execution = command.ExecuteAsync(42);

        Assert.True(command.IsExecuting);
        release.SetResult();
        await execution;
        Assert.False(command.IsExecuting);
    }
}
