using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HermesPOS.Models
{
	public class Product
	{
		[Key]
		public int Id { get; set; } // Πρωτεύον κλειδί

		[Required]
		[StringLength(50)]
		public string Barcode { get; set; } // Barcode προϊόντος (υποχρεωτικό, max 50 χαρακτήρες)

		[Required]
		[StringLength(100)]
		public string Name { get; set; } // Όνομα προϊόντος (υποχρεωτικό, max 100 χαρακτήρες)

		[Required]
		[Column(TypeName = "decimal(18,2)")]
		public decimal Price { get; set; } // Τιμή προϊόντος (υποχρεωτική, 2 δεκαδικά)

		public decimal? WholesalePrice { get; set; } // Τιμή χονδρικής (null αν δεν έχει)
		[Required]
		public int Stock { get; set; } // Απόθεμα προϊόντος (υποχρεωτικό)

		// Ξένο κλειδί για Προμηθευτή
		public int? SupplierId { get; set; }

		[ForeignKey("SupplierId")]
		public Supplier Supplier { get; set; }

		// Ξένο κλειδί για Κατηγορία
		public int? CategoryId { get; set; }

		[ForeignKey("CategoryId")]
		public Category Category { get; set; }
	}
}
