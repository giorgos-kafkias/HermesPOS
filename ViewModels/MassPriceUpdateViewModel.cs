using HermesPOS.Data.Repositories;
using HermesPOS.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace HermesPOS.ViewModels
{
    public class MassPriceUpdateViewModel : INotifyPropertyChanged
    {
        private readonly IUnitOfWork _unitOfWork;

        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<Product> PreviewProducts { get; } = new();
        public ObservableCollection<Supplier> Suppliers { get; } = new();
        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<string> PriceTypes { get; } =
            [
               "Λιανική Τιμή",
                "Χονδρική Τιμή"
            ];
        public ICommand ApplyCommand { get; }

        public MassPriceUpdateViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

            ApplyCommand = new RelayCommand(async () => await ApplyAsync());

            //_ = LoadAsync();
        }

        // ---------------- SELECTIONS ----------------
        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (_selectedSupplier == value)
                    return;

                _selectedSupplier = value;
                OnPropertyChanged(nameof(SelectedSupplier));
                BuildPreview();
            }
        }

        private Category? _selectedCategory;
        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory == value)
                    return;

                _selectedCategory = value;
                OnPropertyChanged(nameof(SelectedCategory));
                BuildPreview();
            }
        }

        public bool IsRetailSelected =>
            SelectedPriceType == "Λιανική Τιμή";

        public bool IsWholesaleSelected =>
            SelectedPriceType == "Χονδρική Τιμή";

        private string _selectedPriceType = "Λιανική Τιμή";
        public string SelectedPriceType
        {
            get => _selectedPriceType;
            set
            {
                if (_selectedPriceType == value)
                    return;

                _selectedPriceType = value;

                OnPropertyChanged(nameof(SelectedPriceType));
                OnPropertyChanged(nameof(IsRetailSelected));
                OnPropertyChanged(nameof(IsWholesaleSelected));

                BuildPreview();
            }
        }
        private string _value = "";
        public string Value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return;

                _value = value;
                OnPropertyChanged(nameof(Value));
                BuildPreview();
            }
        }
        // ---------------- LOAD (FIXED - NO REASSIGN COLLECTIONS) ----------------
        public async Task LoadAsync()
        {
            var products = await _unitOfWork.Products.GetAllAsync(null, "Supplier,Category");
            var suppliers = await _unitOfWork.Suppliers.GetAllAsync();
            var categories = await _unitOfWork.Categories.GetAllAsync();

            App.Current.Dispatcher.Invoke(() =>
            {
                Products.Clear();
                foreach (var p in products)
                    Products.Add(p);

                Suppliers.Clear();
                foreach (var s in suppliers)
                    Suppliers.Add(s);

                Categories.Clear();
                foreach (var c in categories)
                    Categories.Add(c);
            });

            BuildPreview();
        }

        // ---------------- FILTER ----------------
        private IEnumerable<Product> GetTargetProducts()
        {
            if (SelectedSupplier == null &&
                SelectedCategory == null)
            {
                return Enumerable.Empty<Product>();
            }

            return Products.Where(p =>
                (SelectedSupplier == null ||
                 p.SupplierId == SelectedSupplier.Id)
                &&
                (SelectedCategory == null ||
                 p.CategoryId == SelectedCategory.Id));
        }
        // ---------------- PREVIEW ----------------
        private void BuildPreview()
        {
            PreviewProducts.Clear();

            foreach (var p in GetTargetProducts())
            {
                PreviewProducts.Add(p);
            }
        }

        // ---------------- APPLY ----------------
        private async Task ApplyAsync()
        {
            if (!decimal.TryParse(Value, out var newPrice))
            {
                MessageBox.Show("Μη έγκυρη τιμή");
                return;
            }

            var products = GetTargetProducts().ToList();

            if (!products.Any())
            {
                MessageBox.Show("Δεν βρέθηκαν προϊόντα.");
                return;
            }

            var confirm = MessageBox.Show(
                $"Θα ενημερωθούν {products.Count} προϊόντα.",
                "Επιβεβαίωση",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            foreach (var p in products)
            {
                if (SelectedPriceType == "Λιανική Τιμή")
                {
                    p.Price = newPrice;
                }
                else
                {
                    p.WholesalePrice = newPrice;
                }

                await _unitOfWork.Products.UpdateAsync(p);
            }

            await _unitOfWork.CompleteAsync();

            await LoadAsync();

            BuildPreview();

            MessageBox.Show("Οι τιμές ενημερώθηκαν επιτυχώς.");
        }
        // ---------------- INotify ----------------

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}