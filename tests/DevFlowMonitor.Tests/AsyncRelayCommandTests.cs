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
}
