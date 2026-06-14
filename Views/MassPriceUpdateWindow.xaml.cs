using HermesPOS.ViewModels;
using System.Windows;

namespace HermesPOS.Views
{
    public partial class MassPriceUpdateWindow : Window
    {
        public MassPriceUpdateWindow(
            MassPriceUpdateViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
        }
    }
}