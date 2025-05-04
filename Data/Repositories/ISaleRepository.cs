using System.Collections.Generic;
using System.Threading.Tasks;
using HermesPOS.Models;

namespace HermesPOS.Data.Repositories
{
	public interface ISaleRepository 
	{
		Task AddSaleAsync(Sale sale);
		Task<IEnumerable<Sale>> GetSalesByDateAsync(DateTime date);
		Task<IEnumerable<BestSellerItem>> GetBestSellingProductsAsync(int topN,	int? categoryId = null,	int? supplierId = null,	DateTime? fromDate = null,
																		DateTime? toDate = null);
		Task<IEnumerable<Sale>> GetSalesByDateRangeAsync(DateTime? fromDate, DateTime? toDate);
		Task DeleteAsync(int saleId);

	}
}
