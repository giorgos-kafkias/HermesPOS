using HermesPOS.Data;
using HermesPOS.Data.Repositories;
using HermesPOS.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.RegularExpressions;


namespace HermesPOS.Services
{
    public class StockReceptionService : IStockReceptionService
    {
        private readonly ApplicationDbContext _db;
        private readonly HttpClient _http;
        private readonly IUnitOfWork _uow;

        public StockReceptionService(ApplicationDbContext db, HttpClient http, IUnitOfWork uow)
        {
            _db = db;
            _http = http;
            _uow = uow;
        }

        public async Task<(bool ok, string message)> PostReceptionAsync(int receptionId)
        {
            var rec = await _db.StockReceptions
                .AsTracking()
                .Include(r => r.Items)
                .SingleOrDefaultAsync(r => r.Id == receptionId);

            if (rec == null) return (false, "Η παραλαβή δεν βρέθηκε.");
            if (rec.Status != ReceptionStatus.Draft) return (false, "Η παραλαβή δεν είναι Draft.");

            var supplierExists = await _db.Suppliers.AnyAsync(s => s.Id == rec.SupplierId);
            if (!supplierExists) return (false, $"Ο προμηθευτής με Id={rec.SupplierId} δεν υπάρχει.");

            if (rec.Items.Any(i => string.IsNullOrWhiteSpace(i.Barcode)))
                return (false, "Υπάρχουν γραμμές χωρίς barcode.");

            var bcs = rec.Items.Select(i => i.Barcode!.Trim()).Distinct().ToList();
            var products = await _db.Products.Where(p => bcs.Contains(p.Barcode)).ToListAsync();
            var missing = bcs.Except(products.Select(p => p.Barcode)).ToList();
            if (missing.Any())
                return (false, "Δεν βρέθηκαν προϊόντα για: " + string.Join(", ", missing));

            var prodByBarcode = products.ToDictionary(p => p.Barcode, p => p);

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // α) ενημέρωση αποθέματος
                foreach (var g in rec.Items.GroupBy(i => prodByBarcode[i.Barcode!].Id))
                {
                    var pid = g.Key;
                    var qty = (int)Math.Round(g.Sum(x => x.Quantity), MidpointRounding.AwayFromZero);
                    var prod = products.First(p => p.Id == pid);
                    prod.Stock += qty;
                }

                // β) SupplierProductMap χωρίς διπλές εγγραφές
                var maps = await _db.SupplierProductMaps
                    .Where(m => m.SupplierId == rec.SupplierId)
                    .Select(m => new { m.SupplierCode, m.ProductId })
                    .ToListAsync();

                var existing = new HashSet<(string code, int pid)>(
                    maps.Select(m => (Norm(m.SupplierCode), m.ProductId)));

                foreach (var item in rec.Items)
                {
                    var product = prodByBarcode[item.Barcode!];
                    var codeKey = Norm(item.SupplierCode);
                    if (codeKey.Length == 0) continue;

                    var tuple = (codeKey, product.Id);
                    if (existing.Add(tuple))
                    {
                        _db.SupplierProductMaps.Add(new SupplierProductMap
                        {
                            SupplierId = rec.SupplierId,
                            SupplierCode = item.SupplierCode ?? string.Empty,
                            ProductId = product.Id
                        });
                    }
                }

                // γ) Posted
                rec.Status = ReceptionStatus.Posted;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return (true, $"Η παραλαβή #{rec.Id} ολοκληρώθηκε.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, "Σφάλμα στο Post: " + ex.GetBaseException().Message);
            }
        }

        public async Task<(bool ok, string message, List<StockReceptionItem> items, int? supplierId, string? mark)>
            FetchFromQrUrlAsync(string qrUrl)
        {
            var items = new List<StockReceptionItem>();
            int? supplierId = null;
            string? mark = null;

            var token = (qrUrl ?? "").Trim();
            if (string.IsNullOrWhiteSpace(token))
                return (false, "Δώσε URL ή token από το QR.", items, null, null);

            // Αν μας έδωσαν μόνο το token, φτιάξε το πλήρες URL
            string url = token.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? token
                : $"https://mydatapi.aade.gr/myDATA/TimologioQR/QRInfo?q={Uri.EscapeDataString(token)}";

            // HTTP fetch
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return (false, $"HTTP {(int)resp.StatusCode}: δεν μπόρεσα να διαβάσω τη σελίδα της ΑΑΔΕ.", items, null, null);

            var html = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(html))
                return (false, "Κενή απάντηση από την ΑΑΔΕ.", items, null, null);

            // Parse HTML
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // MARK (robust)
            mark = doc.GetElementbyId("tmark")?.InnerText?.Trim();

            if (string.IsNullOrWhiteSpace(mark))
            {
                // 1ο fallback: "MARK: 123456..." οπουδήποτε στο HTML
                var m1 = Regex.Match(html, @"MARK\s*[:=]?\s*([0-9]{8,20})", RegexOptions.IgnoreCase);
                if (m1.Success) mark = m1.Groups[1].Value;
            }

