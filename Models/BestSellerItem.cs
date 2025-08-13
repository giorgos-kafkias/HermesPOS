namespace HermesPOS.Models
{
	// 🔹 Μοντέλο για αναφορά Best Sellers
	public class BestSellerItem
	{
		public int ProductId { get; set; }             // ID του προϊόντος
		public string ProductName { get; set; }        // Όνομα του προϊόντος
		public int TotalQuantitySold { get; set; }     // Πόσες φορές πουλήθηκε συνολικά
	}
}
