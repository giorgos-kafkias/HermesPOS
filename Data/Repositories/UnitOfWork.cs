using HermesPOS.Data;
using HermesPOS.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace HermesPOS.Data.Repositories
{
	public class UnitOfWork : IUnitOfWork
	{
		private readonly ApplicationDbContext _db;

		public IProductRepository Products { get; }
		public ICategoryRepository Categories { get; }
		public ISupplierRepository Suppliers { get; }
		public ISaleRepository Sales { get; }
        public IStockReceptionRepository StockReceptions { get; }


        public UnitOfWork(ApplicationDbContext db,
						  IProductRepository productRepository,
						  ICategoryRepository categoryRepository,
						  ISupplierRepository supplierRepository,
						  ISaleRepository saleRepository, 
						  IStockReceptionRepository stockReceptionRepository)
		{
			_db = db;
			Products = productRepository;
			Categories = categoryRepository;
			Suppliers = supplierRepository;
			Sales=saleRepository;
            StockReceptions = stockReceptionRepository;
        }

		public async Task<int> CompleteAsync()
		{
			return await _db.SaveChangesAsync(); // Αποθηκεύει τις αλλαγές στη βάση
		}

		public void Dispose()
		{
			_db.Dispose(); // Αποδεσμεύει τους πόρους της βάσης δεδομένων
		}
    }
}
