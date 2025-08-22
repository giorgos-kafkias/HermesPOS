using System;
using System.Threading.Tasks;
using System.Windows.Input;

public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool> _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object parameter)
        => !_isExecuting && (_canExecute == null || _canExecute());

    public async void Execute(object parameter)
    {
        _isExecuting = true;
        RaiseCanExecuteChanged();          // ενημέρωσε UI ότι τώρα τρέχει (disable κουμπί)
        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();      // ενημέρωσε UI ότι τελείωσε (enable κουμπί)
        }
    }

    public event EventHandler CanExecuteChanged;

    // 👇 πρόσθεσε αυτό
    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
