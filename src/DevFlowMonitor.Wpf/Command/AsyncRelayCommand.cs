using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DevFlowMonitor.Wpf.Command;

public abstract class AsyncRelayCommandBase : ICommand, INotifyPropertyChanged
{
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;
    public event EventHandler<CommandExecutionFailedEventArgs>? ExecutionFailed;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting == value)
                return;

            _isExecuting = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged();
        }
    }

    public bool CanExecute(object? parameter) =>
        !IsExecuting && CanExecuteCore(parameter);

    public async void Execute(object? parameter)
    {
        try
        {
            await ExecuteAsync(parameter);
        }
        catch (Exception ex)
        {
            Trace.TraceError("Async command failed: {0}", ex);
            ExecutionFailed?.Invoke(this, new CommandExecutionFailedEventArgs(ex));
        }
    }

    public async Task ExecuteAsync(object? parameter = null)
    {
        if (!CanExecute(parameter))
            return;

        try
        {
            IsExecuting = true;
            await ExecuteCoreAsync(parameter);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    protected abstract bool CanExecuteCore(object? parameter);

    protected abstract Task ExecuteCoreAsync(object? parameter);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    : AsyncRelayCommandBase
{
    private readonly Func<Task> _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    protected override bool CanExecuteCore(object? parameter) =>
        canExecute?.Invoke() ?? true;

    protected override Task ExecuteCoreAsync(object? parameter) => _execute();
}

public sealed class AsyncRelayCommand<T>(Func<T, Task> execute, Func<T, bool>? canExecute = null)
    : AsyncRelayCommandBase
{
    private readonly Func<T, Task> _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    protected override bool CanExecuteCore(object? parameter) =>
        parameter is T value && (canExecute?.Invoke(value) ?? true);

    protected override Task ExecuteCoreAsync(object? parameter) =>
        _execute((T)parameter!);
}

public sealed class CommandExecutionFailedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
