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
	{
		return !_isExecuting && (_canExecute == null || _canExecute());
	}

	public async void Execute(object parameter)
	{
		_isExecuting = true;
		try
		{
			await _execute();
		}
		finally
		{
			_isExecuting = false;
		}
		CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}

	public event EventHandler CanExecuteChanged;
}
