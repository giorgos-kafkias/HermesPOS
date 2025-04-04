using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HermesPOS.Data.Repositories;
using HermesPOS.Models;
using Microsoft.Extensions.DependencyInjection;
using HermesPOS.Views;

namespace HermesPOS.ViewModels
{
	public class AdminPanelViewModel : INotifyPropertyChanged
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly IServiceProvider _serviceProvider;

		// 🔹 Λίστες Δεδομένων
		public ObservableCollection<Product> Products { get; set; } = new();
		public ObservableCollection<Category> Categories { get; set; } = new();
		public ObservableCollection<Supplier> Suppliers { get; set; } = new();
		public BestsellerViewModel BestsellerViewModel { get; }
		public SalesReportViewModel SalesReportViewModel { get; }
		public ReceiveStockViewModel ReceiveStockViewModel { get; }
		public LowStockProductsViewModel LowStockViewModel { get; }


		// 🔹 Επιλεγμένα Στοιχεία
		private Product _selectedProduct;
		public Product SelectedProduct
		{
			get => _selectedProduct;
			set
			{
				_selectedProduct = value;
				OnPropertyChanged(nameof(SelectedProduct));
				UpdateCommandStates();
			}
		}

		//αναζήτηση στα προιοντα
		private string _searchText;
		public string SearchText
		{
			get => _searchText;
			set
			{
				_searchText = value;
				OnPropertyChanged(nameof(SearchText));
				ApplyProductFilter(); // Ανανεώνει τα φιλτραρισμένα προϊόντα
			}
		}

		public ObservableCollection<Product> FilteredProducts { get; set; } = new();


		private Category _selectedCategory;
		public Category SelectedCategory
		{
			get => _selectedCategory;
			set
			{
				_selectedCategory = value;
				OnPropertyChanged(nameof(SelectedCategory));
				UpdateCommandStates();
			}
		}

		private Supplier _selectedSupplier;
		public Supplier SelectedSupplier
		{
			get => _selectedSupplier;
			set
			{
				_selectedSupplier = value;
				OnPropertyChanged(nameof(SelectedSupplier));
				UpdateCommandStates();
			}
		}

		// 🔹 Εντολές (Commands)
		public ICommand AddProductCommand { get; }
		public ICommand EditProductCommand { get; }
		public ICommand DeleteProductCommand { get; }

		public ICommand AddCategoryCommand { get; }
		public ICommand EditCategoryCommand { get; }
		public ICommand DeleteCategoryCommand { get; }

		public ICommand AddSupplierCommand { get; }
		public ICommand EditSupplierCommand { get; }
		public ICommand DeleteSupplierCommand { get; }
		

		public AdminPanelViewModel(IUnitOfWork unitOfWork, IServiceProvider serviceProvider)
		{
			_unitOfWork = unitOfWork;
			_serviceProvider = serviceProvider;

			LoadData();

			// 🔹 Δημιουργία των Commands
			AddProductCommand = new RelayCommand(AddProduct);
			EditProductCommand = new RelayCommand(EditProduct, () => SelectedProduct != null);
			DeleteProductCommand = new RelayCommand(DeleteProduct, () => SelectedProduct != null);

			AddCategoryCommand = new RelayCommand(AddCategory);
			EditCategoryCommand = new RelayCommand(EditCategory, () => SelectedCategory != null);
			DeleteCategoryCommand = new RelayCommand(DeleteCategory, () => SelectedCategory != null);

			AddSupplierCommand = new RelayCommand(AddSupplier);
			EditSupplierCommand = new RelayCommand(EditSupplier, () => SelectedSupplier != null);
			DeleteSupplierCommand = new RelayCommand(DeleteSupplier, () => SelectedSupplier != null);
			BestsellerViewModel = _serviceProvider.GetRequiredService<BestsellerViewModel>();
			SalesReportViewModel = _serviceProvider.GetRequiredService<SalesReportViewModel>();

			LowStockViewModel = serviceProvider.GetRequiredService<LowStockProductsViewModel>();
			ReceiveStockViewModel = serviceProvider.GetRequiredService<ReceiveStockViewModel>();
		}

