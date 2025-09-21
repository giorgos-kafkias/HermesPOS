using HermesPOS.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HermesPOS.ViewModels
{
    public class QrReceptionViewModel
    {
        // Οι γραμμές της παραλαβής (θα τις γεμίσουμε αργότερα)
        public ObservableCollection<Models.StockReceptionItem> Items { get; }
            = new ObservableCollection<Models.StockReceptionItem>();

        // Placeholder properties για header
        public int SupplierId { get; set; }
        public string? QrUrl { get; set; }

        // Κουμπιά (θα τα “γεμίσουμε” αργότερα)
        public ICommand ImportFromQrCommand { get; set; } = default!;
        public ICommand SaveMappingsCommand { get; set; } = default!;
        public ICommand PostReceptionCommand { get; set; } = default!;

        public QrReceptionViewModel()
        {
            // Dummy data για δοκιμή UI
            Items.Add(new StockReceptionItem
            {
                SupplierCode = "ABC123",
                Description = "Δείγμα προϊόν",
                Quantity = 10,
                Barcode = "1234567890123",
                ProductName = "Προϊόν Δείγμα"
            });

            Items.Add(new StockReceptionItem
            {
                SupplierCode = "XYZ999",
                Description = "Δείγμα προϊόν 2",
                Quantity = 5,
                Barcode = "",
                ProductName = ""
            });
        }
    }

}
