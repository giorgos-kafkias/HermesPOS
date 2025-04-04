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

		// ✅ Προσθήκη νέας πώλησης
		public async Task AddSaleAsync(Sale sale)
		{
			await _db.Sales.AddAsync(sale);
			//await _db.SaveChangesAsync();
		}

		public async Task<IEnumerable<Sale>> GetSalesByDateAsync(DateTime date)
		{
			return await _db.Sales
				.Where(s => s.SaleDate.Date == date.Date)
				.Include(s => s.Product)
				.ToListAsync();
		}

		public async Task<IEnumerable<Sale>> GetBestSellingProductsAsync(int topN, int? categoryId = null, int? supplierId = null, DateTime? fromDate = null, DateTime? toDate = null)
		{
			var query = _db.Sales
				.Include(s => s.Product)
				.AsQueryable();

			if (categoryId.HasValue)
				query = query.Where(s => s.Product.CategoryId == categoryId.Value);

			if (supplierId.HasValue)
				query = query.Where(s => s.Product.SupplierId == supplierId.Value);

			if (fromDate.HasValue)
				query = query.Where(s => s.SaleDate >= fromDate.Value);

			if (toDate.HasValue)
				query = query.Where(s => s.SaleDate <= toDate.Value);

			var result = await query
				.GroupBy(s => new { s.ProductId, s.Product.Name })
				.Select(g => new Sale
				{
					ProductId = g.Key.ProductId,
					Product = new Product { Name = g.Key.Name }, // Δημιουργούμε νέο αντικείμενο Product
					Quantity = g.Sum(s => s.Quantity), // Συνολική ποσότητα πωλήσεων
					SaleDate = DateTime.Now // Δεν έχει σημασία η ημερομηνία για τα bestsellers
				})
				.OrderByDescending(s => s.Quantity)
				.Take(topN)
				.ToListAsync();

			return result;
		}
		public async Task<IEnumerable<Sale>> GetSalesByDateRangeAsync(DateTime? fromDate, DateTime? toDate)
		{
			var query = _db.Sales
				.Include(s => s.Product)
				.AsQueryable();

			if (fromDate.HasValue)
				query = query.Where(s => s.SaleDate >= fromDate.Value);

			if (toDate.HasValue)
				query = query.Where(s => s.SaleDate <= toDate.Value);

			return await query.ToListAsync();
		}

	}
}
