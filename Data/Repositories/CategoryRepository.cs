using HermesPOS.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HermesPOS.Data.Repositories
{
	public class CategoryRepository : ICategoryRepository
	{
		private readonly ApplicationDbContext _db;

		public CategoryRepository(ApplicationDbContext db)
		{
			_db = db;
		}

		public async Task<IEnumerable<Category>> GetAllAsync()
		{
			return await _db.Categories.ToListAsync(); // Επιστρέφει όλες τις κατηγορίες
		}

		public async Task<Category> GetByIdAsync(int id)
		{
			return await _db.Categories.FindAsync(id); // Επιστρέφει μια κατηγορία με βάση το ID
		}

		public async Task AddAsync(Category category)
		{
			await _db.Categories.AddAsync(category); // Προσθέτει μια νέα κατηγορία
			await _db.SaveChangesAsync();
		}

		public async Task UpdateAsync(Category category)
		{
			_db.Categories.Update(category); // Ενημερώνει μια υπάρχουσα κατηγορία
			await _db.SaveChangesAsync();
		}

		public async Task DeleteAsync(int id)
		{
			var category = await _db.Categories.FindAsync(id);
			if (category != null)
			{
				_db.Categories.Remove(category); // Διαγράφει μια κατηγορία με βάση το ID
				await _db.SaveChangesAsync();
			}
		}
	}
}
