// ComTekAtomicClock.UI.Services.RelayCommand
//
// Minimal ICommand implementation for view-model bindings. Avoids
// pulling in the full Microsoft.Toolkit.Mvvm or CommunityToolkit.Mvvm
// NuGet just for one type. Standard pattern: wraps an Action<object?>
// (and optional Func<object?, bool> for CanExecute).

using System.Windows.Input;

namespace ComTekAtomicClock.UI.Services;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : (Func<object?, bool>)(_ => canExecute()))
    { }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>Force re-evaluation of CanExecute on bound buttons.</summary>
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
