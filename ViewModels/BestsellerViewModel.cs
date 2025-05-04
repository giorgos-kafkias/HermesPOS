using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HermesPOS.Data.Repositories;
using HermesPOS.Models;
using Microsoft.Extensions.DependencyInjection;

namespace HermesPOS.ViewModels
{
	public class BestsellerViewModel : INotifyPropertyChanged
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly IServiceProvider _serviceProvider;
		private Category _selectedCategory;
		private Supplier _selectedSupplier;
		private int _topN = 10;

		public ObservableCollection<BestSellerItem> Bestsellers { get; set; } = new();
		public ObservableCollection<Category> Categories { get; set; } = new();
		public ObservableCollection<Supplier> Suppliers { get; set; } = new();
		public ObservableCollection<int> TopNOptions { get; } = new ObservableCollection<int> { 5, 10, 20 };


		public event PropertyChangedEventHandler PropertyChanged;

		public int TopN
		{
			get => _topN;
			set
			{
				_topN = value;
				OnPropertyChanged(nameof(TopN));
			}
		}

		private DateTime? _fromDate;
		public DateTime? FromDate
		{
			get => _fromDate;
			set
			{
				if (value.HasValue)
					_fromDate = value.Value.Date; // Ορίζει την ώρα 00:00:00
				else
					_fromDate = null;

				OnPropertyChanged(nameof(FromDate));
			}
		}

		private DateTime? _toDate;
		public DateTime? ToDate
		{
			get => _toDate;
			set
			{
				if (value.HasValue)
					_toDate = value.Value.Date.AddDays(1).AddSeconds(-1); // Ορίζει την ώρα 23:59:59
				else
					_toDate = null;

				OnPropertyChanged(nameof(ToDate));
			}
		}

		public Category SelectedCategory
		{
			get => _selectedCategory;
			set
			{
				_selectedCategory = value;
				OnPropertyChanged(nameof(SelectedCategory));
			}
		}

		public Supplier SelectedSupplier
		{
			get => _selectedSupplier;
			set
			{
				_selectedSupplier = value;
				OnPropertyChanged(nameof(SelectedSupplier));
			}
		}

		public ICommand LoadBestsellersCommand { get; }

		public BestsellerViewModel(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
			_unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>(); // ✅ Διατήρηση UnitOfWork

			LoadBestsellersCommand = new RelayCommand(async () => await LoadBestsellers());
			// 🔹 Εκτελούμε ασύγχρονα τη φόρτωση των κατηγοριών και προμηθευτών
			_ = Task.Run(async () => await LoadCategoriesAndSuppliers());
		}

		private async Task LoadCategoriesAndSuppliers()
		{
			try
			{
				using (var scope = _serviceProvider.CreateScope()) // ✅ Δημιουργούμε νέο scope
				{
					var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

					var categories = await unitOfWork.Categories.GetAllAsync();
					var suppliers = await unitOfWork.Suppliers.GetAllAsync();

					App.Current.Dispatcher.Invoke(() =>
					{
						Categories.Clear();
						Suppliers.Clear();

						Categories.Add(new Category { Id = 0, Name = "Όλες οι Κατηγορίες" });
						Suppliers.Add(new Supplier { Id = 0, Name = "Όλοι οι Προμηθευτές" });

						foreach (var category in categories) Categories.Add(category);
						foreach (var supplier in suppliers) Suppliers.Add(supplier);
					});
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ Σφάλμα φόρτωσης κατηγοριών/προμηθευτών: {ex.Message}");
			}
		}

		public async Task LoadBestsellers()
		{
			try
			{
				int? categoryId = SelectedCategory?.Id == 0 ? null : SelectedCategory?.Id;
				int? supplierId = SelectedSupplier?.Id == 0 ? null : SelectedSupplier?.Id;

				var bestsellers = await _unitOfWork.Sales.GetBestSellingProductsAsync(TopN, categoryId, supplierId, FromDate, ToDate);
				Bestsellers.Clear();
				foreach (var item in bestsellers)
					Bestsellers.Add(item);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ Σφάλμα φόρτωσης Bestsellers: {ex.Message}");
			}
		}

		public async Task OnTabSelected()
		{
			if (Bestsellers.Count == 0) //  Φόρτωση μόνο αν είναι κενή η λίστα
			{
				await LoadBestsellers();
			}
		}

		private void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
