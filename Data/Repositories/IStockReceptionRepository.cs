using System.Threading.Tasks;
using HermesPOS.Models;

namespace HermesPOS.Data.Repositories
{
    public interface IStockReceptionRepository
    {
        Task<bool> ExistsByMarkAsync(string mark);
        Task<StockReception> AddDraftAsync(StockReception reception);
        // 🔹 ΝΕΟ:
        Task<StockReception?> GetDraftByIdAsync(int id);
        void Update(StockReception reception); // χωρίς SaveChanges
    }
}
