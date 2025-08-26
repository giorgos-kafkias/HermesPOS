using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using HermesPOS.Data;
using HermesPOS.Data.Repositories;
using HermesPOS.ViewModels;
using HermesPOS.Views;

namespace HermesPOS
{
	public static class Program
	{
		[STAThread]
		public static void Main()
		{
			var host = CreateHostBuilder().Build(); // Δημιουργία του Host για Dependency Injection

			// 🔹 Εκτέλεση αρχικοποίησης της βάσης δεδομένων
			using (var scope = host.Services.CreateScope())
			{
				var services = scope.ServiceProvider;
				try
				{
					var context = services.GetRequiredService<ApplicationDbContext>();
					context.Database.Migrate(); // Εφαρμογή των Migrations αν δεν έχουν γίνει
					SeedData.Initialize(services); // Κλήση της μεθόδου SeedData για αρχικοποίηση δεδομένων
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Σφάλμα κατά την αρχικοποίηση της βάσης: {ex.Message}");
				}
			}
			var app = new Application();
			var mainWindow = host.Services.GetRequiredService<LoginWindow>(); // Ανάκτηση του κύριου παραθύρου

			app.Run(mainWindow); // Εκκίνηση της εφαρμογής WPF
			host.Dispose(); // Διασφαλίζουμε ότι ο Host τερματίζεται σωστά
		}

		private static IHostBuilder CreateHostBuilder()
		{
			return Host.CreateDefaultBuilder()
				.ConfigureServices((context, services) =>
				{
					// 🔹 Προσθήκη της βάσης δεδομένων από το appsettings.json
					services.AddDbContext<ApplicationDbContext>(options =>
						options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")), ServiceLifetime.Scoped);

                    // Παίρνουμε το connection string από το appsettings.json (κρυπτογραφημένο)
                    var encryptedConnectionString = context.Configuration.GetConnectionString("DefaultConnection");
                    // Το κάνουμε decrypt με τον helper
                    var decryptedConnectionString = CryptoHelper.Decrypt(encryptedConnectionString);

                    // Το περνάμε στον DbContext
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseSqlServer(decryptedConnectionString),
                        ServiceLifetime.Scoped);

                    // 🔹 Προσθήκη των Repositories
                    services.AddScoped<IProductRepository, ProductRepository>();
					services.AddScoped<ICategoryRepository, CategoryRepository>();
					services.AddScoped<ISupplierRepository, SupplierRepository>();
					services.AddScoped<ISaleRepository, SaleRepository>();
					services.AddScoped<IUnitOfWork, UnitOfWork>();

					// 🔹 Προσθήκη των ViewModels
					services.AddTransient<MainViewModel>();
					services.AddTransient<LowStockProductsViewModel>();
					services.AddTransient<ReceiveStockViewModel>();
					services.AddTransient<AddProductViewModel>();
					services.AddTransient<AdminPanelViewModel>();
					services.AddTransient<EditProductViewModel>();
					services.AddTransient<EditCategoryOrSupplierViewModel>();
					services.AddTransient<BestsellerViewModel>();
					services.AddTransient<SalesReportViewModel>();
					services.AddTransient<EditSaleViewModel>();

					// 🔹 Προσθήκη των Views
					services.AddScoped<MainWindow>();
					services.AddScoped<LowStockProductsTab>();
					services.AddScoped<ReceiveStockTab>();
					services.AddScoped<AddProductView>();
					services.AddScoped<AdminLoginWindow>();
					services.AddScoped<AdminPanelWindow>();
					services.AddScoped<LoginWindow>();
					services.AddScoped<EditProductWindow>();
					services.AddScoped<EditCategoryOrSupplierView>();
					services.AddScoped< EditSaleWindow>();
				});
		}	
	}
}
