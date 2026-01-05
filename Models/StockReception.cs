using System;
using System.Collections.Generic;

namespace HermesPOS.Models
{
    public class StockReception //παραλαβη απο προμηθευτη με πολλα προιοντα
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public DateTime ReceptionDate { get; set; }
        public string Mark { get; set; } = string.Empty; // μοναδικό MARK από ΑΑΔΕ                                                  
        public ReceptionStatus Status { get; set; } = ReceptionStatus.Draft;   // Draft = σε επεξεργασία, δεν έχει “περαστεί”.  Posted = έχει ολοκληρωθεί και ενημερώθηκε το stock.
        public ICollection<StockReceptionItem> Items { get; set; } = new List<StockReceptionItem>();
        public Supplier Supplier { get; set; } 
    }

    public enum ReceptionStatus
    {
        Draft = 0,
        Posted = 1
    }
}
