using HermesPOS.Models;
using HermesPOS.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace HermesPOS.ViewModels
{
	public class EditSaleViewModel : INotifyPropertyChanged
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly IServiceProvider _serviceProvider;
		private Sale _originalSale;

		public ObservableCollection<SaleItem> SaleItems { get; set; } = new();

		public ICommand SaveCommand { get; }

		public EditSaleViewModel(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
			_unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
			SaveCommand = new RelayCommand(async () => await SaveChangesAsync());
		}

		public void Initialize(Sale sale)
		{
			_originalSale = sale;
			SaleItems.Clear();

			foreach (var item in sale.Items)
			{
				SaleItems.Add(new SaleItem
				{
					Id = item.Id,
					ProductId = item.ProductId,
					Product = item.Product,
					Quantity = item.Quantity,
					Price = item.Price,
					SaleId = sale.Id
				});
			}
		}

		private async Task SaveChangesAsync()
		{
			_originalSale.Items.Clear();

			foreach (var item in SaleItems)
			{
				_originalSale.Items.Add(item);
			}

			_originalSale.TotalAmount = SaleItems.Sum(i => i.Quantity * i.Price);

			await _unitOfWork.Sales.UpdateAsync(_originalSale);
			await _unitOfWork.CompleteAsync();

			MessageBox.Show("Η πώληση αποθηκεύτηκε με επιτυχία.", "Επιτυχία", MessageBoxButton.OK, MessageBoxImage.Information);

			// Κλείνουμε το παράθυρο από View (θα το δεις σε επόμενο βήμα)
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string name) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
