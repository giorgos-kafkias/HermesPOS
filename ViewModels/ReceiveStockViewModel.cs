using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HermesPOS.Data.Repositories;
using HermesPOS.Models;
using HermesPOS.Views;

namespace HermesPOS.ViewModels
{
	public class ReceiveStockViewModel : INotifyPropertyChanged
	{
		private readonly IUnitOfWork _unitOfWork;
		private string _scannedBarcode;
		private readonly DispatcherTimer _barcodeTimer;

		public ObservableCollection<ReceivedItem> ReceivedItems { get; set; }

		public string ScannedBarcode
		{
			get => _scannedBarcode;
			set
			{
				_scannedBarcode = value;
				OnPropertyChanged(nameof(ScannedBarcode));

				// Κάθε φορά που αλλάζει το barcode, κάνε restart τον timer
				_barcodeTimer.Stop();

				if (!string.IsNullOrWhiteSpace(_scannedBarcode) && _scannedBarcode.Length >= 3)
				{
					_barcodeTimer.Start();
				}
			}
		}

		public ICommand CompleteReceptionCommand { get; }

		public ReceiveStockViewModel(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
			ReceivedItems = new ObservableCollection<ReceivedItem>();

			CompleteReceptionCommand = new AsyncRelayCommand(CompleteReception);

			// Αρχικοποίηση του timer για barcode scanning
			_barcodeTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(300)
			};
			_barcodeTimer.Tick += BarcodeTimer_Tick;
		}

		private async void BarcodeTimer_Tick(object sender, EventArgs e)
		{
			_barcodeTimer.Stop();
			await AddReceivedProduct();
		}

		private async Task AddReceivedProduct()
		{
			var product = await _unitOfWork.Products.GetByBarcodeAsync(ScannedBarcode?.Trim());

			if (product == null)
			{
				var result = MessageBox.Show("Το barcode δεν βρέθηκε! Θέλετε να προσθέσετε νέο προϊόν;", "Νέο Προϊόν", MessageBoxButton.YesNo, MessageBoxImage.Question);

				if (result == MessageBoxResult.Yes)
				{
					var viewModel = new AddProductViewModel(_unitOfWork, ScannedBarcode);
					var addProductView = new AddProductView(viewModel);
					addProductView.ShowDialog();
				}

				ScannedBarcode = string.Empty;
				return;
			}

			var existingItem = ReceivedItems.FirstOrDefault(p => p.Product.Barcode == ScannedBarcode);

			if (existingItem != null)
			{
				existingItem.Quantity++;
			}
			else
			{
				ReceivedItems.Add(new ReceivedItem(product, 1));
			}

			ScannedBarcode = string.Empty;
			OnPropertyChanged(nameof(ReceivedItems));
		}

		private async Task CompleteReception()
		{
			foreach (var item in ReceivedItems)
			{
				item.Product.Stock += item.Quantity;
				await _unitOfWork.Products.UpdateAsync(item.Product);
			}

			await _unitOfWork.CompleteAsync();
			ReceivedItems.Clear();
			OnPropertyChanged(nameof(ReceivedItems));
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public class ReceivedItem : INotifyPropertyChanged
	{
		public Product Product { get; set; }
		private int _quantity;

		public int Quantity
		{
			get => _quantity;
			set
			{
				if (_quantity != value)
				{
					_quantity = value;
					OnPropertyChanged(nameof(Quantity));
				}
			}
		}

		public ReceivedItem(Product product, int quantity)
		{
			Product = product;
			_quantity = quantity;
		}

		

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
