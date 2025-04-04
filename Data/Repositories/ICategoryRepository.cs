using HermesPOS.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HermesPOS.Data.Repositories
{
	public interface ICategoryRepository
	{
		Task<IEnumerable<Category>> GetAllAsync(); // Επιστρέφει όλες τις κατηγορίες
		Task<Category> GetByIdAsync(int id); // Επιστρέφει μια κατηγορία με βάση το ID
		Task AddAsync(Category category); // Προσθέτει μια νέα κατηγορία
		Task UpdateAsync(Category category); // Ενημερώνει μια υπάρχουσα κατηγορία
		Task DeleteAsync(int id); // Διαγράφει μια κατηγορία με βάση το ID
	}
}
