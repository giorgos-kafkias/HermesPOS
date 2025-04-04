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

		public ObservableCollection<Product> LowStockProducts { get; set; } // Λίστα προϊόντων με χαμηλό απόθεμα

		public ICommand ExportToExcelCommand { get; } // Εντολή για εξαγωγή σε Excel

		public LowStockProductsViewModel(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
			LowStockProducts = new ObservableCollection<Product>();
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
				LowStockProducts.Clear();

				var sortedProducts = products
					.OrderBy(p => p.Supplier?.Name ?? "Χωρίς Προμηθευτή")
					.ThenBy(p => p.Name)
					.ToList();

				foreach (var product in sortedProducts)
				{
					Console.WriteLine($"📌 Προϊόν: {product.Name}, Stock: {product.Stock}");
					LowStockProducts.Add(product);
				}
			});
		}

		private void ExportToExcel()
		{
			try
			{
				// Χρησιμοποιούμε fileName
				string fileName = "LowStockProducts.xlsx";

				ExcelExportHelper.ExportToExcel(LowStockProducts, fileName);
			}
			catch (Exception ex)
			{
				Console.WriteLine($" Σφάλμα κατά την εξαγωγή: {ex.Message}");
			}
		}
		public async Task OnTabSelected()
		{
			if (LowStockProducts.Count == 0)
				await LoadLowStockProducts();
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