		private async Task LoadData()
		{
			try
			{
				// ✅ Εκτέλεση των queries σειριακά
				var products = await _unitOfWork.Products.GetAllAsync(null, "Category,Supplier");
				var categories = await _unitOfWork.Categories.GetAllAsync();
				var suppliers = await _unitOfWork.Suppliers.GetAllAsync();

				App.Current.Dispatcher.Invoke(() =>
				{
					Products.Clear();
					Categories.Clear();
					Suppliers.Clear();

					foreach (var product in products) Products.Add(product);
					foreach (var category in categories) Categories.Add(category);
					foreach (var supplier in suppliers) Suppliers.Add(supplier);

					ApplyProductFilter(); // 👉 γέμισε και τη FilteredProducts με όλα τα προϊόντα
				});
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"❌ Σφάλμα: {ex.Message}");
			}
		}


		private void UpdateCommandStates()
		{
			((RelayCommand)EditProductCommand).RaiseCanExecuteChanged();
			((RelayCommand)DeleteProductCommand).RaiseCanExecuteChanged();
			((RelayCommand)EditCategoryCommand).RaiseCanExecuteChanged();
			((RelayCommand)DeleteCategoryCommand).RaiseCanExecuteChanged();
			((RelayCommand)EditSupplierCommand).RaiseCanExecuteChanged();
			((RelayCommand)DeleteSupplierCommand).RaiseCanExecuteChanged();
		}

		private void AddProduct()
		{
			var addProductViewModel = _serviceProvider.GetRequiredService<AddProductViewModel>();
			var addProductWindow = new AddProductView (addProductViewModel);
			addProductWindow.ShowDialog();
			LoadData();
		}

		private void EditProduct()
		{
			if (SelectedProduct == null) return;

			var editProductViewModel = _serviceProvider.GetRequiredService<EditProductViewModel>();
			editProductViewModel.Initialize(SelectedProduct);

			var editProductWindow = new EditProductWindow (editProductViewModel);
			editProductWindow.ShowDialog();
			LoadData();
		}

		private async void DeleteProduct()
		{
			if (SelectedProduct == null) return;

			if (MessageBox.Show($"Θέλετε να διαγράψετε το προϊόν '{SelectedProduct.Name}'?",
				"Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
			{
				await _unitOfWork.Products.DeleteAsync(SelectedProduct.Id);
				await _unitOfWork.CompleteAsync();
				await LoadData();
			}
		}

		private void AddCategory()
		{
			var viewModel = _serviceProvider.GetRequiredService<EditCategoryOrSupplierViewModel>();
			viewModel.Initialize(new Category(), null, async () => await LoadData());

			var window = new EditCategoryOrSupplierView (viewModel);
			window.ShowDialog();
		}

		private void EditCategory()
		{
			if (SelectedCategory == null) return;

			var viewModel = _serviceProvider.GetRequiredService<EditCategoryOrSupplierViewModel>();
			viewModel.Initialize(SelectedCategory, null, async () => await LoadData());

			var window = new EditCategoryOrSupplierView (viewModel);
			window.ShowDialog();
		}

		private async void DeleteCategory()
		{
			if (SelectedCategory == null) return;

			if (MessageBox.Show($"Θέλετε να διαγράψετε την κατηγορία '{SelectedCategory.Name}'?",
				"Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
			{
				await _unitOfWork.Categories.DeleteAsync(SelectedCategory.Id);
				await _unitOfWork.CompleteAsync();
				await LoadData();
			}
		}

		private void AddSupplier()
		{
			var viewModel = _serviceProvider.GetRequiredService<EditCategoryOrSupplierViewModel>();
			viewModel.Initialize(null, new Supplier(), async () => await LoadData());

			var window = new EditCategoryOrSupplierView (viewModel);
			window.ShowDialog();
		}

		private void EditSupplier()
		{
			if (SelectedSupplier == null) return;

			var viewModel = _serviceProvider.GetRequiredService<EditCategoryOrSupplierViewModel>();
			viewModel.Initialize(null, SelectedSupplier, async () => await LoadData());
			var window = new EditCategoryOrSupplierView (viewModel );
			window.ShowDialog();
		}

		private async void DeleteSupplier()
		{
			if (SelectedSupplier == null) return;

			if (MessageBox.Show($"Θέλετε να διαγράψετε τον προμηθευτή '{SelectedSupplier.Name}'?",
				"Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
			{
				await _unitOfWork.Suppliers.DeleteAsync(SelectedSupplier.Id);
				await _unitOfWork.CompleteAsync();
				await LoadData();
			}
		}

		private void ApplyProductFilter()
		{
			FilteredProducts.Clear();

			var filtered = string.IsNullOrWhiteSpace(SearchText)
				? Products
				: new ObservableCollection<Product>(
					Products.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
										(p.Category?.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
										(p.Supplier?.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)));

			foreach (var product in filtered)
			{
				FilteredProducts.Add(product);
			}
		}


		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
