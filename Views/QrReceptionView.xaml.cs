using HermesPOS.Models;
using HermesPOS.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace HermesPOS.Views
{
    public partial class QrReceptionView : UserControl
    {
        public QrReceptionView()
        {
            InitializeComponent();
            Loaded += QrReceptionView_Loaded;
        }

        private void QrReceptionView_Loaded(object sender, RoutedEventArgs e)
        {
            QrTextBox.Focus();
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        private async void DataGrid_CellEditEnding(
            object sender,
            DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
                return;

            if (e.Column.Header?.ToString() != "Barcode Προϊόντος")
                return;

            if (e.Row.Item is not StockReceptionItem item)
                return;

            if (DataContext is not QrReceptionViewModel viewModel)
                return;

            await viewModel.RefreshProductStatusAsync(item);
        }
    }
}
