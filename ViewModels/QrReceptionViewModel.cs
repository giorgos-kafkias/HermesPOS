using HermesPOS.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using HermesPOS.Data.Repositories;

namespace HermesPOS.ViewModels
{
    public class QrReceptionViewModel
    {
        private readonly IUnitOfWork _unitOfWork;

        // Κρατάμε το Draft που δημιουργήθηκε με "Αποθήκευση"
        private int? _currentReceptionId;
        private string? _currentMark;

        // Γραμμές παραλαβής (UI)
        public ObservableCollection<StockReceptionItem> Items { get; } = new();

        // Header (UI)
        public int SupplierId { get; set; }
        public string? QrUrl { get; set; }

        // Εντολές
        public ICommand ImportFromQrCommand { get; }
        public ICommand SaveMappingsCommand { get; }
        public ICommand PostReceptionCommand { get; }

        public QrReceptionViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

            ImportFromQrCommand = new RelayCommand(ImportFromQr);
            SaveMappingsCommand = new RelayCommand(SaveMappings);
            // Το Post ενεργοποιείται ΜΟΝΟ όταν υπάρχει Draft (μετά από Save)
            PostReceptionCommand = new RelayCommand(PostReception, () => _currentReceptionId.HasValue);
        }

        private void ImportFromQr()
        {
            // Νέα εισαγωγή → ακυρώνουμε τυχόν προηγούμενο Draft στο UI
            _currentReceptionId = null;
            _currentMark = null;
            ((RelayCommand)PostReceptionCommand).RaiseCanExecuteChanged();

            Items.Clear();
            Items.Add(new StockReceptionItem
            {
                SupplierCode = "ABC123",
                Description = "Δείγμα προϊόν",
                Quantity = 10,
                Barcode = "842"
            });

            Items.Add(new StockReceptionItem
            {
                SupplierCode = "XYZ999",
                Description = "Δείγμα προϊόν 2",
                Quantity = 5,
                Barcode = ""
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

            var supplierId = SupplierId > 0 ? SupplierId : 1;

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

                    // ΔΕΝ αλλάζουμε το MARK — μένει σταθερό
                    rec.SupplierId = supplierId;
                    rec.ReceptionDate = DateTime.Now;

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
                    // (αν έχεις πραγματικό MARK από QR, βάλ’ το εδώ αντί για DEV-....)
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

                    // ⬇️ AddDraftAsync ΕΠΙΣΤΡΕΦΕΙ StockReception (ΟΧΙ int)
                    var rec = await _unitOfWork.StockReceptions.AddDraftAsync(reception);
                    await _unitOfWork.CompleteAsync();

                    // ✅ Κρατάμε Id/Mark για επόμενα Save & Post
                    _currentReceptionId = rec.Id;
                    _currentMark = rec.Mark;
                    ((RelayCommand)PostReceptionCommand).RaiseCanExecuteChanged();

                    MessageBox.Show($"✅ Δημιουργήθηκε Draft Παραλαβή #{rec.Id}\nMARK: {rec.Mark}",
                        "Αποθήκευση", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Σφάλμα κατά την αποθήκευση:\n" + ex.Message,
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

            var (ok, message) = await _unitOfWork.PostReceptionAsync(_currentReceptionId.Value);

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

    }
}