            if (string.IsNullOrWhiteSpace(mark))
            {
                // 2ο fallback: σταθερό pseudo-MARK από το υπάρχον 'token' (ΔΕΝ το ξαναδηλώνουμε)
                using var sha1 = SHA1.Create();
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(token));
                mark = "QR-" + Convert.ToHexString(hash).Substring(0, 16);
            }

            var tableByHeading =
                doc.DocumentNode.SelectSingleNode("//h2[contains(normalize-space(.),'Στοιχεία δελτίου διακίνησης')]/following::table[1]")
                ?? doc.DocumentNode.SelectSingleNode("//h3[contains(normalize-space(.),'Στοιχεία δελτίου διακίνησης')]/following::table[1]");

            // Βρες τον πίνακα γραμμών
            HtmlNode? table = doc.GetElementbyId("tableDiakinisis") ?? tableByHeading;

            if (table == null)
            {
                var tables = doc.DocumentNode.SelectNodes("//table") ?? new HtmlNodeCollection(null);
                foreach (var t in tables)
                {
                    var hdr = t.SelectSingleNode(".//tr[th]") ??
                              t.SelectSingleNode(".//thead/tr[1]") ??
                              t.SelectSingleNode(".//tr[1]");
                    var cells = hdr?.SelectNodes("./th|./td")?.ToList();
                    if (cells == null || cells.Count == 0) continue;

                    var norm = cells.Select(c => NormalizeGreek(HtmlEntity.DeEntitize(c.InnerText))).ToList();
                    bool hasDescr = norm.Any(h => h.Contains("ΠΕΡΙΓΡΑΦ"));
                    bool hasQty = norm.Any(h => h.Contains("ΠΟΣΟΤ"));
                    if (hasDescr && hasQty) { table = t; break; }
                }
            }

            if (table == null)
                return (false,
                    "Η σελίδα της ΑΑΔΕ δεν περιέχει γραμμές διακίνησης.\n" +
                    "Δεν είναι δυνατό να γίνει αυτόματη εισαγωγή από QR.",
                    items, supplierId, mark);

            // Πάρε “κεφαλίδες” όπου κι αν είναι
            List<HtmlNode> GetHeaderCells(HtmlNode tbl)
            {
                var headCells = tbl.SelectSingleNode(".//thead/tr[1]")?.SelectNodes("./th|./td")?.ToList();
                if (headCells != null && headCells.Count > 0) return headCells;

                var rowWithTh = tbl.SelectSingleNode(".//tr[th]");
                if (rowWithTh != null) return rowWithTh.SelectNodes("./th|./td")?.ToList() ?? new List<HtmlNode>();

                var firstTr = tbl.SelectSingleNode(".//tbody/tr[1]") ?? tbl.SelectSingleNode(".//tr[1]");
                return firstTr?.SelectNodes("./td|./th")?.ToList() ?? new List<HtmlNode>();
            }

            var headerCells = GetHeaderCells(table);

