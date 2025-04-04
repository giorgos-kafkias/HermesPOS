using System.Collections.Generic;
using System.Reflection.Emit;
using HermesPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace HermesPOS.Data
{
	/// Διαχειρίζεται την επικοινωνία με τη βάση δεδομένων.
	public class ApplicationDbContext : DbContext
	{
		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
		{
		}

		public DbSet<Product> Products { get; set; }
		public DbSet<Supplier> Suppliers { get; set; }
		public DbSet<Category> Categories { get; set; }
		public DbSet<Sale> Sales { get; set; }


		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Product>()
				.HasOne(p => p.Supplier)
				.WithMany()
				.HasForeignKey(p => p.SupplierId)
				.OnDelete(DeleteBehavior.SetNull); // ❗

			modelBuilder.Entity<Product>()
				.HasOne(p => p.Category)
				.WithMany()
				.HasForeignKey(p => p.CategoryId)
				.OnDelete(DeleteBehavior.SetNull); // ❗

			modelBuilder.Entity<Sale>()
				.HasOne(s => s.Product)
				.WithMany()
				.HasForeignKey(s => s.ProductId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
