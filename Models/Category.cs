using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HermesPOS.Models
{
	public class Category
	{
		[Key]
		public int Id { get; set; } // Πρωτεύον κλειδί

		[Required]
		[StringLength(100)]
		public string Name { get; set; } // Όνομα κατηγορίας (υποχρεωτικό, max 100 χαρακτήρες)

		// Σχέση 1 προς πολλά με τα προϊόντα
		public ICollection<Product> Products { get; set; }
	}
}
