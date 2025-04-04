using HermesPOS.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HermesPOS.Data.Repositories
{
	public class SupplierRepository : ISupplierRepository
	{
		private readonly ApplicationDbContext _db;

		public SupplierRepository(ApplicationDbContext db)
		{
			_db = db;
		}

		public async Task<IEnumerable<Supplier>> GetAllAsync()
		{
			return await _db.Suppliers.ToListAsync(); // Επιστρέφει όλους τους προμηθευτές
		}

		public async Task<Supplier> GetByIdAsync(int id)
		{
			return await _db.Suppliers.FindAsync(id); // Επιστρέφει έναν προμηθευτή με βάση το ID
		}

		public async Task AddAsync(Supplier supplier)
		{
			await _db.Suppliers.AddAsync(supplier); // Προσθέτει έναν νέο προμηθευτή
			await _db.SaveChangesAsync();
		}

		public async Task UpdateAsync(Supplier supplier)
		{
			_db.Suppliers.Update(supplier); // Ενημερώνει έναν υπάρχοντα προμηθευτή
			await _db.SaveChangesAsync();
		}

		public async Task DeleteAsync(int id)
		{
			var supplier = await _db.Suppliers.FindAsync(id);
			if (supplier != null)
			{
				_db.Suppliers.Remove(supplier);
				await _db.SaveChangesAsync();
			}
		}

	}
}
