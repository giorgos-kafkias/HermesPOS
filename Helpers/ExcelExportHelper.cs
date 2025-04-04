using ClosedXML.Excel;
using HermesPOS.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public static class ExcelExportHelper
{
	public static void ExportToExcel(IEnumerable<Product> products, string fileName)
	{
		try
		{
			// 🔹 Ανάκτηση του φακέλου "Λήψεις" του χρήστη
			string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			string filePath = Path.Combine(downloadsPath, fileName);

			Debug.WriteLine($"🔹 Ξεκινά η εξαγωγή σε Excel: {filePath}");

			using (var workbook = new XLWorkbook())
			{
				var worksheet = workbook.Worksheets.Add("Low Stock Products");

				// ✅ Δημιουργία κεφαλίδων (Χωρίς ID, SupplierID, CategoryID, Barcode)
				worksheet.Cell(1, 1).Value = "Όνομα Προϊόντος";
				worksheet.Cell(1, 2).Value = "Απόθεμα";
				worksheet.Cell(1, 3).Value = "Προμηθευτής";
				worksheet.Cell(1, 4).Value = "Κατηγορία";
				worksheet.Row(1).Style.Font.Bold = true; // Κάνει τη γραμμή τίτλων bold

				int row = 2;
				foreach (var product in products)
				{
					worksheet.Cell(row, 1).Value = product.Name;
					worksheet.Cell(row, 2).Value = product.Stock;
					worksheet.Cell(row, 3).Value = product.Supplier?.Name ?? "Άγνωστος Προμηθευτής";
					worksheet.Cell(row, 4).Value = product.Category?.Name ?? "Άγνωστη Κατηγορία";
					row++;
				}

				// ✅ Αυτόματο μέγεθος στηλών
				worksheet.Columns().AdjustToContents();

				// ✅ Αποθήκευση αρχείου Excel στις Λήψεις
				workbook.SaveAs(filePath);
				Console.WriteLine($"✅ Το αρχείο αποθηκεύτηκε στις Λήψεις: {filePath}");

				// ✅ Άνοιγμα του αρχείου μόλις δημιουργηθεί
				System.Diagnostics.Process.Start("explorer.exe", downloadsPath);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Σφάλμα κατά την εγγραφή στο Excel: {ex.Message}");
		}
	}
}
