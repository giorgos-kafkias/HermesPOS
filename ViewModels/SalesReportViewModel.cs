using HermesPOS.Data.Repositories;
using HermesPOS.Models;
using HermesPOS.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace HermesPOS.ViewModels
{
	public class SalesReportViewModel : INotifyPropertyChanged
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly IServiceProvider _serviceProvider;

		public ObservableCollection<Sale> Sales { get; set; } = new();

		private Sale _selectedSale;
		public Sale SelectedSale
		{
			get => _selectedSale;
			set
			{
				_selectedSale = value;
				OnPropertyChanged(nameof(SelectedSale));
				((RelayCommand)DeleteSaleCommand).RaiseCanExecuteChanged();
			}
		}


		public DateTime? FromDate { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
		public DateTime? ToDate { get; set; } = DateTime.Today;

		public ICommand LoadSalesCommand { get; }
		public ICommand DeleteSaleCommand { get; }
		public ICommand EditSaleCommand { get; }

		public SalesReportViewModel(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
			_unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>(); // ✅ Διατήρηση UnitOfWork
			LoadSalesCommand = new RelayCommand(async () => await LoadSalesAsync());
			DeleteSaleCommand = new RelayCommand(async () => await DeleteSelectedSale(), () => SelectedSale != null);
			EditSaleCommand = new RelayCommand<Sale>(EditSale);
		}

		private async Task LoadSalesAsync()
		{
			Sales.Clear();

			var sales = await _unitOfWork.Sales.GetSalesByDateRangeAsync(
				FromDate ?? DateTime.MinValue,
				ToDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue
			);

			foreach (var sale in sales)
			{
				Sales.Add(sale);

				int totalQuantity = sale.Items?.Sum(i => i.Quantity) ?? 0;
				decimal totalAmount = sale.Items?.Sum(i => i.Quantity * i.Price) ?? 0;
			}
			OnPropertyChanged(nameof(Sales));
		}


		private async Task DeleteSelectedSale()
		{
			if (SelectedSale == null) return;

			if (System.Windows.MessageBox.Show("Θέλεις σίγουρα να διαγράψεις αυτή την πώληση;", "Επιβεβαίωση", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes)
			{
				await _unitOfWork.Sales.DeleteAsync(SelectedSale.Id);
				await _unitOfWork.CompleteAsync();
				await LoadSalesAsync();
			}
		}
		private void EditSale(Sale sale)
		{
			if (sale == null) return;

			var viewModel = _serviceProvider.GetRequiredService<EditSaleViewModel>();
			var window = new EditSaleWindow(viewModel, sale);
			window.ShowDialog();

			// Μετά το κλείσιμο, ανανέωσε τις πωλήσεις
			_ = LoadSalesAsync();
		}

		public async Task OnTabSelected()
		{
			if (Sales.Count == 0)
				await LoadSalesAsync();
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string propertyName) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
