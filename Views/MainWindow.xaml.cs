using HermesPOS.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

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

		//ελέγχει τη το validation στο input τιμη
		private void PriceTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			// Επιτρέπει μόνο αριθμούς, κόμμα και τελεία
			string input = e.Text;

			// Το Regex επιτρέπει χαρακτήρες 0-9, κόμμα (,) και τελεία (.)
			Regex regex = new Regex(@"^[0-9.,]+$");

			e.Handled = !regex.IsMatch(input);
		}

	}
}
