using HermesPOS.Data.Repositories;
using HermesPOS.Models;
using HermesPOS.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;

namespace HermesPOS.ViewModels
{
    public class QrReceptionViewModel : INotifyPropertyChanged
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStockReceptionService _receptionService;
        public ObservableCollection<Supplier> Suppliers { get; } = new();

        // Κρατάμε το Draft που δημιουργήθηκε με "Αποθήκευση"
        private int? _currentReceptionId;
        private string? _currentMark;

        // Γραμμές παραλαβής (UI)
        public ObservableCollection<StockReceptionItem> Items { get; } = new();

        // Header (UI)
        private int _supplierId;
        public int SupplierId
        {
            get => _supplierId;
            set
            {
                if (_supplierId != value)
                {
                    _supplierId = value;
                    OnPropertyChanged(nameof(SupplierId)); // 👈 ενημερώνει το ComboBox
                }
            }
        }
        public string? QrUrl { get; set; }

        // Εντολές
        public ICommand ImportFromQrCommand { get; }
        public ICommand SaveMappingsCommand { get; }
        public ICommand PostReceptionCommand { get; }

        public QrReceptionViewModel(IUnitOfWork unitOfWork, IStockReceptionService receptionService)
        {
            _unitOfWork = unitOfWork;
            _receptionService = receptionService;

            ImportFromQrCommand = new RelayCommand(ImportFromQr);
            SaveMappingsCommand = new RelayCommand(SaveMappings);
            // Το Post ενεργοποιείται ΜΟΝΟ όταν υπάρχει Draft (μετά από Save)
            PostReceptionCommand = new RelayCommand(PostReception, () => _currentReceptionId.HasValue);
        }

        public async Task EnsureSuppliersLoadedAsync()
        {
            if (Suppliers.Count > 0) return; // φόρτωσες ήδη
            await LoadSuppliersAsync();
        }

