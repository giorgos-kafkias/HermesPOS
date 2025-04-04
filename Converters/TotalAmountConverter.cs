using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Collections.ObjectModel;
using HermesPOS.ViewModels; // Για να βρει το SalesSummaryItem

namespace HermesPOS.Converters
{
	public class TotalAmountConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is ObservableCollection<SalesSummaryItem> list)
			{
				var total = list.Sum(x => x.TotalAmount);
				return $"Σύνολο: {total:0.00}€";
			}
			return "Σύνολο: 0.00€";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}
