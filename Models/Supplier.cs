using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HermesPOS.Models
{
	public class Supplier
	{
		[Key]
		public int Id { get; set; } // Πρωτεύον κλειδί

		[Required]
		[StringLength(100)]
		public string Name { get; set; } // Όνομα προμηθευτή (υποχρεωτικό, max 100 χαρακτήρες)

		[StringLength(200)]
		public string? Address { get; set; } // Διεύθυνση προμηθευτή (προαιρετικό)

		[StringLength(20)]
		public string? Phone { get; set; } // Τηλέφωνο προμηθευτή (προαιρετικό)

		// Σχέση 1 προς πολλά με τα προϊόντα
		public ICollection<Product> Products { get; set; }
	}
}
