using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HermesPOS.Data.Repositories;
using HermesPOS.Models;

namespace HermesPOS.ViewModels
{
	public class EditProductViewModel : INotifyPropertyChanged
	{
		private readonly IUnitOfWork _unitOfWork;
		private string _barcode;
		private string _name;
		private decimal _price;
		private int _stock;
		private Category _selectedCategory;
		private Supplier _selectedSupplier;

		public Product Product { get; private set; }

		public string Barcode
		{
			get => _barcode;
			set
			{
				_barcode = value;
				OnPropertyChanged(nameof(Barcode));
			}
		}

		public string Name
		{
			get => _name;
			set
			{
				_name = value;
				OnPropertyChanged(nameof(Name));
			}
		}

		public decimal Price
		{
			get => _price;
			set
			{
				_price = value;
				OnPropertyChanged(nameof(Price));
			}
		}

		public int Stock
		{
			get => _stock;
			set
			{
				_stock = value;
				OnPropertyChanged(nameof(Stock));
			}
		}

		public ObservableCollection<Category> Categories { get; set; } = new();
		public ObservableCollection<Supplier> Suppliers { get; set; } = new();

		public Category SelectedCategory
		{
			get => _selectedCategory;
			set
			{
				_selectedCategory = value;
				OnPropertyChanged(nameof(SelectedCategory));
			}
		}

		public Supplier SelectedSupplier
		{
			get => _selectedSupplier;
			set
			{
				_selectedSupplier = value;
				OnPropertyChanged(nameof(SelectedSupplier));
			}
		}

		public ICommand SaveCommand { get; }
		public ICommand CancelCommand { get; }

		// ✅ Constructor ΔΕΝ δέχεται Product
		public EditProductViewModel(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;

			SaveCommand = new AsyncRelayCommand(SaveProduct);
			CancelCommand = new RelayCommand(CloseWindow);
		}

		// ✅ Μέθοδος αρχικοποίησης
		public void Initialize(Product product)
		{
			Product = product;

			// ✅ Γεμίζουμε τις τιμές
			Barcode = product.Barcode;
			Name = product.Name;
			Price = product.Price;
			Stock = product.Stock;
			SelectedCategory = product.Category;
			SelectedSupplier = product.Supplier;

			// ✅ Φόρτωση Κατηγοριών & Προμηθευτών
			LoadCategoriesAndSuppliers();
		}

		private async void LoadCategoriesAndSuppliers()
		{
			var categories = await _unitOfWork.Categories.GetAllAsync();
			var suppliers = await _unitOfWork.Suppliers.GetAllAsync();

			Categories.Clear();
			Suppliers.Clear();

			foreach (var category in categories)
				Categories.Add(category);

			foreach (var supplier in suppliers)
				Suppliers.Add(supplier);
		}

		private async Task SaveProduct()
		{
			if (string.IsNullOrWhiteSpace(Barcode) || string.IsNullOrWhiteSpace(Name) || Price <= 0 || Stock < 0 || SelectedCategory == null || SelectedSupplier == null)
			{
				MessageBox.Show("Παρακαλώ συμπληρώστε όλα τα πεδία σωστά!", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			// ✅ Ενημέρωση του προϊόντος
			Product.Barcode = Barcode;
			Product.Name = Name;
			Product.Price = Price;
			Product.Stock = Stock;
			Product.CategoryId = SelectedCategory.Id;
			Product.SupplierId = SelectedSupplier.Id;

			await _unitOfWork.Products.UpdateAsync(Product);
			await _unitOfWork.CompleteAsync();

			MessageBox.Show("Το προϊόν ενημερώθηκε επιτυχώς!", "Επιτυχία", MessageBoxButton.OK, MessageBoxImage.Information);
			CloseWindow();
		}

		private void CloseWindow()
		{
			Application.Current.Windows[1]?.Close();
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
