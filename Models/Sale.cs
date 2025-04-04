using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HermesPOS.Models
{
	public class Sale
	{
		[Key]
		public int Id { get; set; } // Πρωτεύον κλειδί

		[Required]
		public int ProductId { get; set; } // Ξένο κλειδί για το προϊόν
		[ForeignKey("ProductId")]
		public Product Product { get; set; }

		[Required]
		public int Quantity { get; set; } // Ποσότητα πώλησης

		[Required]
		public decimal Price { get; set; } // Τιμή κατά την πώληση

		[Required]
		public DateTime SaleDate { get; set; } // Ημερομηνία πώλησης
	}
}
