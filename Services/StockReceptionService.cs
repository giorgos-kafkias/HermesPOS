using HermesPOS.Data;
using HermesPOS.Data.Repositories;
using HermesPOS.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;


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

            // 👉 ΝΕΟ: SBZ
            if (qrUrl.Contains("api.sbz.gr", StringComparison.OrdinalIgnoreCase))
            {
                return await FetchFromSbzAsync(qrUrl);
            }
            // ✅ Αν είναι e-invoicing.gr → διάβασε PDF
            if (qrUrl.Contains("e-invoicing.gr", StringComparison.OrdinalIgnoreCase))
            {
                return await FetchFromEInvoicingPdfAsync(qrUrl);
            }

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
        private async Task<(bool ok, string message, List<StockReceptionItem> items, int? supplierId, string? mark)>
     FetchFromSbzAsync(string url)
        {
            var items = new List<StockReceptionItem>();
            int? supplierId = null;
            string? mark = null;

            // 1) Φέρε τη σελίδα SBZ
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            var resp = await _http.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
                return (false, $"HTTP {(int)resp.StatusCode}: δεν μπόρεσα να διαβάσω τη σελίδα SBZ.", items, supplierId, mark);

            var html = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(html))
                return (false, "Κενή απάντηση από τη σελίδα SBZ.", items, supplierId, mark);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 2) Προσπάθησε να βρεις το Μ.Αρ.Κ. από το κείμενο στο κάτω μέρος
            var allText = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText ?? "");
            var mMark = Regex.Match(allText, @"Μ\.?\s*Αρ\.?\s*Κ\.?\s*:\s*([0-9]{8,20})");
            if (mMark.Success)
                mark = mMark.Groups[1].Value;

            // 3) Βρες τον πίνακα ειδών: πίνακας που η πρώτη γραμμή έχει Κωδικός/Περιγραφή/Ποσότητα
            HtmlNode? productTable = null;

            List<HtmlNode> GetHeaderCells(HtmlNode tbl)
            {
                // thead > tr
                var headCells = tbl.SelectSingleNode(".//thead/tr[1]")?.SelectNodes("./th|./td")?.ToList();
                if (headCells != null && headCells.Count > 0) return headCells;

                // οποιοδήποτε tr με th
                var rowWithTh = tbl.SelectSingleNode(".//tr[th]");
                if (rowWithTh != null)
                    return rowWithTh.SelectNodes("./th|./td")?.ToList() ?? new List<HtmlNode>();

                // fallback: η πρώτη σειρά
                var firstTr = tbl.SelectSingleNode(".//tbody/tr[1]") ?? tbl.SelectSingleNode(".//tr[1]");
                return firstTr?.SelectNodes("./td|./th")?.ToList() ?? new List<HtmlNode>();
            }

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

            var tables = doc.DocumentNode.SelectNodes("//table") ?? new HtmlNodeCollection(null);

            int idxCode = -1, idxDescr = -1, idxQty = -1;

            foreach (var tbl in tables)
            {
                var headerCells = GetHeaderCells(tbl);
                if (headerCells.Count == 0) continue;

                idxCode = IndexByKeys(headerCells, "Κωδικός", "Κωδικος", "CODE");
                idxDescr = IndexByKeys(headerCells, "Περιγραφή", "Περιγραφη", "DESCRIPTION", "ITEM");
                idxQty = IndexByKeys(headerCells, "Ποσ.", "Ποσότητα", "ΠΟΣΟΤΗΤΑ", "QTY", "QUANTITY");

                if (idxDescr >= 0 && idxQty >= 0)
                {
                    productTable = tbl;
                    break;
                }
            }

            if (productTable == null)
                return (false, "Δεν βρέθηκε πίνακας ειδών στη σελίδα SBZ.", items, supplierId, mark);

            // 4) Πάρε τις γραμμές του πίνακα
            var dataRows = productTable.SelectNodes(".//tbody/tr[td]") ??
                           productTable.SelectNodes(".//tr[td]") ??
                           new HtmlNodeCollection(null);

            foreach (var tr in dataRows)
            {
                var tds = tr.SelectNodes("./td")?.ToList();
                if (tds == null || tds.Count == 0) continue;

                string code = idxCode >= 0 ? SafeText(tds, idxCode) : "";
                string descr = idxDescr >= 0 ? SafeText(tds, idxDescr) : "";
                string qtyTxt = idxQty >= 0 ? SafeText(tds, idxQty) : "";

                if (string.IsNullOrWhiteSpace(descr))
                    continue;

                if (!TryParseDecimal(qtyTxt, out var qty))
                    qty = 1;

                items.Add(new StockReceptionItem
                {
                    SupplierCode = code,
                    Description = descr,
                    Quantity = (int)Math.Round(qty, MidpointRounding.AwayFromZero),
                    Barcode = null
                });
            }

            if (items.Count == 0)
                return (false, "Η σελίδα SBZ διαβάστηκε αλλά δεν βρέθηκαν γραμμές ειδών.", items, supplierId, mark);

            return (true, $"OK από SBZ ({items.Count} γραμμές)", items, supplierId, mark ?? url);
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

        private async Task<(bool ok, string message, List<StockReceptionItem> items, int? supplierId, string? mark)>
