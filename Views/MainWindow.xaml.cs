using HermesPOS.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;  // για να αναγνωρίζει το TextBox
using System.Windows.Input;
using HermesPOS.Models;

namespace HermesPOS.Views
{
	public partial class MainWindow : Window
	{
		private readonly MainViewModel _viewModel;
		private readonly IServiceProvider _serviceProvider;
		public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
		{
			InitializeComponent();
			_viewModel = viewModel;
			_serviceProvider = serviceProvider;
			DataContext = _viewModel; // Σύνδεση του ViewModel με το UI
									  // Όταν φορτωθεί το παράθυρο, δώσε focus στο barcode TextBox
			Loaded += (s, e) => txtBarcodeScanner.Focus();
		}
		private void OpenAdminLogin_Click(object sender, RoutedEventArgs e)
		{
			var loginWindow = new LoginWindow(_serviceProvider);
			loginWindow.Show();

			this.Close(); // Αν θες να κλείνει το MainWindow
		}
        private void TextBox_EnterCommit(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var tb = (TextBox)sender;

            // 1) Κάνε commit στο binding (ώστε να γραφτεί η νέα τιμή στο ViewModel)
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            // 2) Στείλε το focus πίσω στο πεδίο barcode (ώστε να συνεχίσεις σκανάρισμα)
            txtBarcodeScanner.Focus();

            e.Handled = true; // μην περάσει πιο κάτω το Enter
        }

        private void CartListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double fixedColumnsWidth =
                PriceColumn.Width +
                WholesaleColumn.Width +
                QuantityColumn.Width +
                TotalColumn.Width +
                DeleteColumn.Width;

            double extraSpace = 35;

            double availableWidth =
                CartListView.ActualWidth
                - fixedColumnsWidth
                - extraSpace;

            if (availableWidth > 200)
                ProductNameColumn.Width = availableWidth;
        }
    }
}
