using HermesPOS.Models;
using System.Threading.Tasks;

namespace HermesPOS.Services
{
    public interface IStockReceptionService
    {
        Task<(bool ok, string message)> PostReceptionAsync(int receptionId);

        // ΝΕΟ: παίρνει URL από QR/ΑΑΔΕ και επιστρέφει γραμμές + προμηθευτή (αν προκύπτει)
        Task<(bool ok, string message, List<StockReceptionItem> items, int? supplierId)>
            FetchFromQrUrlAsync(string qrUrl);
    }
}
