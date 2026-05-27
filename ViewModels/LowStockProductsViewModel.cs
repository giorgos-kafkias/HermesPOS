using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using HermesPOS.Models;
using HermesPOS.Data.Repositories;
using System.Diagnostics;

namespace HermesPOS.ViewModels
{
	public class LowStockProductsViewModel : INotifyPropertyChanged
	{
		private readonly IUnitOfWork _unitOfWork;

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                ApplyProductFilter();
            }
        }

        public ObservableCollection<Product> FilteredProducts { get; set; } = new();

        private List<Product> _lowStockProducts = new();
        public ICommand ExportToExcelCommand { get; } // Εντολή για εξαγωγή σε Excel

		public LowStockProductsViewModel(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
			ExportToExcelCommand = new RelayCommand(ExportToExcel);
			_ = LoadLowStockProducts(); // ✅ Fire and forget, χωρίς warning
		}


		private async Task LoadLowStockProducts()
		{
			var products = await _unitOfWork.Products.GetAllAsync(p => p.Stock <= 5, "Supplier,Category");

			if (products == null || !products.Any())
			{
				Console.WriteLine(" Δεν βρέθηκαν προϊόντα με χαμηλό απόθεμα!");
				return;
			}

			//  Εκτελούμε αλλαγές στο UI Thread
			System.Windows.Application.Current.Dispatcher.Invoke(() =>
			{
				var sortedProducts = products
					.OrderBy(p => p.Supplier?.Name ?? "Χωρίς Προμηθευτή")
					.ThenBy(p => p.Name)
					.ToList();

                _lowStockProducts = sortedProducts;

                ApplyProductFilter();
            });
		}

		private void ExportToExcel()
		{
			try
			{
				// Χρησιμοποιούμε fileName
				string fileName = "LowStockProducts.xlsx";

                ExcelExportHelper.ExportToExcel(FilteredProducts, fileName);
            }
			catch (Exception ex)
			{
				Console.WriteLine($" Σφάλμα κατά την εξαγωγή: {ex.Message}");
			}
		}
		public async Task OnTabSelected()
		{
            if (_lowStockProducts.Count == 0)
                await LoadLowStockProducts();
		}
        private void ApplyProductFilter()
        {
            FilteredProducts.Clear();

            var term = (SearchText ?? "").Trim();

            var filtered = string.IsNullOrWhiteSpace(term)
				? _lowStockProducts
               : _lowStockProducts.Where(p =>
                    (!string.IsNullOrWhiteSpace(p.Name) &&
                     p.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                    ||
                    ((p.Barcode?.ToString() ?? "")
                        .Contains(term, StringComparison.OrdinalIgnoreCase))
                    ||
                    (p.Supplier?.Name?
                        .Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    ||
                    (p.Category?.Name?
                        .Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                );

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
