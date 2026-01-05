using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HermesPOS.Models
{
    public class StockReceptionItem : INotifyPropertyChanged //Κάθε παραλαβή (τιμολόγιο) έχει πολλά StockReceptionItem
    {
        public int Id { get; set; }
        public int StockReceptionId { get; set; }
        public StockReception StockReception { get; set; }

        private string _supplierCode = string.Empty;
        public string SupplierCode
        {
            get => _supplierCode;
            set { if (_supplierCode != value) { _supplierCode = value; OnPropertyChanged(); } }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set { if (_description != value) { _description = value; OnPropertyChanged(); } }
        }

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set { if (_quantity != value) { _quantity = value; OnPropertyChanged(); } }
        }

        public int? ProductId { get; set; }

        private string? _barcode;
        public string? Barcode
        {
            get => _barcode;
            set { if (_barcode != value) { _barcode = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
