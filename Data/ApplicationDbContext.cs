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
		public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<StockReception> StockReceptions { get; set; }
        public DbSet<StockReceptionItem> StockReceptionItems { get; set; }
        public DbSet<SupplierProductMap> SupplierProductMaps { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Product>()
				.HasOne(p => p.Supplier)
				.WithMany(s => s.Products)
                .HasForeignKey(p => p.SupplierId)
				.OnDelete(DeleteBehavior.SetNull); // ❗

			modelBuilder.Entity<Product>()
				.HasOne(p => p.Category)
				.WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
				.OnDelete(DeleteBehavior.SetNull); // ❗

			modelBuilder.Entity<SaleItem>()
				.HasOne(si => si.Sale)
				.WithMany(s => s.Items)
				.HasForeignKey(si => si.SaleId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<SaleItem>()
				.HasOne(si => si.Product)
                .WithMany(p => p.SaleItems)        // 👈 σύνδεση με τη συλλογή στο Product
				.HasForeignKey(si => si.ProductId)
				.OnDelete(DeleteBehavior.Cascade); // 👈 ενεργοποίηση cascade

            modelBuilder.Entity<Product>()
				.Property(p => p.IsActive)
				.HasDefaultValue(true); // ✅ default στον SQL πίνακα

            // ===== Precision για decimal =====
            modelBuilder.Entity<Product>()
                .Property(p => p.WholesalePrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Sale>()
                .Property(s => s.TotalAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<SaleItem>()
                .Property(i => i.Price)
                .HasPrecision(18, 2);
        }
	}
}
