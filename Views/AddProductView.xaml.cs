using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using HermesPOS.ViewModels;

namespace HermesPOS.Views
{
	public partial class AddProductView : Window
	{
		public AddProductView(AddProductViewModel viewModel)
		{
			InitializeComponent();
			DataContext = viewModel;
			viewModel.CloseAction = () => this.Close(); // ✅ Αναθέτουμε το κλείσιμο στο ViewModel
		}

		private void DecimalValidationTextBox(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex(@"^[0-9]*[.,]?[0-9]*$"); // Επιτρέπει αριθμούς και ένα δεκαδικό σημείο
			e.Handled = !regex.IsMatch(e.Text); // Απορρίπτει μη επιτρεπτούς χαρακτήρες
		}
        private void Barcode_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;     // 🚫 μην περάσει το Enter πιο κάτω
                NameTextBox.Focus();  // ✅ πήγαινε στο όνομα
            }
        }

    }
}