            int IndexByKeys(List<HtmlNode> cells, params string[] keys)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    var text = NormalizeGreek(HtmlEntity.DeEntitize(cells[i].InnerText));
                    if (keys.Any(k => text.Contains(NormalizeGreek(k))))
                        return i;
                }
                return -1;
            }

            int idxKodikos = IndexByKeys(headerCells,
                "Κωδικός", "Κωδικος", "Κωδικός Είδους", "Κωδ. Είδους", "CODE");
            int idxPerigrafi = IndexByKeys(headerCells,
                "Περιγραφή", "DESCRIPTION", "ITEM");
            int idxPosotita = IndexByKeys(headerCells,
                "Ποσότητα", "ΠΟΣΟΤΗΤΑ", "QTY", "QUANTITY");

            if (idxPerigrafi < 0 || idxPosotita < 0)
                return (false, "Δεν μπόρεσα να εντοπίσω τις στήλες Περιγραφή/Ποσότητα.", items, supplierId, mark);

            var dataRows = table.SelectNodes(".//tbody/tr[position()>1]") ??
                           table.SelectNodes(".//tr[td][position()>1]") ??
                           new HtmlNodeCollection(null);

            foreach (var tr in dataRows)
            {
                var tds = tr.SelectNodes("./td")?.ToList();
                if (tds == null || tds.Count == 0) continue;

                string descr = SafeText(tds, idxPerigrafi);
                string qtyTxt = SafeText(tds, idxPosotita);
                string supplierCode = idxKodikos >= 0 ? SafeText(tds, idxKodikos) : "";

                if (string.IsNullOrWhiteSpace(descr)) continue;
                if (!TryParseDecimal(qtyTxt, out var qty)) qty = 1;

                items.Add(new StockReceptionItem
                {
                    SupplierCode = supplierCode,
                    Description = descr,
                    Quantity = (int)Math.Round(qty, MidpointRounding.AwayFromZero),
                    Barcode = null
                });
            }

            if (items.Count == 0)
                return (false, "Δε βρέθηκαν γραμμές στο παραστατικό.", items, supplierId, mark);

            return (true, $"MARK: {mark}", items, supplierId, mark);
        }

        public async Task<int> AutoMapBarcodesAsync(int supplierId, IEnumerable<StockReceptionItem> items)
        {
            if (supplierId <= 0 || items == null) return 0;

            var list = items.ToList();
            int filled = 0;

            var targetCodes = list
                .Where(i => string.IsNullOrWhiteSpace(i.Barcode) && !string.IsNullOrWhiteSpace(i.SupplierCode))
                .Select(i => Norm(i.SupplierCode))
                .Where(k => k.Length > 0)
                .Distinct()
                .ToList();

            if (targetCodes.Count > 0)
            {
                var maps = await _db.SupplierProductMaps
                    .Where(m => m.SupplierId == supplierId)
                    .Select(m => new { m.SupplierCode, m.ProductId })
                    .ToListAsync();

                var productIdsByCode = maps
                    .GroupBy(m => Norm(m.SupplierCode))
                    .Where(g => g.Key.Length > 0 && g.Select(x => x.ProductId).Distinct().Count() == 1)
                    .ToDictionary(g => g.Key, g => g.First().ProductId);

                var productIds = productIdsByCode
                    .Where(kv => targetCodes.Contains(kv.Key))
                    .Select(kv => kv.Value)
                    .Distinct()
                    .ToList();

                if (productIds.Count > 0)
                {
                    var products = await _db.Products
                        .Where(p => productIds.Contains(p.Id) && !string.IsNullOrWhiteSpace(p.Barcode))
                        .Select(p => new { p.Id, p.Barcode })
                        .ToListAsync();

                    var barcodeByProductId = products.ToDictionary(p => p.Id, p => p.Barcode!);

                    foreach (var it in list)
                    {
                        if (!string.IsNullOrWhiteSpace(it.Barcode)) continue;
                        var codeKey = Norm(it.SupplierCode);
                        if (codeKey.Length == 0) continue;

                        if (productIdsByCode.TryGetValue(codeKey, out var pid) &&
                            barcodeByProductId.TryGetValue(pid, out var bc) &&
                            !string.IsNullOrWhiteSpace(bc))
                        {
                            it.Barcode = bc;
                            filled++;
                        }
                    }
                }
            }

            return filled;
        }

        public async Task<List<(StockReceptionItem item, string barcode, string productName)>> SuggestBarcodesAsync(
            int supplierId, IEnumerable<StockReceptionItem> items)
        {
            var result = new List<(StockReceptionItem, string, string)>();
            if (supplierId <= 0 || items == null) return result;

            var list = items.Where(i => string.IsNullOrWhiteSpace(i.Barcode)).ToList();
            if (list.Count == 0) return result;

            static string Key(string? s) => NormalizeGreek(GreekLatinFold(s ?? string.Empty));

            var productsForSupplier = await _db.Products
                .Where(p => p.SupplierId == supplierId &&
                            !string.IsNullOrWhiteSpace(p.Name) &&
                            !string.IsNullOrWhiteSpace(p.Barcode))
                .Select(p => new { p.Name, p.Barcode })
                .ToListAsync();

            var byName = productsForSupplier
                .GroupBy(x => Key(x.Name))
                .Where(g => g.Key != string.Empty && g.Count() == 1)
                .ToDictionary(
                    g => g.Key,
                    g => (Name: g.First().Name, Barcode: g.First().Barcode!),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var it in list)
            {
                var k = Key(it.Description);
                if (k.Length == 0) continue;

                if (byName.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v.Barcode))
                {
                    result.Add((it, v.Barcode, v.Name));
                }
            }

            return result;
        }

        private static string NormalizeGreek(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            s = HtmlEntity.DeEntitize(s).Replace('\u00A0', ' ');
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();

            var formD = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            var cleaned = sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();

            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[^A-Z0-9Α-Ω ]", "");
            return cleaned.Trim();
        }

        private static string Norm(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var cleaned = System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s+", " ");
            return cleaned.ToUpperInvariant();
        }

        static string GreekLatinFold(string s)
        {
            return s
                .Replace('Α', 'A').Replace('Β', 'B').Replace('Ε', 'E').Replace('Ζ', 'Z')
                .Replace('Η', 'H').Replace('Ι', 'I').Replace('Κ', 'K').Replace('Μ', 'M')
                .Replace('Ν', 'N').Replace('Ο', 'O').Replace('Ρ', 'P').Replace('Τ', 'T')
                .Replace('Υ', 'Y').Replace('Χ', 'X')
                .Replace('ά', 'a').Replace('έ', 'e').Replace('ί', 'i').Replace('ό', 'o')
                .Replace('ή', 'h').Replace('ύ', 'y').Replace('ϊ', 'i').Replace('ϋ', 'y');
        }

        private static string SafeText(List<HtmlNode> tds, int idx)
        {
            if (idx < 0 || idx >= tds.Count) return "";
            var s = HtmlEntity.DeEntitize(tds[idx].InnerText ?? "");
            s = s.Replace('\u00A0', ' ');
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        private static bool TryParseDecimal(string s, out decimal value)
        {
            s = (s ?? "").Trim();
            return decimal.TryParse(s, NumberStyles.Any, new CultureInfo("el-GR"), out value) ||
                   decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
    }
}
