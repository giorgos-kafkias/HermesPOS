using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using HermesPOS.ViewModels;

namespace HermesPOS.Views
{
	public partial class AdminLoginWindow : Window
	{
		private readonly IServiceProvider _serviceProvider;
		private const string AdminPassword = "1234"; // Ο κωδικός διαχειριστή

		public AdminLoginWindow(IServiceProvider serviceProvider)
		{
			InitializeComponent();
			_serviceProvider = serviceProvider;
		}

		private void AdminLogin_Click(object sender, RoutedEventArgs e)
		{
			Authenticate();
		}

		private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter) // Αν πατηθεί το Enter
			{
				Authenticate();
			}
		}

		private void Authenticate()
		{
			string enteredPassword = PasswordBox.Password;

			if (enteredPassword == AdminPassword)
			{
				var viewModel = _serviceProvider.GetRequiredService<AdminPanelViewModel>();
				var adminPanel = new AdminPanelWindow(viewModel,_serviceProvider);
                // Κάνε το AdminPanel το κύριο παράθυρο
                Application.Current.MainWindow = adminPanel;
                adminPanel.Show();

                // Επιστροφή επιτυχίας στο ShowDialog()
                this.DialogResult = true;
                this.Close();
            }
			else
			{
				MessageBox.Show("Λάθος κωδικός! Προσπαθήστε ξανά.", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			PasswordBox.Focus(); // 👉 Ορισμός αρχικού focus στο πεδίο κωδικού
		}
	}
}
