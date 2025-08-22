using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HermesPOS.Data.Repositories;
using HermesPOS.Models;
using HermesPOS.Views;
using Microsoft.Extensions.DependencyInjection;
using static HermesPOS.ViewModels.MainViewModel;

namespace HermesPOS.ViewModels
{
	public class MainViewModel : INotifyPropertyChanged
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly IServiceProvider _serviceProvider;
		private string _scannedBarcode;
		private decimal _totalPrice;
		private readonly DispatcherTimer _barcodeTimer;

		public ObservableCollection<CartItem> CartItems { get; set; }

        public string ScannedBarcode
		{
			get => _scannedBarcode;
			set
			{
				_scannedBarcode = value;
				OnPropertyChanged(nameof(ScannedBarcode));

                // 🔔 ενημέρωσε το κουμπί ότι άλλαξαν οι προϋποθέσεις
                ((AsyncRelayCommand)AddProductCommand).RaiseCanExecuteChanged();
            }
		}

		public class CartItem : INotifyPropertyChanged
		{
			public Product Product { get; set; }
			private int _quantity;
			private decimal _price;
			private readonly Action _onQuantityOrPriceChanged;

			public bool HasWholesaleOption => Product.WholesalePrice.HasValue;

			private bool _useWholesalePrice;
			public bool UseWholesalePrice
			{
				get => _useWholesalePrice;
				set
				{
					_useWholesalePrice = value;

					// ➕ Αν έχει χονδρική τιμή, την εφαρμόζουμε
					if (_useWholesalePrice && Product.WholesalePrice.HasValue)
						Price = Product.WholesalePrice.Value;
					else
						Price = Product.Price;

					// ✅ Ενημερώνουμε και το TextBox
					PriceString = Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

					OnPropertyChanged(nameof(UseWholesalePrice));
				}
			}


			// Εμφάνιση δίπλα στο checkbox
			public string WholesalePriceDisplay =>
				Product.WholesalePrice.HasValue ? $"({Product.WholesalePrice.Value:0.00} €)" : string.Empty;

			public int Quantity
			{
				get => _quantity;
				set
				{
					if (_quantity != value)
					{
						if (value > Product.Stock)
						{
							System.Windows.MessageBox.Show(
								$"Το απόθεμα για το προϊόν \"{Product.Name}\" είναι {Product.Stock}.",
								"Μη διαθέσιμη ποσότητα",
								System.Windows.MessageBoxButton.OK,
								System.Windows.MessageBoxImage.Warning
							);
							return;
						}

						_quantity = value;
						OnPropertyChanged(nameof(Quantity));
						OnPropertyChanged(nameof(TotalPrice));
						_onQuantityOrPriceChanged?.Invoke();
					}
				}
			}
			private string _priceString;
			public string PriceString
			{
				get => _priceString;
				set
				{
					if (_priceString != value)
					{
						_priceString = value;

						// Επιτρέπουμε και τελεία και κόμμα
						var clean = value?.Replace(',', '.') ?? "";

						if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
						{
							Price = parsed;
						}

						OnPropertyChanged(nameof(PriceString));
					}
				}
			}

			// ✅ Editable τιμή από το textbox
			public decimal Price
			{
				get => _price;
				set
				{
					if (_price != value)
					{
						_price = value;
						OnPropertyChanged(nameof(Price));
						OnPropertyChanged(nameof(TotalPrice));
						_onQuantityOrPriceChanged?.Invoke();
					}
				}
			}

			public decimal TotalPrice => Quantity * Price;

			public CartItem(Product product, Action onQuantityOrPriceChanged)
			{
				Product = product;
				_quantity = 1;
				_price = product.Price; // Ξεκινάμε με λιανική
				_priceString = _price.ToString("0.00"); // για να γεμίζει το textbox
				_onQuantityOrPriceChanged = onQuantityOrPriceChanged;
			}

			public event PropertyChangedEventHandler PropertyChanged;
			protected void OnPropertyChanged(string propertyName) =>
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}


		public decimal TotalPrice
		{
			get => _totalPrice;
			set
			{
				_totalPrice = value;
				OnPropertyChanged(nameof(TotalPrice));
			}
		}

		public ICommand AddProductCommand { get; }
		public ICommand CompleteTransactionCommand { get; }
		public ICommand OpenLowStockViewCommand { get; }
		public ICommand RemoveProductCommand { get; }
		public ICommand EmptyCartCommand { get; }
		public ICommand OpenReceiveStockViewCommand { get; }

		public MainViewModel(IUnitOfWork unitOfWork, IServiceProvider serviceProvider)
		{
			_unitOfWork = unitOfWork;
			_serviceProvider = serviceProvider;
			CartItems = new ObservableCollection<CartItem>();

			UpdateTotalPrice();

			EmptyCartCommand = new RelayCommand(EmptyCart, CanEmptyCart);
			RemoveProductCommand = new RelayCommand<CartItem>(RemoveProduct);
			AddProductCommand = new AsyncRelayCommand(AddProductByBarcode, CanManuallyAdd);
			CompleteTransactionCommand = new AsyncRelayCommand(CompleteTransaction);
		}

