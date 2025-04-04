using System;
using System.Windows.Input;

/// Μια γενική εντολή που υποστηρίζει παραμέτρους τύπου <T>.
/// Χρησιμοποιείται για το binding των εντολών στα ViewModels.
/// Ο τύπος του παραμέτρου που περνάμε στην εντολή</typeparam>
public class RelayCommand<T> : ICommand
{
	private readonly Action<T> _execute; // Η μέθοδος που θα εκτελεστεί
	private readonly Func<T, bool> _canExecute; // Μέθοδος που ελέγχει αν η εντολή μπορεί να εκτελεστεί

	/// Δημιουργεί μια νέα εντολή `RelayCommand<T>`.
	/// Η μέθοδος που θα εκτελεστεί</param>
	/// Μέθοδος που καθορίζει αν η εντολή μπορεί να εκτελεστεί (προαιρετικό)</param>
	public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	/// Ελέγχει αν η εντολή μπορεί να εκτελεστεί.
	/// Η παράμετρος της εντολής</param>
	/// <returns>True αν η εντολή μπορεί να εκτελεστεί, αλλιώς False</returns>
	public bool CanExecute(object parameter)
	{
		return _canExecute == null || _canExecute((T)parameter);
	}

	/// Εκτελεί την εντολή.
	/// <param name="parameter">Η παράμετρος που περνάμε στην εντολή</param>
	public void Execute(object parameter)
	{
		_execute((T)parameter);
	}

	/// Γεγονός που καλείται όταν αλλάζει η κατάσταση εκτέλεσης της εντολής.
	public event EventHandler CanExecuteChanged;
}

/// Μια απλή εντολή `RelayCommand` χωρίς παραμέτρους.
/// Χρησιμοποιείται όταν δεν απαιτείται παράμετρος στην εκτέλεση της εντολής.
public class RelayCommand : ICommand
{
	private readonly Action _execute; // Η μέθοδος που θα εκτελεστεί
	private readonly Func<bool> _canExecute; // Μέθοδος που ελέγχει αν η εντολή μπορεί να εκτελεστεί

	/// Δημιουργεί μια νέα εντολή `RelayCommand`.
	/// Η μέθοδος που θα εκτελεστεί</param>
	/// Μέθοδος που καθορίζει αν η εντολή μπορεί να εκτελεστεί (προαιρετικό)</param>
	public RelayCommand(Action execute, Func<bool> canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	/// Ελέγχει αν η εντολή μπορεί να εκτελεστεί.
	///Το αντικείμενο που περνάμε στην εντολή (δεν χρησιμοποιείται εδώ)</param>
	/// <returns>True αν η εντολή μπορεί να εκτελεστεί, αλλιώς False</returns>
	public bool CanExecute(object parameter)
	{
		return _canExecute == null || _canExecute();
	}

	/// Εκτελεί την εντολή.
	/// Το αντικείμενο που περνάμε στην εντολή (δεν χρησιμοποιείται εδώ)</param>
	public void Execute(object parameter)
	{
		_execute();
	}

	/// Γεγονός που καλείται όταν αλλάζει η κατάσταση εκτέλεσης της εντολής.
	public event EventHandler CanExecuteChanged
	{
		add { CommandManager.RequerySuggested += value; }
		remove { CommandManager.RequerySuggested -= value; }
	}

	public void RaiseCanExecuteChanged()
	{
		CommandManager.InvalidateRequerySuggested();
	}
}
