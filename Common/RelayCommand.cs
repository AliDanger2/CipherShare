using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CipherShare.Common;

/// <summary>
/// A simple ICommand implementation for buttons/menu items bound in XAML via {Binding SomeCommand}.
/// This is the same idea as onClick handlers in the original React app, just wrapped so
/// XAML can bind directly to it.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    private readonly Predicate<object> _canExecute;

    public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool> canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute())
    {
    }

    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object parameter) => _execute(parameter);

    /// <summary>Forces bound controls to re-check CanExecute (e.g. after state changes outside the UI thread).</summary>
    public static void RaiseCanExecuteChangedForAll()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

/// <summary>
/// Same as RelayCommand but for async handlers (e.g. sending files over the network).
/// Guards against re-entrancy so double-clicking "Send" doesn't start two transfers.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object, Task> _execute;
    private readonly Predicate<object> _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<object, Task> execute, Predicate<object> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute())
    {
    }

    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object parameter)
    {
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
