using HermesPOS.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HermesPOS.Data.Repositories
{
	public interface ISupplierRepository
	{
		Task<IEnumerable<Supplier>> GetAllAsync(); // Επιστρέφει όλους τους προμηθευτές
		Task<Supplier> GetByIdAsync(int id); // Επιστρέφει έναν προμηθευτή με βάση το ID
		Task AddAsync(Supplier supplier); // Προσθέτει έναν νέο προμηθευτή
		Task UpdateAsync(Supplier supplier); // Ενημερώνει έναν υπάρχοντα προμηθευτή
		Task DeleteAsync(int id); // Διαγράφει έναν προμηθευτή με βάση το ID
	}
}
