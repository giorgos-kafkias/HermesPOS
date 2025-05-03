using HermesPOS.Data.Repositories;
using HermesPOS.Models;
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
	public class SalesSummaryItem
	{
		public string Date { get; set; }
		public int TotalSales { get; set; }
		public decimal TotalAmount { get; set; }
	}

	public class SalesReportViewModel : INotifyPropertyChanged
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly IServiceProvider _serviceProvider;

		public ObservableCollection<SalesSummaryItem> SalesSummary { get; set; } = new();

		public DateTime? FromDate { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
		public DateTime? ToDate { get; set; } = DateTime.Today;

		public ICommand LoadSalesCommand { get; }

		public SalesReportViewModel(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
			_unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>(); // ✅ Διατήρηση UnitOfWork
			LoadSalesCommand = new RelayCommand(async () => await LoadSalesAsync());
		}

		private async Task LoadSalesAsync()
		{
			SalesSummary.Clear();

			var sales = await _unitOfWork.Sales.GetSalesByDateRangeAsync(
				FromDate ?? DateTime.MinValue,
				ToDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue
			);

			foreach (var sale in sales)
			{
				// ✅ Υπολογίζουμε το άθροισμα των τεμαχίων (από όλα τα SaleItems)
				int totalQuantity = sale.Items?.Sum(i => i.Quantity) ?? 0;

				// ✅ Υπολογίζουμε το άθροισμα ποσού πώλησης
				decimal totalAmount = sale.Items?.Sum(i => i.Quantity * i.Price) ?? 0;

				SalesSummary.Add(new SalesSummaryItem
				{
					Date = sale.SaleDate.ToString("dd/MM/yyyy HH:mm"),
					TotalSales = totalQuantity,
					TotalAmount = totalAmount
				});
			}

			OnPropertyChanged(nameof(SalesSummary));
		}

		public async Task OnTabSelected()
		{
			if (SalesSummary.Count == 0)
				await LoadSalesAsync();
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string propertyName) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