        private async void ImportFromQr()
        {
            // Ασφάλεια: καθάρισε τρέχον draft στο UI
            _currentReceptionId = null;
            _currentMark = null;
            ((RelayCommand)PostReceptionCommand).RaiseCanExecuteChanged();

            var url = (QrUrl ?? "").Trim();
            var (ok, message, items, supplierId) = await _receptionService.FetchFromQrUrlAsync(url);

            if (!ok)
            {
                MessageBox.Show(message, "Εισαγωγή", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Γέμισμα γραμμών
            Items.Clear();
            foreach (var it in items) Items.Add(it);

            // Αν το service κατάφερε να αναγνωρίσει προμηθευτή, τον ορίζουμε
            if (supplierId.HasValue) SupplierId = supplierId.Value;

            // Προαιρετικό info (π.χ. για MARK)
            if (!string.IsNullOrEmpty(message))
                MessageBox.Show(message, "Εισαγωγή", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task LoadSuppliersAsync()
        {
            var list = await _unitOfWork.Suppliers.GetAllAsync();
            App.Current.Dispatcher.Invoke(() =>
            {
                Suppliers.Clear();
                foreach (var s in list) Suppliers.Add(s);

                // Αν δεν έχει επιλεγεί supplier, βάλε τον πρώτο διαθέσιμο
                if (SupplierId <= 0 && Suppliers.Any())
                    SupplierId = Suppliers.First().Id;
            });
        }
        private async void SaveMappings()
        {
            // 1) Διπλά barcodes σε διαφορετικούς SupplierCodes → μπλοκάρουμε
            var withBarcode = Items
                .Select((item, idx) => new { item, idx })
                .Where(x => !string.IsNullOrWhiteSpace(x.item.Barcode))
                .ToList();

            var duplicates = withBarcode
                .GroupBy(x => x.item.Barcode!.Trim())
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    Barcode = g.Key,
                    Rows = g.Select(x => new { x.idx, x.item.SupplierCode }).ToList(),
                    DistinctSupplierCodes = g.Select(x => x.item.SupplierCode ?? "").Distinct().Count()
                })
                .ToList();

            if (duplicates.Any(d => d.DistinctSupplierCodes > 1))
            {
                var msg = string.Join("\n\n", duplicates
                    .Where(d => d.DistinctSupplierCodes > 1)
                    .Select(d => $"Barcode: {d.Barcode}\nΓραμμές: {string.Join(", ", d.Rows.Select(r => $"#{r.idx + 1} ({r.SupplierCode})"))}"));

                MessageBox.Show(
                    "Διπλό barcode σε διαφορετικές γραμμές:\n\n" + msg,
                    "Σφάλμα αποθήκευσης",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 2) Προειδοποίηση για γραμμές χωρίς barcode (επιτρέπουμε Save)
            var missingBarcode = Items
                .Select((item, idx) => new { item, idx })
                .Where(x => string.IsNullOrWhiteSpace(x.item.Barcode))
                .ToList();

            if (missingBarcode.Any())
            {
                var lines = string.Join(", ", missingBarcode.Select(x => $"#{x.idx + 1}"));
                MessageBox.Show(
                    $"Προσοχή: {missingBarcode.Count} γραμμές χωρίς barcode (γραμμές: {lines}).\n" +
                    $"Θα αποθηκευτούν ως Draft και ΔΕΝ θα επιτραπεί Ολοκλήρωση (Post) μέχρι να συμπληρωθούν.",
                    "Προειδοποίηση", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // 🔹 Βρες ΕΓΚΥΡΟ SupplierId από τη βάση (όχι σκληροκώδικα)
            var suppliers = await _unitOfWork.Suppliers.GetAllAsync();
            if (suppliers == null || !suppliers.Any())
            {
                MessageBox.Show("Δεν υπάρχει καταχωρημένος προμηθευτής στη βάση. Δημιούργησε έναν πρώτα.",
                    "Αποθήκευση", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int supplierId = SupplierId;
            if (!suppliers.Any(s => s.Id == supplierId))
            {
                supplierId = suppliers.First().Id; // πάρε έναν υπαρκτό (π.χ. 17/20)
                SupplierId = supplierId;           // κράτα τον για τα επόμενα saves/post
            }

            try
            {
                if (_currentReceptionId.HasValue)
                {
                    // -------- UPDATE ΥΠΑΡΧΟΝΤΟΣ DRAFT --------
                    var rec = await _unitOfWork.StockReceptions.GetDraftByIdAsync(_currentReceptionId.Value);
                    if (rec == null)
                    {
                        MessageBox.Show("Το draft δεν βρέθηκε (ίσως έχει ήδη κλειδωθεί).",
                            "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    rec.SupplierId = supplierId;
                    rec.ReceptionDate = DateTime.Now;
                    rec.Status = ReceptionStatus.Draft; // ασφάλεια

                    rec.Items.Clear();
                    foreach (var i in Items)
                    {
                        rec.Items.Add(new StockReceptionItem
                        {
                            SupplierCode = i.SupplierCode ?? string.Empty,
                            Description = i.Description ?? string.Empty,
                            Quantity = i.Quantity,
                            Barcode = string.IsNullOrWhiteSpace(i.Barcode) ? null : i.Barcode
                        });
                    }

                    _unitOfWork.StockReceptions.Update(rec);
                    await _unitOfWork.CompleteAsync();

                    MessageBox.Show($"✅ Το Draft #{rec.Id} ενημερώθηκε (MARK: {rec.Mark}).",
                        "Αποθήκευση", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // -------- ΔΗΜΙΟΥΡΓΙΑ ΝΕΟΥ DRAFT --------
                    var mark = "DEV-" + DateTime.Now.ToString("yyyyMMddHHmmss");

                    var exists = await _unitOfWork.StockReceptions.ExistsByMarkAsync(mark);
                    if (exists)
                    {
                        MessageBox.Show($"Το MARK {mark} υπάρχει ήδη. Η αποθήκευση ακυρώθηκε.",
                            "Προειδοποίηση", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var reception = new StockReception
                    {
                        SupplierId = supplierId,
                        ReceptionDate = DateTime.Now,
                        Mark = mark,
                        Status = ReceptionStatus.Draft
                    };

                    foreach (var i in Items)
                    {
                        reception.Items.Add(new StockReceptionItem
                        {
                            SupplierCode = i.SupplierCode ?? string.Empty,
                            Description = i.Description ?? string.Empty,
                            Quantity = i.Quantity,
                            Barcode = string.IsNullOrWhiteSpace(i.Barcode) ? null : i.Barcode
                        });
                    }

                    var rec = await _unitOfWork.StockReceptions.AddDraftAsync(reception);
                    await _unitOfWork.CompleteAsync();

                    _currentReceptionId = rec.Id;
                    _currentMark = rec.Mark;
                    ((RelayCommand)PostReceptionCommand).RaiseCanExecuteChanged();

                    MessageBox.Show($"✅ Δημιουργήθηκε Draft Παραλαβή #{rec.Id}\nMARK: {rec.Mark}",
                        "Αποθήκευση", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                var baseMsg = ex.GetBaseException().Message;
                MessageBox.Show(
                    $"Σφάλμα κατά την αποθήκευση:\n{baseMsg}\n\n{ex}",
                    "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async void PostReception()
        {
            if (!_currentReceptionId.HasValue)
            {
                MessageBox.Show("Πρέπει πρώτα να κάνεις Αποθήκευση (δημιουργία Draft).",
                    "Αδυναμία Post", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (ok, message) = await _receptionService.PostReceptionAsync(_currentReceptionId.Value);

            MessageBox.Show(message,
                ok ? "Ολοκλήρωση" : "Αποτυχία",
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);

            if (ok)
            {
                // καθάρισμα UI μετά το Post
                _currentReceptionId = null;
                _currentMark = null;
                Items.Clear();
                ((RelayCommand)PostReceptionCommand).RaiseCanExecuteChanged();
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}