using System;
using System.Threading.Tasks;

namespace HermesPOS.Data.Repositories
{
	public interface IUnitOfWork : IDisposable
	{
		IProductRepository Products { get; } // Διαχείριση προϊόντων
		ICategoryRepository Categories { get; } // Διαχείριση κατηγοριών
		ISupplierRepository Suppliers { get; } // Διαχείριση προμηθευτών
		ISaleRepository Sales { get; } // Διαχείριση πωλησεων
        IStockReceptionRepository StockReceptions { get; }
        Task<int> CompleteAsync(); // Αποθήκευση αλλαγών στη βάση
    }
}

