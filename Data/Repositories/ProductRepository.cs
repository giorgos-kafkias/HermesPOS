using HermesPOS.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace HermesPOS.Data.Repositories
{
	public class ProductRepository : IProductRepository
	{
		private readonly ApplicationDbContext _db;
		internal DbSet<Product> dbSet;

		public ProductRepository(ApplicationDbContext db)
		{
			_db = db;
			this.dbSet = _db.Set<Product>();
		}

		public async Task<List<Product>> GetAllAsync(Expression<Func<Product, bool>>? filter, string? includeProperties = null)
		{
			IQueryable<Product> query = dbSet/*.AsNoTracking()*/; // ✅ Προσθήκη AsNoTracking();

			if (filter != null)
			{
				query = query.Where(filter);
			}

			if (!string.IsNullOrEmpty(includeProperties))
			{
				foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
				{
					query = query.Include(includeProp);
				}
			}

			return await query.ToListAsync();
		}


		public async Task<Product> GetByIdAsync(int id)
		{
			return await _db.Products.FindAsync(id);
		}

		public async Task<Product> GetByBarcodeAsync(string barcode)
		{
			return await _db.Products.FirstOrDefaultAsync(p => p.Barcode == barcode);
		}

		public async Task AddAsync(Product product)
		{
			await _db.Products.AddAsync(product);
			//await _db.SaveChangesAsync();
		}

		public async Task UpdateAsync(Product product)
		{
			_db.Products.Update(product);
			//await _db.SaveChangesAsync();
		}

		public async Task DeleteAsync(int id)
		{
			var product = await _db.Products.FindAsync(id);
			if (product != null)
			{
				_db.Products.Remove(product);
				//await _db.SaveChangesAsync();
			}
		}
	}
}
