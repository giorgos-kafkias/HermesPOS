using HermesPOS.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HermesPOS.ViewModels
{
    public class QrReceptionViewModel
    {
        public ObservableCollection<StockReceptionItem> Items { get; set; }
            = new ObservableCollection<StockReceptionItem>();
    }

}