FetchFromEInvoicingPdfAsync(string url)
        {
            var items = new List<StockReceptionItem>();

            // 🔹 1️⃣ Καρφωμένο URL για το PDF
            var pdfUrl = "https://e-invoicing.gr/api/DownloadPDFFile?contentType=PDF&id=EB5D690D8B25CCE683621EAF3DB01B5EBD0B491D&source=A&hashToken=15aa518c";

            // 🔹 2️⃣ Καρφωμένο viewer URL (ο referrer)
            var viewerUrl = "https://e-invoicing.gr/edocuments/ViewInvoice?ct=PDF&id=EB5D690D8B25CCE683621EAF3DB01B5EBD0B491D&s=A&h=15aa518c";

            // 🔹 3️⃣ Δημιουργία request
            var pdfReq = new HttpRequestMessage(HttpMethod.Get, pdfUrl);
            pdfReq.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127 Safari/537.36");
            pdfReq.Headers.TryAddWithoutValidation("Accept",
                "application/pdf,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");

            // 🔹 4️⃣ Το σημαντικό: referrer = viewer URL
            pdfReq.Headers.Referrer = new Uri(viewerUrl);

            var pdfResp = await _http.SendAsync(pdfReq);
            if (!pdfResp.IsSuccessStatusCode)
                return (false, $"HTTP {(int)pdfResp.StatusCode}: Το PDF δεν ήταν προσβάσιμο.", items, null, null);

            var pdfBytes = await pdfResp.Content.ReadAsByteArrayAsync();

            // 🔹 5️⃣ Έλεγχος ότι όντως είναι PDF
            var header = Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
            if (!header.StartsWith("%PDF"))
                return (false, "Περίμενα PDF αλλά πήρα HTML.", items, null, null);

            // 🔹 6️⃣ Αν είναι σωστό, προχώρα στο parsing
            return ParseEInvoicingPdfBytes(pdfBytes, pdfUrl);
        }

        private (bool ok, string message, List<StockReceptionItem> items, int? supplierId, string? mark)
        ParseEInvoicingPdfBytes(byte[] pdfBytes, string markOrUrl)
        {
            var empty = new List<StockReceptionItem>();

            if (pdfBytes == null || pdfBytes.Length < 10)
                return (false, "Το αρχείο είναι κενό, δεν περιέχει δεδομένα PDF.", empty, null, null);

            var header = Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
            if (!header.StartsWith("%PDF"))
            {
                var preview = Encoding.UTF8.GetString(pdfBytes.Take(200).ToArray());
                return (false, "Δεν είναι PDF. Ξεκινάει με: " + preview, empty, null, null);
            }

            var ms = new MemoryStream(pdfBytes);
            PdfDocument pdf;
            try
            {
                pdf = PdfDocument.Open(ms);
            }
            catch (Exception ex)
            {
                ms.Dispose();
                return (false, "Αποτυχία ανάγνωσης PDF: " + ex.Message, empty, null, null);
            }

            try
            {
                // τρέχουμε ΚΑΙ τα δύο
                var itemsPos = ParseByPositions(pdf);
                var itemsLines = ParseByLines(pdf);

                var merged = MergeItems(itemsPos, itemsLines);

                if (merged.Count == 0)
                    return (false, "Το PDF διαβάστηκε αλλά δεν βρέθηκαν γραμμές με (Περιγραφή, Ποσότητα).", empty, null, null);

                return (true, "OK από e-invoicing.gr", merged, null, markOrUrl);
            }
            finally
            {
                ms.Dispose();
            }
        }
        private static List<StockReceptionItem> MergeItems(
    List<StockReceptionItem> a,
    List<StockReceptionItem> b)
        {
            var result = new List<StockReceptionItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void addRange(List<StockReceptionItem> src)
            {
                foreach (var it in src)
                {
                    var key = (it.SupplierCode ?? "") + "§" + (it.Description ?? "");
                    if (seen.Add(key))
                        result.Add(it);
                }
            }

            addRange(a);
            addRange(b);

            return result;
        }

        private List<StockReceptionItem> ParseByPositions(PdfDocument pdf)
        {
            const double yTol = 2.9;
            var items = new List<StockReceptionItem>();

            // μαζεύουμε όλες τις λέξεις με X/Y
            var allWords = new List<(string Text, double X, double Y, int Page)>();
            int pageIndex = 0;
            foreach (var page in pdf.GetPages())
            {
                pageIndex++;
                foreach (var w in page.GetWords())
                {
                    var t = (w.Text ?? "").Trim();
                    if (t.Length == 0) continue;
                    allWords.Add((t, w.BoundingBox.Left, w.BoundingBox.Bottom, pageIndex));
                }
            }

            if (allWords.Count == 0)
                return items;

            var groupedRows = allWords
                .GroupBy(w => (w.Page, RowKey: Math.Round(w.Y / yTol)))
                .OrderBy(g => g.Key.Page)
                .ThenByDescending(g => g.Key.RowKey)
                .ToList();

            var columnCuts = new List<double>();
            int colCode = -1, colDescr = -1, colQty = -1;

            // βρες τη γραμμή κεφαλίδων
            foreach (var row in groupedRows)
            {
                var rowWords = row.OrderBy(w => w.X).ToList();
                var headerText = NormalizeGreek(string.Join(" ", rowWords.Select(r => r.Text)));
                if (headerText.Contains("ΠΕΡΙΓΡΑΦ") && headerText.Contains("ΠΟΣΟΤ"))
                {
                    double? last = null;
                    foreach (var x in rowWords.Select(r => r.X).OrderBy(x => x))
                    {
                        if (last == null || Math.Abs(x - last.Value) > 20)
                            columnCuts.Add(x);
                        last = x;
                    }

                    for (int i = 0; i < rowWords.Count; i++)
                    {
                        var w = rowWords[i];
                        var txt = NormalizeGreek(w.Text);
                        int colIdx = columnCuts.TakeWhile(c => w.X >= c).Count() - 1;
                        if (txt.Contains("ΚΩΔΙΚ")) colCode = colIdx;
                        if (txt.Contains("ΠΕΡΙΓΡΑΦ")) colDescr = colIdx;
                        if (txt.Contains("ΠΟΣΟΤ") || txt.Contains("QTY")) colQty = colIdx;
                    }

                    break;
                }
            }

            if (colDescr < 0 || colQty < 0 || columnCuts.Count == 0)
                return items;

            // διάβασε τις πραγματικές γραμμές
            foreach (var row in groupedRows)
            {
                var rowWords = row.OrderBy(w => w.X).ToList();
                var lineNorm = NormalizeGreek(string.Join(" ", rowWords.Select(r => r.Text)));

                if (lineNorm.Contains("ΠΕΡΙΓΡΑΦ") ||
                    lineNorm.StartsWith("ΣΥΝΟΛ") ||
                    lineNorm.Contains("ΑΝΑΛΥΣΗ ΣΥΝΤΕΛΕΣΤΗ"))
                    continue;

                var cols = new Dictionary<int, List<string>>();
                foreach (var w in rowWords)
                {
                    int colIdx = columnCuts.TakeWhile(c => w.X >= c).Count() - 1;
                    if (colIdx < 0) colIdx = 0;
                    if (!cols.ContainsKey(colIdx))
                        cols[colIdx] = new List<string>();
                    cols[colIdx].Add(w.Text);
                }

                string GetCol(int idx) => cols.TryGetValue(idx, out var lst) ? string.Join(" ", lst) : "";

                var descr = GetCol(colDescr).Trim();
                if (string.IsNullOrWhiteSpace(descr)) continue;

                string code = colCode >= 0 ? GetCol(colCode).Trim() : "";

                // ποσότητα
                var qtyText = GetCol(colQty);
                qtyText = Regex.Replace(qtyText, @"\b(ΤΜΧ|TEM|PCS)\b", "", RegexOptions.IgnoreCase).Trim();
                if (!TryParseDecimal(qtyText, out var qty)) continue;
                if (qty <= 0 || qty > 1_000_000) continue;
                int safeQty = (int)Math.Round(qty, MidpointRounding.AwayFromZero);

                // 🔴 εδώ είναι η νέα διόρθωση
                // αν ο "κωδικός" στην πραγματικότητα είναι "1234567 ΚΑΤΙ-ΚΕΙΜΕΝΟ",
                // τότε το ΚΑΤΙ-ΚΕΙΜΕΝΟ το περνάμε μπροστά στην περιγραφή
                var codeMatch = Regex.Match(code, @"^(\d{5,})\s+(.+)$");
                if (codeMatch.Success)
                {
                    var realCode = codeMatch.Groups[1].Value.Trim();   // π.χ. 33600002
                    var extraText = codeMatch.Groups[2].Value.Trim();  // π.χ. PE014 ΠΕΝΣΑΚ
                    code = realCode;
                    if (!string.IsNullOrEmpty(extraText))
                    {
                        // βάλ’ το μπροστά στην περιγραφή
                        descr = extraText + " " + descr;
                    }
                }

                items.Add(new StockReceptionItem
                {
                    SupplierCode = code,
                    Description = descr,
                    Quantity = safeQty,
                    Barcode = null
                });
            }

            return items;
        }

        private List<StockReceptionItem> ParseByLines(PdfDocument pdf)
        {
            var items = new List<StockReceptionItem>();

            foreach (var page in pdf.GetPages())
            {
                var text = page.Text ?? string.Empty;

                var lines = text
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => Regex.IsMatch(l, @"^\d{5,}")) // πρέπει να ξεκινάει με αριθμητικό κωδικό
                    .ToList();

                foreach (var line in lines)
                {
                    // Παράδειγμα: 33600002 PE014 ΠΕΝΑΚΙ ACTUEL TECHNIQUE 4mm 6 6,00 ...
                    // Το pattern επιτρέπει ποικιλία στα κενά και στους αριθμούς
                    var m = Regex.Match(
                        line,
                        @"^(?<code>\d{5,})\s+(?<descr>.+?)\s+(?<qty>\d+)(\s+[0-9]+([.,][0-9]+)?){1,}",
                        RegexOptions.Singleline);

                    if (!m.Success)
                        continue;

                    var code = m.Groups["code"].Value.Trim();
                    var descr = m.Groups["descr"].Value.Trim();
                    var qtyText = m.Groups["qty"].Value.Trim();

                    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(descr))
                        continue;

                    if (!TryParseDecimal(qtyText, out var qty))
                        continue;

                    if (qty <= 0 || qty > 1_000_000)
                        continue;

                    items.Add(new StockReceptionItem
                    {
                        SupplierCode = code,
                        Description = descr,
                        Quantity = (int)Math.Round(qty, MidpointRounding.AwayFromZero),
                        Barcode = null
                    });
                }
            }

            return items;
        }



        // μικρό helper – δέχεται 12, 12.00, 1,00 κλπ
        private static bool IsNumberLike(string s)
        {
            s = s.Trim();
            return Regex.IsMatch(s, @"^\d+([.,]\d+)?$");
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
