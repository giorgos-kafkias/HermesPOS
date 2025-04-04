using System.Windows;
using HermesPOS.ViewModels;

namespace HermesPOS.Views
{
	public partial class EditProductWindow : Window
	{
		public EditProductWindow(EditProductViewModel viewModel)
		{
			InitializeComponent();
			DataContext = viewModel;
		}
	}
}
