using System.Windows;
using HermesPOS.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HermesPOS.Views
{
	public partial class LoginWindow : Window
	{
		private readonly IServiceProvider _serviceProvider;

		public LoginWindow(IServiceProvider serviceProvider)
		{
			InitializeComponent();
			_serviceProvider = serviceProvider;
		}

		private void UserLogin_Click(object sender, RoutedEventArgs e)
		{
			// Άνοιγμα της εφαρμογής για απλό χρήστη
			var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
			var mainWindow = new MainWindow(mainViewModel,_serviceProvider);
			mainWindow.Show();
			this.Close(); // Κλείσιμο του παραθύρου σύνδεσης
		}

		private void AdminLogin_Click(object sender, RoutedEventArgs e)
		{
			//  Άνοιγμα παραθύρου εισαγωγής κωδικού διαχειριστή
			AdminLoginWindow adminLogin = new AdminLoginWindow(_serviceProvider);
			adminLogin.ShowDialog(); // Περιμένει να κλείσει το AdminLoginWindow πριν συνεχίσει
			this.Close();
		}
	}
}
