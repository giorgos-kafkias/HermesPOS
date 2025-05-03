using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HermesPOS.Models
{
	public class Sale
	{
		[Key]
		public int Id { get; set; }

		[Required]
		public DateTime SaleDate { get; set; }

		[Required]
		public decimal TotalAmount { get; set; } // ✅ Συνολική αξία πώλησης

		// ✅ Σχέση: Μία πώληση έχει πολλά προϊόντα
		public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
	}
}
