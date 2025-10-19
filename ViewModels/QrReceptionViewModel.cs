using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using HermesPOS.Data.Repositories;
using HermesPOS.Models;
using HermesPOS.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace HermesPOS.ViewModels
{
    public class QrReceptionViewModel : INotifyPropertyChanged
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStockReceptionService _receptionService;

        // Τρέχον draft
        private int? _currentReceptionId;
        private string? _currentMark;

        // Γραμμές UI
        public ObservableCollection<StockReceptionItem> Items { get; } = new();
        public ObservableCollection<Supplier> Suppliers { get; } = new();

        // Προτάσεις PASS 2 (μόνο εμφάνιση/εφαρμογή από χρήστη)
        public sealed class Proposal
        {
            public StockReceptionItem Item { get; }
            public string Barcode { get; }
            public string ProductName { get; }

            public Proposal(StockReceptionItem item, string barcode, string productName)
            {
                Item = item; Barcode = barcode; ProductName = productName;
            }
        }
        public ObservableCollection<Proposal> Suggestions { get; } = new();
        public bool HasSuggestions => Suggestions.Any();

        // Header UI
        private int _supplierId;
        public int SupplierId
        {
            get => _supplierId;
            set
            {
                if (_supplierId != value)
                {
                    _supplierId = value;
                    OnPropertyChanged(nameof(SupplierId));
                    OnPropertyChanged(nameof(HasValidSupplier));
                    ((RelayCommand)SaveMappingsCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)PostReceptionCommand).RaiseCanExecuteChanged();

                    // Re-map PASS 1 και μετά ανανέωση προτάσεων PASS 2
                    _ = RefreshAfterSupplierChangeAsync();
                }
            }
        }

        public bool HasValidSupplier => SupplierId > 0 && Suppliers.Any(s => s.Id == SupplierId);

        public string? QrUrl { get; set; }

        // Commands
        public ICommand ImportFromQrCommand { get; }
        public ICommand SaveMappingsCommand { get; }
        public ICommand PostReceptionCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand ApplySuggestionCommand { get; }
        public ICommand ClearQrCommand { get; }


        public QrReceptionViewModel(IUnitOfWork unitOfWork, IStockReceptionService receptionService)
        {
            _unitOfWork = unitOfWork;
            _receptionService = receptionService;

            ImportFromQrCommand = new RelayCommand(ImportFromQr);
            SaveMappingsCommand = new RelayCommand(SaveMappings, () => HasValidSupplier && Items.Any());
            PostReceptionCommand = new RelayCommand(PostReception, () => _currentReceptionId.HasValue && HasValidSupplier);
            RemoveLineCommand = new RelayCommand<StockReceptionItem>(RemoveLine);
            ApplySuggestionCommand = new RelayCommand<Proposal>(ApplySuggestion);
            ClearQrCommand = new RelayCommand(ClearQr);

            _ = LoadSuppliersAsync();
        }

        private void RemoveLine(StockReceptionItem item)
        {
            if (item == null) return;
            Items.Remove(item);
            // Καθάρισε σχετικές προτάσεις για το item
            var toRemove = Suggestions.Where(p => p.Item == item).ToList();
            foreach (var p in toRemove) Suggestions.Remove(p);
            OnPropertyChanged(nameof(HasSuggestions));
        }

        public async Task EnsureSuppliersLoadedAsync()
        {
            if (Suppliers.Count > 0) return;

            var list = await _unitOfWork.Suppliers.GetAllAsync();

            App.Current.Dispatcher.Invoke(() =>
            {
                Suppliers.Clear();
                foreach (var s in list) Suppliers.Add(s);

                if (SupplierId <= 0 && Suppliers.Any())
                    SupplierId = Suppliers.First().Id;
            });
        }

        private async Task LoadSuppliersAsync()
        {
            var list = await _unitOfWork.Suppliers.GetAllAsync();

            App.Current.Dispatcher.Invoke(() =>
            {
                Suppliers.Clear();
                foreach (var s in list) Suppliers.Add(s);

                ((RelayCommand)SaveMappingsCommand).RaiseCanExecuteChanged();
                ((RelayCommand)PostReceptionCommand).RaiseCanExecuteChanged();
            });
        }

        private async void ImportFromQr()
        {
            _currentReceptionId = null;
            _currentMark = null;
            ((RelayCommand)PostReceptionCommand).RaiseCanExecuteChanged();

            var url = (QrUrl ?? "").Trim();
            var (ok, message, items, supplierId, mark) = await _receptionService.FetchFromQrUrlAsync(url);
            if (!ok) { MessageBox.Show(message, "Εισαγωγή", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            // ΧΡΗΣΗ του mark που ΕΠΕΣΤΡΕΨΕ το service (όχι από το message)
            if (!string.IsNullOrWhiteSpace(mark))
            {
                var existing = await _unitOfWork.StockReceptions.GetByMarkAsync(mark);
                if (existing != null)
                {
                    _currentReceptionId = existing.Id;
                    _currentMark = existing.Mark;

                    Items.Clear();
                    foreach (var i in existing.Items) Items.Add(i);

                    SupplierId = existing.SupplierId;

                    MessageBox.Show($"Φορτώθηκε το υπάρχον draft #{existing.Id}\n(MARK: {existing.Mark})",
                        "Επαναφόρτωση", MessageBoxButton.OK, MessageBoxImage.Information);

                    ((RelayCommand)SaveMappingsCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)PostReceptionCommand).RaiseCanExecuteChanged();
                    return;
                }
                else
                {
                    // Κρατάμε το MARK της ΑΑΔΕ για το νέο draft
                    _currentMark = mark;
                }
            }

            Items.Clear();
            foreach (var it in items) Items.Add(it);

            if (SupplierId <= 0 && supplierId.HasValue) SupplierId = supplierId.Value;

            if (SupplierId > 0 && Items.Any())
                await _receptionService.AutoMapBarcodesAsync(SupplierId, Items);

            await RefreshSuggestionsAsync();

            if (!string.IsNullOrEmpty(message))
                MessageBox.Show(message, "Εισαγωγή", MessageBoxButton.OK, MessageBoxImage.Information);

            ((RelayCommand)SaveMappingsCommand).RaiseCanExecuteChanged();
        }

        private async void SaveMappings()
        {
            if (!HasValidSupplier)
            {
                MessageBox.Show("Επίλεξε προμηθευτή από το dropdown πριν την αποθήκευση.",
                    "Αποθήκευση", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var withBarcode = Items.Select((item, idx) => new { item, idx })
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

                MessageBox.Show("Διπλό barcode σε διαφορετικές γραμμές:\n\n" + msg,
                    "Σφάλμα αποθήκευσης", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var missingBarcode = Items.Select((item, idx) => new { item, idx })
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

            int supplierId = SupplierId;

            try
            {
                if (_currentReceptionId.HasValue)
                {
                    var rec = await _unitOfWork.StockReceptions.GetDraftByIdAsync(_currentReceptionId.Value);
                    if (rec == null)
                    {
                        MessageBox.Show("Το draft δεν βρέθηκε (ίσως έχει ήδη κλειδωθεί).",
                            "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    rec.SupplierId = supplierId;
                    rec.ReceptionDate = DateTime.Now;
                    rec.Status = ReceptionStatus.Draft;

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
                    var mark = _currentMark ?? "DEV-" + DateTime.Now.ToString("yyyyMMddHHmmss");

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
                    $"Σφάλμα κατά την αποθήκευση:\n{baseMsg}",
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

            if (!HasValidSupplier)
            {
                MessageBox.Show("Επίλεξε προμηθευτή πριν την Ολοκλήρωση.",
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
                _currentReceptionId = null;
                _currentMark = null;
                Items.Clear();
                Suggestions.Clear();
                OnPropertyChanged(nameof(HasSuggestions));

                ((RelayCommand)PostReceptionCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SaveMappingsCommand).RaiseCanExecuteChanged();
            }
        }

        private void ClearQr()
        {
            QrUrl = string.Empty;
            OnPropertyChanged(nameof(QrUrl));
        }

        // -------- ΝΕΑ: helpers για προτάσεις --------

        private async Task RefreshAfterSupplierChangeAsync()
        {
            if (SupplierId <= 0 || !Items.Any())
            {
                Suggestions.Clear();
                OnPropertyChanged(nameof(HasSuggestions));
                return;
            }

            // PASS 1
            await _receptionService.AutoMapBarcodesAsync(SupplierId, Items);

            // PASS 2
            await RefreshSuggestionsAsync();

        }

        private async Task RefreshSuggestionsAsync()
        {
            if (SupplierId <= 0) { Suggestions.Clear(); OnPropertyChanged(nameof(HasSuggestions)); return; }

            var props = await _receptionService.SuggestBarcodesAsync(SupplierId, Items);

            Suggestions.Clear();
            foreach (var (item, barcode, productName) in props)
                Suggestions.Add(new Proposal(item, barcode, productName));

            OnPropertyChanged(nameof(HasSuggestions));
        }

        private void ApplySuggestion(Proposal? proposal)
        {
            if (proposal == null) return;

            // Ο χρήστης επιβεβαίωσε → γέμισε Barcode στη γραμμή
            proposal.Item.Barcode = proposal.Barcode;

            // Βγάλε την πρόταση (και τυχόν άλλες για το ίδιο item)
            var toRemove = Suggestions.Where(p => p.Item == proposal.Item).ToList();
            foreach (var p in toRemove) Suggestions.Remove(p);
            OnPropertyChanged(nameof(HasSuggestions));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
