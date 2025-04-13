using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HermesPOS.Data.Repositories;
using HermesPOS.Models;

namespace HermesPOS.ViewModels
{
	public class AddProductViewModel : INotifyPropertyChanged
	{
		private readonly IUnitOfWork _unitOfWork;
		private string _barcode;

		public string Barcode
		{
			get => _barcode;
			set
			{
				_barcode = value;
				OnPropertyChanged(nameof(Barcode));
			}
		}
		private string _priceText;
		// Το `PriceText` χρησιμοποιείται για την εισαγωγή δεδομένων στο TextBox.
		public string PriceText
		{
			get => _priceText;
			set
			{
				_priceText = value;
				// Προσπαθεί να μετατρέψει το `PriceText` σε `decimal` με βάση την αγγλική κουλτούρα (δεκαδικός χωριστήρας = ".")
				if (decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal parsedValue))
				{
					Price = parsedValue; // Αν η μετατροπή πετύχει, αποθηκεύει την αριθμητική τιμή στο `Price`
				}
				OnPropertyChanged(nameof(PriceText));  // Ενημέρωση UI
			}
		}

		private string _wholesalePriceText;
		public string WholesalePriceText
		{
			get => _wholesalePriceText;
			set
			{
				_wholesalePriceText = value;
				OnPropertyChanged(nameof(WholesalePriceText));
			}
		}

		public string Name { get; set; }
		public decimal Price { get; private set; } // Η αριθμητική τιμή της τιμής του προϊόντος. Κρατάει την τελική τιμή που θα αποθηκευτεί στη βάση.
		public int Stock { get; set; }
		public ObservableCollection<Category> Categories { get; set; }
		public ObservableCollection<Supplier> Suppliers { get; set; }
		public Category SelectedCategory { get; set; }
		public Supplier SelectedSupplier { get; set; }
		public Action CloseAction { get; set; } // Ανάθεση αυτής της μεθόδου από το View

		public ICommand SaveProductCommand { get; } // Εντολή για αποθήκευση

		public AddProductViewModel(IUnitOfWork unitOfWork, string scannedBarcode = null)
		{
			_unitOfWork = unitOfWork;
			Barcode = scannedBarcode ?? ""; // Αν υπάρχει barcode από σκανάρισμα, το περνάει αυτόματα
			Categories = new ObservableCollection<Category>();
			Suppliers = new ObservableCollection<Supplier>();
			LoadCategoriesAndSuppliers();
			SaveProductCommand = new AsyncRelayCommand(SaveProduct);
		}

		private async void LoadCategoriesAndSuppliers()
		{
			var categories = await _unitOfWork.Categories.GetAllAsync();
			var suppliers = await _unitOfWork.Suppliers.GetAllAsync();

			Categories.Clear();
			Suppliers.Clear();

			foreach (var category in categories)
				Categories.Add(category);

			foreach (var supplier in suppliers)
				Suppliers.Add(supplier);
		}

		private async Task SaveProduct()
		{
			if (string.IsNullOrWhiteSpace(Barcode) || string.IsNullOrWhiteSpace(Name) || Price <= 0 || Stock < 0 || SelectedCategory == null || SelectedSupplier == null)
			{
				MessageBox.Show("Παρακαλώ συμπληρώστε όλα τα πεδία σωστά!", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			decimal? wholesalePrice = null;

			if (!string.IsNullOrWhiteSpace(WholesalePriceText))
			{
				var clean = WholesalePriceText.Replace(',', '.');

				if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
					wholesalePrice = parsed;
			}

			// Δημιουργία νέου προϊόντος με τα δεδομένα από το ViewModel
			var newProduct = new Product
			{
				Barcode = Barcode,
				Name = Name,
				Price = Price,
				Stock = Stock,
				CategoryId = SelectedCategory.Id,
				SupplierId = SelectedSupplier.Id,
				WholesalePrice = wholesalePrice
			};

			await _unitOfWork.Products.AddAsync(newProduct);
			await _unitOfWork.CompleteAsync();

			AutoClosingMessageBox.Show("Το προϊόν προστέθηκε επιτυχώς!", "Επιτυχία", 1000); //  Κλείσιμο σε 2 δευτερόλεπτα

			//MessageBox.Show("Το προϊόν προστέθηκε επιτυχώς!", "Επιτυχία", MessageBoxButton.OK, MessageBoxImage.Information);

			CloseAction?.Invoke(); //  Κλείσιμο του παραθύρου
		}
		public static class AutoClosingMessageBox
		{
			[DllImport("user32.dll", CharSet = CharSet.Auto)]
			private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

			[DllImport("user32.dll", CharSet = CharSet.Auto)]
			private static extern int SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

			private const uint WM_CLOSE = 0x0010;

			public static void Show(string message, string title, int timeout = 1000) // ⏳ Κλείσιμο σε 2 δευτερόλεπτα
			{
				Task.Run(() =>
				{
					IntPtr msgBox = IntPtr.Zero;
					do
					{
						msgBox = FindWindow(null, title); // 🔍 Αναζήτηση του MessageBox
						Task.Delay(500).Wait(); //  Επανάληψη μέχρι να βρεθεί
					}
					while (msgBox == IntPtr.Zero);

					Task.Delay(timeout).Wait(); //  Αναμονή για το χρονικό όριο
					SendMessage(msgBox, WM_CLOSE, IntPtr.Zero, IntPtr.Zero); // Κλείσιμο του MessageBox
				});

				MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
