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
	//public class TotalAmountConverter : IValueConverter
	//{
	//	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	//	{
	//		if (value is ObservableCollection<SalesSummaryItem> list)
	//		{
	//			var total = list.Sum(x => x.TotalAmount);
	//			return $"Σύνολο: {total:0.00}€";
	//		}
	//		return "Σύνολο: 0.00€";
	//	}

	//	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
	//		throw new NotImplementedException();
	//}

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
				SalesSummary.Add(new SalesSummaryItem
				{
					Date = sale.SaleDate.ToString("dd/MM/yyyy HH:mm"),
					TotalSales = sale.Quantity,
					TotalAmount = sale.Quantity * sale.Price
				});
			}
			OnPropertyChanged(nameof(SalesSummary)); // ✅ Ενημερώνει το TextBlock κάτω
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
