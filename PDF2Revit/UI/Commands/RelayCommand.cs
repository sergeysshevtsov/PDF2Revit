using System.Windows.Input;

namespace PDF2Revit.UI.Commands;

public class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
{
    private readonly Action<object?> execute = execute;
    private readonly Predicate<object?>? canExecute = canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
        => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter)
        => execute(parameter);
}

