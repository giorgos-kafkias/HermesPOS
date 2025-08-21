using HermesPOS.Models;
using HermesPOS.ViewModels;
using System.Windows;

namespace HermesPOS.Views
{
    public partial class EditSaleWindow : Window
    {
        private readonly EditSaleViewModel _viewModel;

        public EditSaleWindow(EditSaleViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        public void LoadSale(Sale sale)
        {
            _viewModel.Initialize(sale);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
