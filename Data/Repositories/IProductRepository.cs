using HermesPOS.Models;
using NPOI.SS.Formula.Functions;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace HermesPOS.Data.Repositories
{
	/// <summary>
	/// Ορίζει τις λειτουργίες διαχείρισης προϊόντων.
	/// </summary>
	public interface IProductRepository
	{
		/// Επιστρέφει όλα τα προϊόντα από τη βάση δεδομένων.
		Task<List<Product>> GetAllAsync(Expression<Func<Product, bool>>? filter, string? includeProperties = null);

		/// Επιστρέφει ένα προϊόν με βάση το ID του.
		Task<Product> GetByIdAsync(int id);

		/// Επιστρέφει ένα προϊόν με βάση το barcode του.
		Task<Product> GetByBarcodeAsync(string barcode);

		/// Προσθέτει ένα νέο προϊόν στη βάση δεδομένων.
		Task AddAsync(Product product);

		/// Ενημερώνει ένα υπάρχον προϊόν στη βάση δεδομένων.
		Task UpdateAsync(Product product);

		/// Διαγράφει ένα προϊόν με βάση το ID του.
		Task DeleteAsync(int id);
	}
}

