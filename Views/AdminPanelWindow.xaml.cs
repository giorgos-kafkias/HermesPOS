using System.Windows;
using System.Windows.Controls;
using HermesPOS.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HermesPOS.Views
{
	public partial class AdminPanelWindow : Window
	{
		private readonly AdminPanelViewModel _viewModel;
		private readonly IServiceProvider _serviceProvider;

		public AdminPanelWindow(AdminPanelViewModel viewModel, IServiceProvider serviceProvider)
		{
			InitializeComponent();
			_viewModel = viewModel;
			_serviceProvider = serviceProvider;
			DataContext = _viewModel; // Σύνδεση του ViewModel με το UI

		}

		private void AddProduct_Click(object sender, RoutedEventArgs e)
		{
			_viewModel.AddProductCommand.Execute(null);
		}

		private void EditProduct_Click(object sender, RoutedEventArgs e)
		{
			_viewModel.EditProductCommand.Execute(null);
		}

		private void DeleteProduct_Click(object sender, RoutedEventArgs e)
		{
			_viewModel.DeleteProductCommand.Execute(null);
		}
	// Διαχείριση Κατηγοριών
		private void AddCategory_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Προσθήκη νέας κατηγορίας (Θα προστεθεί).", "Διαχείριση Κατηγοριών");
		}

		private void EditCategory_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Επεξεργασία κατηγορίας (Θα προστεθεί).", "Διαχείριση Κατηγοριών");
		}

		private void DeleteCategory_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Διαγραφή κατηγορίας (Θα προστεθεί).", "Διαχείριση Κατηγοριών");
		}

		// Διαχείριση Προμηθευτών
		private void AddSupplier_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Προσθήκη νέου προμηθευτή (Θα προστεθεί).", "Διαχείριση Προμηθευτών");
		}

		private void EditSupplier_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Επεξεργασία προμηθευτή (Θα προστεθεί).", "Διαχείριση Προμηθευτών");
		}

		private void DeleteSupplier_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Διαγραφή προμηθευτή (Θα προστεθεί).", "Διαχείριση Προμηθευτών");
		}

		private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.Source is TabControl tabControl)
			{
				var selectedTab = tabControl.SelectedItem as TabItem;
				if (selectedTab != null && selectedTab.Header.ToString() == "🔥 Bestseller Προϊόντα")
				{
					if (DataContext is AdminPanelViewModel viewModel)
					{
						await viewModel.BestsellerViewModel.OnTabSelected();
					}
				}
				else if (selectedTab.Header.ToString() == "📈 Αναφορά Πωλήσεων")
				{
					if (DataContext is AdminPanelViewModel viewModel)
					{
						await viewModel.SalesReportViewModel.OnTabSelected();
					}
				}
				else if (selectedTab.Header.ToString() == "📉 Χαμηλό Απόθεμα")
				{
					if (DataContext is AdminPanelViewModel viewModel)
					{
						await viewModel.LowStockViewModel.OnTabSelected();
					}
				}
				
			}
		}
		private void OpenAdminLogin_Click(object sender, RoutedEventArgs e)
		{
			var loginWindow = new LoginWindow(_serviceProvider);
			loginWindow.Show();

			Window.GetWindow(this)?.Close(); // Κλείνει το τρέχον παράθυρο
		}


	}
}
