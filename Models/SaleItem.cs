using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HermesPOS.Models
{
	public class SaleItem
	{
		[Key]
		public int Id { get; set; }

		[Required]
		public int SaleId { get; set; } // Ξένο κλειδί προς Sale
		[ForeignKey("SaleId")]
		public Sale Sale { get; set; }

		[Required]
		public int ProductId { get; set; } // Ξένο κλειδί προς Product
		[ForeignKey("ProductId")]
		public Product Product { get; set; }

		[Required]
		public int Quantity { get; set; }

		[Required]
		public decimal Price { get; set; } // Τιμή του προϊόντος εκείνη τη στιγμή
	}
}
