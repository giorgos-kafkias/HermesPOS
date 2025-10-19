using System.Threading.Tasks;
using HermesPOS.Models;
using Microsoft.EntityFrameworkCore;
using HermesPOS.Data;

namespace HermesPOS.Data.Repositories
{
    public class StockReceptionRepository : IStockReceptionRepository
    {
        private readonly ApplicationDbContext _context;
        public StockReceptionRepository(ApplicationDbContext context) => _context = context;

        public async Task<bool> ExistsByMarkAsync(string mark)
            => await _context.StockReceptions.AnyAsync(r => r.Mark == mark);

        public Task<StockReception> AddDraftAsync(StockReception reception)
        {
            _context.StockReceptions.Add(reception);
            // SaveChanges θα γίνει από UnitOfWork.CompleteAsync()
            return Task.FromResult(reception);
        }

        // 🔹 ΝΕΟ:
        public async Task<StockReception?> GetDraftByIdAsync(int id)
        {
            return await _context.StockReceptions
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == id && r.Status == ReceptionStatus.Draft);
        }

        public void Update(StockReception reception)
        {
            _context.StockReceptions.Update(reception);
        }
        public async Task<StockReception?> GetByMarkAsync(string mark)
        {
            return await _context.StockReceptions
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Mark == mark);
        }

    }
}
