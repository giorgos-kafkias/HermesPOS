using HermesPOS.Data;
using HermesPOS.Data.Repositories;
using HermesPOS.Services;
using HermesPOS.ViewModels;
using HermesPOS.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Windows;

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
                    var conn = context.Configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Missing DefaultConnection");

                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseSqlServer(conn),
                        ServiceLifetime.Scoped);
                    // 🔹 Προσθήκη των Repositories
                    services.AddScoped<IProductRepository, ProductRepository>();
					services.AddScoped<ICategoryRepository, CategoryRepository>();
					services.AddScoped<ISupplierRepository, SupplierRepository>();
					services.AddScoped<ISaleRepository, SaleRepository>();
                    services.AddScoped<IStockReceptionRepository, StockReceptionRepository>();
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
                    services.AddSingleton<QrReceptionViewModel>();

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
                    services.AddScoped<QrReceptionService>();
                });
		}	
	}
}
