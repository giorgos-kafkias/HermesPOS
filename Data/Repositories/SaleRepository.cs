using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HermesPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace HermesPOS.Data.Repositories
{
	public class SaleRepository : ISaleRepository
	{
		private readonly ApplicationDbContext _db;

		public SaleRepository(ApplicationDbContext db)
		{
			_db = db;
		}

		// ✅ Προσθήκη νέας πώλησης (με προϊόντα)
		public async Task AddSaleAsync(Sale sale)
		{
			await _db.Sales.AddAsync(sale);
		}

		// ✅ Πωλήσεις συγκεκριμένης ημέρας (με προϊόντα)
		public async Task<IEnumerable<Sale>> GetSalesByDateAsync(DateTime date)
		{
			return await _db.Sales
				.Where(s => s.SaleDate.Date == date.Date)
				.Include(s => s.Items)                // Φέρνουμε τα SaleItems
					.ThenInclude(si => si.Product)    // Και τα προϊόντα τους
				.ToListAsync();
		}

		// ✅ Πωλήσεις ανά χρονικό διάστημα (με προϊόντα)
		public async Task<IEnumerable<Sale>> GetSalesByDateRangeAsync(DateTime? fromDate, DateTime? toDate)
		{
			var query = _db.Sales
				.Include(s => s.Items)
					.ThenInclude(si => si.Product)
				.AsQueryable();

			if (fromDate.HasValue)
				query = query.Where(s => s.SaleDate >= fromDate.Value);

			if (toDate.HasValue)
				query = query.Where(s => s.SaleDate <= toDate.Value);

			return await query.ToListAsync();
		}

		// ✅ Best Seller Προϊόντα (με βάση τα SaleItems)
		public async Task<IEnumerable<BestSellerItem>> GetBestSellingProductsAsync(
			int topN,
			int? categoryId = null,
			int? supplierId = null,
			DateTime? fromDate = null,
			DateTime? toDate = null)
		{
			var query = _db.SaleItems
				.Include(si => si.Product)
				.Include(si => si.Sale)
				.AsQueryable();

			if (categoryId.HasValue)
				query = query.Where(si => si.Product.CategoryId == categoryId.Value);

			if (supplierId.HasValue)
				query = query.Where(si => si.Product.SupplierId == supplierId.Value);

			if (fromDate.HasValue)
				query = query.Where(si => si.Sale.SaleDate >= fromDate.Value);

			if (toDate.HasValue)
				query = query.Where(si => si.Sale.SaleDate <= toDate.Value);

			// Ομαδοποιούμε τα προϊόντα και υπολογίζουμε συνολικές πωλήσεις
			var result = await query
				.GroupBy(si => new { si.ProductId, si.Product.Name })
				.Select(g => new BestSellerItem
				{
					ProductId = g.Key.ProductId,
					ProductName = g.Key.Name,
					TotalQuantitySold = g.Sum(si => si.Quantity)
				})
				.OrderByDescending(x => x.TotalQuantitySold)
				.Take(topN)
				.ToListAsync();

			return result;
		}
		//διαγραφή πώλησης
		public async Task DeleteAsync(int saleId)
		{
			var sale = await _db.Sales
				.Include(s => s.Items)
				.FirstOrDefaultAsync(s => s.Id == saleId);

			if (sale != null)
			{
				_db.Sales.Remove(sale);
			}
		}
		public async Task UpdateAsync(Sale sale)
		{
			var existingSale = await _db.Sales
				.Include(s => s.Items)
				.FirstOrDefaultAsync(s => s.Id == sale.Id);

			if (existingSale != null)
			{
				// Καθαρίζουμε τα παλιά SaleItems
				_db.SaleItems.RemoveRange(existingSale.Items);

				// Ενημερώνουμε το Sale με τα νέα SaleItems
				existingSale.Items = sale.Items;
				existingSale.TotalAmount = sale.TotalAmount;
				existingSale.SaleDate = sale.SaleDate;
			}
		}

	}
}
