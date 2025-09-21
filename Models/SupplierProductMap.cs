using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HermesPOS.Models
{
    public class SupplierProductMap
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; } = string.Empty;
        public int ProductId { get; set; }
    }
}

