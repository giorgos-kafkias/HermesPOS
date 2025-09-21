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
            // 1) Φόρτωσε το draft ως tracked
            var rec = await _db.StockReceptions
                .AsTracking()
                .Include(r => r.Items)
                .SingleOrDefaultAsync(r => r.Id == receptionId);

            if (rec == null) return (false, "Η παραλαβή δεν βρέθηκε.");
            if (rec.Status != ReceptionStatus.Draft) return (false, "Η παραλαβή δεν είναι Draft.");

            // 2) Έγκυρος προμηθευτής
            var supplierExists = await _db.Suppliers.AnyAsync(s => s.Id == rec.SupplierId);
            if (!supplierExists) return (false, $"Ο προμηθευτής με Id={rec.SupplierId} δεν υπάρχει.");

            // 3) Όλες οι γραμμές να έχουν barcode
            if (rec.Items.Any(i => string.IsNullOrWhiteSpace(i.Barcode)))
                return (false, "Υπάρχουν γραμμές χωρίς barcode.");

            // 4) Resolve προϊόντων από barcodes
            var bcs = rec.Items.Select(i => i.Barcode!.Trim()).Distinct().ToList();
            var products = await _db.Products.Where(p => bcs.Contains(p.Barcode)).ToListAsync();
            var missing = bcs.Except(products.Select(p => p.Barcode)).ToList();
            if (missing.Any())
                return (false, "Δεν βρέθηκαν προϊόντα για: " + string.Join(", ", missing));

            var prodByBarcode = products.ToDictionary(p => p.Barcode, p => p);

            // 5) Συναλλαγή
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // α) ενημέρωση αποθέματος
                foreach (var g in rec.Items.GroupBy(i => prodByBarcode[i.Barcode!].Id))
                {
                    var pid = g.Key;
                    // Quantity είναι decimal -> γύρνα το σε int (στρογγυλοποίηση)
                    var qty = (int)Math.Round(g.Sum(x => x.Quantity), MidpointRounding.AwayFromZero);
                    var prod = products.First(p => p.Id == pid);
                    prod.Stock += qty;  // Stock είναι int
                }

                // β) SupplierProductMap (όσα λείπουν)
                var maps = await _db.SupplierProductMaps
                    .Where(m => m.SupplierId == rec.SupplierId)
                    .ToListAsync();

                foreach (var item in rec.Items)
                {
                    var product = prodByBarcode[item.Barcode!];
                    bool existsMap = maps.Any(m =>
                        m.SupplierId == rec.SupplierId &&
                        m.SupplierCode == (item.SupplierCode ?? string.Empty) &&
                        m.ProductId == product.Id);
                    if (!existsMap)
                    {
                        _db.SupplierProductMaps.Add(new SupplierProductMap
                        {
                            SupplierId = rec.SupplierId,
                            SupplierCode = item.SupplierCode ?? string.Empty,
                            ProductId = product.Id
                        });
                    }
                }

                // γ) Μαρκάρουμε Posted
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

        public async Task<(bool ok, string message, List<StockReceptionItem> items, int? supplierId)>
            FetchFromQrUrlAsync(string? urlOrToken)
        {
            var items = new List<StockReceptionItem>();
            int? supplierId = null;

            var token = (urlOrToken ?? "").Trim();
            if (string.IsNullOrWhiteSpace(token))
                return (false, "Δώσε URL ή token από το QR.", items, null);

            // Αν μας έδωσαν μόνο το token, φτιάξε το πλήρες URL
            string url = token.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? token
                : $"https://mydatapi.aade.gr/myDATA/TimologioQR/QRInfo?q={Uri.EscapeDataString(token)}";

            // HTTP fetch
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return (false, $"HTTP {(int)resp.StatusCode}: δεν μπόρεσα να διαβάσω τη σελίδα της ΑΑΔΕ.", items, null);

            var html = await resp.Content.ReadAsStringAsync();
            System.IO.File.WriteAllText("last_aade.html", html, Encoding.UTF8); // debug
            if (string.IsNullOrWhiteSpace(html))
                return (false, "Κενή απάντηση από την ΑΑΔΕ.", items, null);

            // Parse HTML
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // MARK + Προμηθευτής
            string? mark = doc.GetElementbyId("tmark")?.InnerText?.Trim();
            string? supplierName = doc.GetElementbyId("bname")?.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                var all = await _uow.Suppliers.GetAllAsync();
                var sup = all.FirstOrDefault(s =>
                    string.Equals(s.Name?.Trim(), supplierName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (sup != null) supplierId = sup.Id;
            }

            // Βρες τον πίνακα γραμμών
            HtmlNode? table = doc.GetElementbyId("tableDiakinisis");
            if (table == null)
            {
                // εναλλακτικό: βρες table με κεφαλίδες “Κωδικός/Περιγραφή/Ποσότητα”
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
                return (false, "Δεν βρέθηκε πίνακας γραμμών στο HTML της ΑΑΔΕ. Ίσως άλλαξε η μορφή σελίδας.", items, supplierId);

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

            int idxKodikos = IndexByKeys(headerCells, "Κωδικός", "Κωδικός Είδους", "Κωδ.");
            int idxPerigrafi = IndexByKeys(headerCells, "Περιγραφή");
            int idxPosotita = IndexByKeys(headerCells, "Ποσότητα");

            // Αν δεν βρέθηκε "Ποσότητα", μάντεψε από τα πρώτα rows ποια στήλη είναι κυρίως αριθμητική
            if (idxPosotita < 0 && headerCells.Count > 0)
            {
                var dataRowsGuess = table.SelectNodes(".//tbody/tr[position()>1]") ??
                                    table.SelectNodes(".//tr[td][position()>1]") ??
                                    new HtmlNodeCollection(null);

                int rowsToCheck = Math.Min(6, dataRowsGuess.Count);
                int bestIdx = -1, bestScore = -1;

                for (int c = 0; c < headerCells.Count; c++)
                {
                    int score = 0;
                    for (int r = 0; r < rowsToCheck; r++)
                    {
                        var tds = dataRowsGuess[r].SelectNodes("./td")?.ToList();
                        if (tds == null || c >= tds.Count) continue;

                        var txt = HtmlEntity.DeEntitize(tds[c].InnerText).Trim();
                        if (TryParseDecimal(txt, out _)) score++;
                    }
                    if (score > bestScore) { bestScore = score; bestIdx = c; }
                }
                if (bestScore >= 2) idxPosotita = bestIdx;
            }

            // Απλοί fallbacks
            if (idxPerigrafi < 0 && headerCells.Count >= 3) idxPerigrafi = 2;
            if (idxKodikos < 0 && headerCells.Count >= 2) idxKodikos = 1;

            if (idxPerigrafi < 0 || idxPosotita < 0)
                return (false, "Δεν μπόρεσα να εντοπίσω τις στήλες Περιγραφή/Ποσότητα.", items, supplierId);

            // Δεδομένα
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
                return (false, "Δε βρέθηκαν γραμμές στο παραστατικό.", items, supplierId);

            var info = !string.IsNullOrWhiteSpace(mark) ? $"MARK: {mark}" : "";
            return (true, info, items, supplierId);
        }


        // -------- helpers --------
        private static string NormalizeGreek(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            s = HtmlAgilityPack.HtmlEntity.DeEntitize(s);
            s = s.Replace('\u00A0', ' ');
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();

            var formD = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }

        private static HtmlAgilityPack.HtmlNode? FindDataTable(HtmlAgilityPack.HtmlDocument doc)
        {
            var tables = doc.DocumentNode.SelectNodes("//table");
            if (tables == null) return null;

            foreach (var t in tables)
            {
                // Πάρε headers είτε είναι σε <thead><th>, είτε στο 1ο <tr> με <th> ή <td>
                var headerCells =
                    t.SelectNodes(".//thead//th")
                    ?? t.SelectNodes(".//tr[th][1]//th")
                    ?? t.SelectNodes(".//tr[1]//th")
                    ?? t.SelectNodes(".//tr[1]//td");

                if (headerCells == null || headerCells.Count == 0)
                    continue;

                var headers = headerCells
                    .Select(h => NormalizeGreek(HtmlAgilityPack.HtmlEntity.DeEntitize(h.InnerText)))
                    .ToList();

                // Υποστήριξε και αγγλικά
                bool hasDescr = headers.Any(h => h.Contains("ΠΕΡΙΓΡΑΦ") || h.Contains("DESCRIPTION"));
                bool hasQty = headers.Any(h => h.Contains("ΠΟΣΟΤ") || h.Contains("QUANTITY"));
                bool hasCode = headers.Any(h => h.Contains("ΚΩΔΙΚ") || h.Contains("CODE"));

                // Συνήθως αυτά τα δύο φτάνουν
                if (hasDescr && hasQty)
                    return t;

                // fallback: πίνακας με 5+ στήλες και κελιά με αριθμούς
                var firstDataRow = t.SelectSingleNode(".//tr[td]");
                var dataTds = firstDataRow?.SelectNodes("./td");
                if (dataTds != null && dataTds.Count >= 5)
                {
                    // αν βρούμε αριθμητική στήλη (quantity-like) + “λογοτεχνικό” κελί (περιγραφή)
                    bool anyNumeric = dataTds.Any(td =>
                    {
                        var s = HtmlAgilityPack.HtmlEntity.DeEntitize(td.InnerText).Trim()
                                .Replace('\u00A0', ' ');
                        return decimal.TryParse(s, NumberStyles.Any, new CultureInfo("el-GR"), out _)
                            || decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
                    });

                    bool anyTexty = dataTds.Any(td =>
                    {
                        var s = NormalizeGreek(HtmlAgilityPack.HtmlEntity.DeEntitize(td.InnerText));
                        return s.Length > 3 && s.Any(char.IsLetter);
                    });

                    if (anyNumeric && anyTexty)
                        return t;
                }
            }

            return null;
        }


        private static int FindHeaderIndexByKey(IEnumerable<HtmlAgilityPack.HtmlNode> ths, params string[] keys)
        {
            var list = ths.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var text = NormalizeGreek(HtmlAgilityPack.HtmlEntity.DeEntitize(list[i].InnerText));
                foreach (var key in keys)
                {
                    var nKey = NormalizeGreek(key);
                    if (text.Contains(nKey))
                        return i;

                    // Αγγλικές εναλλακτικές
                    if (nKey.Contains("ΠΕΡΙΓΡΑΦ") && (text.Contains("DESCRIPTION") || text.Contains("ITEM")))
                        return i;
                    if (nKey.Contains("ΠΟΣΟΤ") && (text.Contains("QUANTITY") || text.Contains("QTY")))
                        return i;
                    if (nKey.Contains("ΚΩΔΙΚ") && (text.Contains("CODE") || text.Contains("ITEM CODE")))
                        return i;
                }
            }
            return -1;
        }

        private static string SafeText(List<HtmlAgilityPack.HtmlNode> tds, int idx)
        {
            if (idx < 0 || idx >= tds.Count) return "";
            var s = HtmlAgilityPack.HtmlEntity.DeEntitize(tds[idx].InnerText ?? "");
            s = s.Replace('\u00A0', ' ');
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }
        private static string? ExtractCellAfterLabel(HtmlDocument doc, string labelContains)
        {
            if (doc?.DocumentNode == null) return null;  
            var rows = doc.DocumentNode.SelectNodes("//tr[td and td[2]]"); 
            if (rows == null) return null;
            foreach (var tr in rows)
            { var first = tr.SelectSingleNode("./td[1]"); 
                if (first == null) continue; var firstText = HtmlEntity.DeEntitize(first.InnerText ?? string.Empty).Trim();
                 if (firstText.IndexOf(labelContains, StringComparison.OrdinalIgnoreCase) >= 0) { var val = tr.SelectSingleNode("./td[2]")?.InnerText?.Trim() ?? string.Empty; 
                    return HtmlEntity.DeEntitize(val); } } return null; 
        }
        private static bool TryParseDecimal(string s, out decimal value)
        {
            s = (s ?? "").Trim(); 
            return decimal.TryParse(s, NumberStyles.Any, new 
                CultureInfo("el-GR"), out value) || decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value); }
        }

}

