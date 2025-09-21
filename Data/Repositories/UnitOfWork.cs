using HermesPOS.Data;
using HermesPOS.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace HermesPOS.Data.Repositories
{
	public class UnitOfWork : IUnitOfWork
	{
		private readonly ApplicationDbContext _db;

		public IProductRepository Products { get; }
		public ICategoryRepository Categories { get; }
		public ISupplierRepository Suppliers { get; }
		public ISaleRepository Sales { get; }
        public IStockReceptionRepository StockReceptions { get; }


        public UnitOfWork(ApplicationDbContext db,
						  IProductRepository productRepository,
						  ICategoryRepository categoryRepository,
						  ISupplierRepository supplierRepository,
						  ISaleRepository saleRepository, 
						  IStockReceptionRepository stockReceptionRepository)
		{
			_db = db;
			Products = productRepository;
			Categories = categoryRepository;
			Suppliers = supplierRepository;
			Sales=saleRepository;
            StockReceptions = stockReceptionRepository;
        }

		public async Task<int> CompleteAsync()
		{
			return await _db.SaveChangesAsync(); // Αποθηκεύει τις αλλαγές στη βάση
		}

		public void Dispose()
		{
			_db.Dispose(); // Αποδεσμεύει τους πόρους της βάσης δεδομένων
		}
        public async Task<(bool Ok, string Message)> PostReceptionAsync(int receptionId)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            // Φόρτωση draft + γραμμές
            var rec = await _db.StockReceptions
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == receptionId);

            if (rec == null)
                return (false, "Το draft δεν βρέθηκε.");
            if (rec.Status != ReceptionStatus.Draft)
                return (false, "Η παραλαβή δεν είναι Draft (ίσως έχει ήδη ολοκληρωθεί).");

            var items = rec.Items.ToList();
            if (items.Count == 0)
                return (false, "Δεν υπάρχουν γραμμές για Post.");

            // A) Λείπουν barcodes;
            var missing = items
                .Select((it, i) => new { it, i })
                .Where(x => string.IsNullOrWhiteSpace(x.it.Barcode))
                .Select(x => x.i + 1)
                .ToList();

            if (missing.Any())
                return (false, "Λείπουν barcodes στις γραμμές: " + string.Join(", ", missing));

            // B) Διπλά barcodes με διαφορετικούς SupplierCodes;
            var dupDiffSup = items
                .GroupBy(i => i.Barcode!.Trim())
                .Where(g => g.Select(x => x.SupplierCode ?? "").Distinct().Count() > 1)
                .ToList();

            if (dupDiffSup.Any())
            {
                var list = string.Join(", ", dupDiffSup.Select(g => g.Key));
                return (false, "Διπλά barcodes με διαφορετικούς κωδικούς προμηθευτή: " + list);
            }

            // C) Υπάρχουν προϊόντα για όλα τα barcodes;
            var barcodes = items.Select(i => i.Barcode!.Trim()).Distinct().ToList();
            var products = await _db.Products.Where(p => barcodes.Contains(p.Barcode)).ToListAsync();
            var found = products.Select(p => p.Barcode).ToHashSet();
            var missingProducts = barcodes.Where(bc => !found.Contains(bc)).ToList();

            if (missingProducts.Any())
                return (false, "Δεν βρέθηκαν προϊόντα για τα barcodes: " + string.Join(", ", missingProducts));

            var prodByBarcode = products.ToDictionary(p => p.Barcode);

            // D) Δένουμε τα items με ProductId και (αν λείπει) γράφουμε mapping Supplier→Code→Product
            foreach (var it in items)
            {
                var prod = prodByBarcode[it.Barcode!.Trim()];
                it.ProductId = prod.Id;

                if (!string.IsNullOrWhiteSpace(it.SupplierCode))
                {
                    bool mapExists = await _db.SupplierProductMaps.AnyAsync(m =>
                        m.SupplierId == rec.SupplierId &&
                        m.SupplierCode == it.SupplierCode &&
                        m.ProductId == prod.Id);

                    if (!mapExists)
                    {
                        _db.SupplierProductMaps.Add(new SupplierProductMap
                        {
                            SupplierId = rec.SupplierId,
                            SupplierCode = it.SupplierCode!,
                            ProductId = prod.Id
                        });
                    }
                }
            }

            // E) Ενημέρωση αποθέματος (ομαδοποίηση ανά προϊόν)
            var deltas = items
                .GroupBy(i => prodByBarcode[i.Barcode!.Trim()].Id)
                .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToList();

            foreach (var d in deltas)
            {
                var p = products.First(x => x.Id == d.ProductId);
                // Αν το Stock σου είναι int:
                p.Stock += (int)d.Qty;
                // Αν είναι decimal, άλλαξέ το σε: p.Stock += d.Qty;
            }

            // F) Μαρκάρουμε ως Posted
            rec.Status = ReceptionStatus.Posted;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, $"Η παραλαβή #{rec.Id} ολοκληρώθηκε. Ενημερώθηκαν {deltas.Count} προϊόντα.");
        }
    }
}
