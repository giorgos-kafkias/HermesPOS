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
            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow(mainViewModel, _serviceProvider);

            // Κάνε το MainWindow κύριο παράθυρο πριν κλείσεις το Login
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();

            this.Close(); // Τώρα δεν θα τερματίσει η εφαρμογή
        }

        private void AdminLogin_Click(object sender, RoutedEventArgs e)
        {
            var adminLogin = new AdminLoginWindow(_serviceProvider)
            {
                Owner = this // προαιρετικό, για σωστό modality
            };

            bool? result = adminLogin.ShowDialog();

            // ΜΟΝΟ αν έγινε επιτυχία (DialogResult == true) κλείνουμε το Login
            if (result == true)
            {
                this.Close();
            }
            // Αλλιώς δεν κάνουμε τίποτα: μένουμε στο LoginWindow
        }
    }
}
