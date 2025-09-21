using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HermesPOS.Models
{
    public class StockReceptionItem
    {
        public int Id { get; set; }
        public int StockReceptionId { get; set; }
        public StockReception StockReception { get; set; }

        public string SupplierCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }

        public int? ProductId { get; set; }   // null αν είναι ασυσχέτιστο
        public string? Barcode { get; set; }
    }
}

