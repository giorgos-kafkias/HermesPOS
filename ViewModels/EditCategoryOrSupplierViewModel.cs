using HermesPOS.Data.Repositories;
using HermesPOS.Models;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows;
using System.Threading.Tasks;
using System.Linq;

public class EditCategoryOrSupplierViewModel : INotifyPropertyChanged
{
	private readonly IUnitOfWork _unitOfWork;
	private /*Func<Task>*/Action _onSaveCompleted;
	private bool _isEditing;
	private Category _category;
	private Supplier _supplier;

	public bool IsSupplier { get; private set; } // ✅ Δηλώνουμε αν είναι Προμηθευτής

	public string Name { get; set; }
	public string Phone { get; set; }
	public string Address { get; set; }

	public ICommand SaveCommand { get; }
	public ICommand CancelCommand { get; }

	// ✅ Constructor με μόνο το IUnitOfWork για DI
	public EditCategoryOrSupplierViewModel(IUnitOfWork unitOfWork)
	{
		_unitOfWork = unitOfWork;
		SaveCommand = new RelayCommand(async () => await SaveData());
		CancelCommand = new RelayCommand(CloseWindow);
	}

	// ✅ Μέθοδος αρχικοποίησης που περνάει δεδομένα
	public void Initialize(Category category, Supplier supplier, Action onSaveCompleted)
	{
		_onSaveCompleted = onSaveCompleted;

		if (supplier != null)
		{
			_supplier = supplier;
			Name = supplier.Name;
			Phone = supplier.Phone;
			Address = supplier.Address;
			IsSupplier = true;
		}
		else
		{
			_category = category ?? new Category();
			Name = _category.Name;
			IsSupplier = false;
		}

		_isEditing = (_category != null || _supplier != null);
	}
	//public void Initialize(Category category, Supplier supplier, Func<Task> loadDataCallback)
	//{
	//	_category = category;
	//	_supplier = supplier;
	//	_onSaveCompleted = async () => await loadDataCallback();

	//	if (_supplier != null)
	//	{
	//		Name = _supplier.Name;
	//		Phone = _supplier.Phone;
	//		Address = _supplier.Address;
	//		IsSupplier = true;
	//	}
	//	else
	//	{
	//		Name = _category.Name;
	//		IsSupplier = false;
	//	}

	//	Task.Run(async () => await _onSaveCompleted()); // ✅ Καλούμε τη LoadData() κατά την αρχικοποίηση
	//}

	private async Task SaveData()
	{
		if (string.IsNullOrWhiteSpace(Name))
		{
			MessageBox.Show("Το όνομα δεν μπορεί να είναι κενό!", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		if (IsSupplier)
		{
			if (_supplier == null) _supplier = new Supplier();

			_supplier.Name = Name;
			_supplier.Phone = Phone;
			_supplier.Address = Address;

			if (!_isEditing)
				await _unitOfWork.Suppliers.AddAsync(_supplier);
			else
				await _unitOfWork.Suppliers.UpdateAsync(_supplier);
		}
		else
		{
			if (_category == null) _category = new Category();

			_category.Name = Name;

			if (!_isEditing)
				await _unitOfWork.Categories.AddAsync(_category);
			else
				await _unitOfWork.Categories.UpdateAsync(_category);
		}

		await _unitOfWork.CompleteAsync();
		_onSaveCompleted?.Invoke();
		CloseWindow();
	}

	private void CloseWindow()
	{
		Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)?.Close();
	}

	public event PropertyChangedEventHandler PropertyChanged;
	protected void OnPropertyChanged(string propertyName) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
