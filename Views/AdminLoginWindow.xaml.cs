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
				adminPanel.Show();

				this.Close(); // Κλείσιμο του παραθύρου εισόδου
			}
			else
			{
				MessageBox.Show("Λάθος κωδικός! Προσπαθήστε ξανά.", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}
}
