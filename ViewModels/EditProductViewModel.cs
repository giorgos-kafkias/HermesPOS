using HermesPOS.Data.Repositories;
using HermesPOS.Helpers;
using HermesPOS.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace HermesPOS.ViewModels
{
	public class EditProductViewModel : INotifyPropertyChanged
	{
		private readonly IUnitOfWork _unitOfWork;
		private string _barcode;
		private string _name;
        private string _priceText;
        private int _stock;
		private Category _selectedCategory;
		private Supplier _selectedSupplier;
		private string _wholesalePriceText;

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

        public string PriceText
        {
            get => _priceText;
            set
            {
                _priceText = value;
                OnPropertyChanged(nameof(PriceText));
            }
        }

        public string WholesalePriceText
		{
			get => _wholesalePriceText;
			set
			{
				_wholesalePriceText = value;
				OnPropertyChanged(nameof(WholesalePriceText));
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
            PriceText = product.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            WholesalePriceText = product.WholesalePrice?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
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
            if (string.IsNullOrWhiteSpace(Barcode) ||
                string.IsNullOrWhiteSpace(Name) ||
                Stock < 0 ||
                SelectedCategory == null ||
                SelectedSupplier == null)
            {
                MessageBox.Show("Παρακαλώ συμπληρώστε όλα τα πεδία σωστά!", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!DecimalHelper.TryParseFlexibleDecimal(PriceText, out var parsedPrice) || parsedPrice <= 0)
            {
                MessageBox.Show("Λάθος τιμή!", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal? wholesalePrice = null;

            if (!string.IsNullOrWhiteSpace(WholesalePriceText) &&
                DecimalHelper.TryParseFlexibleDecimal(WholesalePriceText, out var parsedWholesale))
            {
                wholesalePrice = parsedWholesale;
            }

            // ✅ Ενημέρωση του προϊόντος
            Product.Barcode = Barcode;
			Product.Name = Name;
			Product.Price = parsedPrice;
            Product.WholesalePrice = wholesalePrice;
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
