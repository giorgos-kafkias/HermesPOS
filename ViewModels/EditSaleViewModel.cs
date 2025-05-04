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
			// 🔹 Φέρνουμε την πώληση από τη βάση για να πάρουμε τα προηγούμενα SaleItems
			var saleInDb = await _unitOfWork.Sales.GetByIdAsync(_originalSale.Id);

			if (saleInDb == null)
			{
				MessageBox.Show("Η πώληση δεν βρέθηκε.", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// 🔹 Ενημερώνουμε stock ανάλογα με τη διαφορά παλιής και νέας ποσότητας
			foreach (var oldItem in saleInDb.Items)
			{
				var newItem = SaleItems.FirstOrDefault(i => i.ProductId == oldItem.ProductId);

				if (newItem != null)
				{
					var difference = newItem.Quantity - oldItem.Quantity;

					// ➕ Αν αύξησες την ποσότητα, αφαιρούμε από το stock
					// ➖ Αν μείωσες την ποσότητα, προσθέτουμε στο stock
					oldItem.Product.Stock -= difference;
				}
			}

			// 🔹 Αντικαθιστούμε όλα τα SaleItems με τα νέα
			saleInDb.Items.Clear();
			foreach (var item in SaleItems)
			{
				saleInDb.Items.Add(item);
			}

			// 🔹 Υπολογίζουμε εκ νέου το σύνολο
			saleInDb.TotalAmount = SaleItems.Sum(i => i.Quantity * i.Price);

			await _unitOfWork.CompleteAsync();

			MessageBox.Show("Η πώληση αποθηκεύτηκε και ενημερώθηκε το απόθεμα.", "✅ Επιτυχία", MessageBoxButton.OK, MessageBoxImage.Information);
		}


		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string name) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