		public async Task AddProductByBarcode()
		{
			if (string.IsNullOrWhiteSpace(ScannedBarcode))
				return;

			var product = await _unitOfWork.Products.GetByBarcodeAsync(ScannedBarcode.Trim());
			if (product != null)
			{
				// 🛑 Έλεγχος αν το απόθεμα είναι μηδέν
				if (product.Stock <= 0)
				{
					MessageBox.Show($"Το προϊόν \"{product.Name}\" δεν έχει διαθέσιμο απόθεμα.", "Μη διαθέσιμο", MessageBoxButton.OK, MessageBoxImage.Warning);
					ScannedBarcode = string.Empty;
					return;
				}

                // 🚫 Μπλοκάρισμα αν είναι ανενεργό
                if (!product.IsActive)
                {
                    MessageBox.Show(
                        $"Το προϊόν \"{product.Name}\" είναι ανενεργό και δεν μπορεί να πουληθεί.",
                        "Ανενεργό προϊόν", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ScannedBarcode = string.Empty;
                    return;
                }

                var existingCartItem = CartItems.FirstOrDefault(c => c.Product.Barcode == ScannedBarcode);

				if (existingCartItem != null)
				{
					// ✅ Έλεγχος να μη ξεπερνάμε το διαθέσιμο απόθεμα
					if (existingCartItem.Quantity < product.Stock)
					{
						existingCartItem.Quantity++;
					}
					else
					{
						MessageBox.Show($"Δεν μπορείς να προσθέσεις περισσότερα. Απόθεμα: {product.Stock}.", "Περιορισμός", MessageBoxButton.OK, MessageBoxImage.Information);
					}
				}
				else
				{
					CartItems.Add(new CartItem(product, UpdateTotalPrice));
				}

				UpdateTotalPrice();
				ScannedBarcode = string.Empty;
				OnPropertyChanged(nameof(CartItems));
			}
			else
			{
				// 📢 Αν δεν βρεθεί προϊόν με αυτό το barcode
				MessageBox.Show($"Το προϊόν με barcode \"{ScannedBarcode}\" δεν βρέθηκε.", "Μη διαθέσιμο προϊόν", MessageBoxButton.OK, MessageBoxImage.Error);
				ScannedBarcode = string.Empty;
			}
		}


		private void UpdateTotalPrice()
		{
			TotalPrice = CartItems.Sum(c => c.TotalPrice);
			OnPropertyChanged(nameof(TotalPrice));
		}

		private bool CanManuallyAdd() =>
			!string.IsNullOrWhiteSpace(ScannedBarcode) && ScannedBarcode.Length <= 20;

		private void RemoveProduct(CartItem cartItem)
		{
			if (cartItem != null)
			{
				CartItems.Remove(cartItem);
				UpdateTotalPrice();
			}
		}

		private bool CanEmptyCart() => CartItems.Count > 0;

		private void EmptyCart()
		{
			CartItems.Clear();
			UpdateTotalPrice();
			((RelayCommand)EmptyCartCommand).RaiseCanExecuteChanged();
		}

		private async Task CompleteTransaction()
		{
			if (!CartItems.Any()) return;

			// 🔹 Δημιουργούμε νέο αντικείμενο Sale (κεφαλίδα πώλησης)
			var sale = new Sale
			{
				SaleDate = DateTime.Now,
				TotalAmount = CartItems.Sum(c => c.TotalPrice), // ✅ Σύνολο όλων των προϊόντων
				Items = new List<SaleItem>() // ✅ Γραμμές προϊόντων
			};

			foreach (var cartItem in CartItems)
			{
				// 🛑 Έλεγχος αν υπάρχει αρκετό απόθεμα
				if (cartItem.Product.Stock < cartItem.Quantity)
				{
					MessageBox.Show($"Το προϊόν \"{cartItem.Product.Name}\" δεν έχει αρκετό απόθεμα.", "Προσοχή", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}

				// 🔻 Αφαιρούμε ποσότητα από το stock
				cartItem.Product.Stock -= cartItem.Quantity;
				await _unitOfWork.Products.UpdateAsync(cartItem.Product);

				// 🔹 Δημιουργούμε SaleItem
				var item = new SaleItem
				{
					ProductId = cartItem.Product.Id,
					Quantity = cartItem.Quantity,
					Price = cartItem.Price
				};

				sale.Items.Add(item);
			}

			// 🔹 Αποθηκεύουμε την πώληση με τα προϊόντα της
			await _unitOfWork.Sales.AddSaleAsync(sale);
			await _unitOfWork.CompleteAsync();

			// 🔹 Καθαρισμός καλαθιού
			CartItems.Clear();
			TotalPrice = 0;
		}


		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string propertyName) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
