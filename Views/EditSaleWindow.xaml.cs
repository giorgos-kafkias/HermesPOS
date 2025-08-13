using HermesPOS.Models;
using HermesPOS.ViewModels;
using System.Windows;

namespace HermesPOS.Views
{
	public partial class EditSaleWindow : Window
	{
		private readonly EditSaleViewModel _viewModel;

		public EditSaleWindow(EditSaleViewModel viewModel, Sale sale)
		{
			InitializeComponent();
			_viewModel = viewModel;
			_viewModel.Initialize(sale);
			DataContext = _viewModel;
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}
	}
}
