using HermesPOS.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HermesPOS.Services
{
    public interface IStockReceptionService
    {
        Task<(bool ok, string message)> PostReceptionAsync(int receptionId);

        // Παίρνει URL/token από QR/ΑΑΔΕ και επιστρέφει γραμμές + προμηθευτή (αν προκύπτει)
        Task<(bool ok, string message, List<StockReceptionItem> items, int? supplierId, string? mark)>
            FetchFromQrUrlAsync(string qrUrl);

        // PASS 1: συμπλήρωση barcodes από SupplierCode → Product
        Task<int> AutoMapBarcodesAsync(int supplierId, IEnumerable<StockReceptionItem> items);

        // PASS 2: προτάσεις (όχι auto-fill) βάσει SupplierId + normalized ονόματος
        Task<List<(StockReceptionItem item, string barcode, string productName)>>
            SuggestBarcodesAsync(int supplierId, IEnumerable<StockReceptionItem> items);
    }
}
