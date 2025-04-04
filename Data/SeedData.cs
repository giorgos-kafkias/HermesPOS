using HermesPOS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace HermesPOS.Data
{
	public static class SeedData
	{
		public static void Initialize(IServiceProvider serviceProvider)
		{
			using (var context = new ApplicationDbContext(
				serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()))
			{
				Console.WriteLine(" Εκτέλεση SeedData.Initialize()...");
				//Αν υπάρχουν ήδη δεδομένα, δεν κάνουμε τίποτα
				if (context.Products.Any() || context.Categories.Any() || context.Suppliers.Any())
				{
					return;
				}

				// ?? Προσθήκη Κατηγοριών
				var categories = new Category[]
				{
					new Category { Name = "Βαφές" },
					new Category { Name = "Καλλυντικά" },
					new Category { Name = "Σαμπουάν" }
				};
				context.Categories.AddRange(categories);
				context.SaveChanges();

				// ?? Προσθήκη Προμηθευτών
				var suppliers = new Supplier[]
				{
					new Supplier { Name = "Loreal" },
					new Supplier { Name = "Wella" },
					new Supplier { Name = "Farcom" }
				};
				context.Suppliers.AddRange(suppliers);
				context.SaveChanges();

				// ?? Προσθήκη Προϊόντων
				var products = new Product[]
				{
					new Product { Barcode = "123456", Name = "Γάλα 1L", Price = 1.50m, Stock = 50, CategoryId = categories[0].Id, SupplierId = suppliers[0].Id },
					new Product { Barcode = "654321", Name = "Ψωμί", Price = 0.80m, Stock = 3, CategoryId = categories[0].Id, SupplierId = suppliers[0].Id },
					new Product { Barcode = "111222", Name = "Laptop", Price = 999.99m, Stock = 1, CategoryId = categories[1].Id, SupplierId = suppliers[1].Id },
					new Product { Barcode = "333444", Name = "T-Shirt", Price = 15.99m, Stock = 25, CategoryId = categories[2].Id, SupplierId = suppliers[2].Id }
				};
				context.Products.AddRange(products);
				context.SaveChanges();
			}
		}
	}
}
