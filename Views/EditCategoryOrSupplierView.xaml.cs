using HermesPOS.ViewModels;
using System.Windows;


namespace HermesPOS.Views
{
	public partial class EditCategoryOrSupplierView : Window
	{
		public EditCategoryOrSupplierView(EditCategoryOrSupplierViewModel viewModel)
		{
			InitializeComponent();
			DataContext = viewModel;
		}
	}
}
