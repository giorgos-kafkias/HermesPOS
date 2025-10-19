using System.Windows;
using System.Windows.Controls;

namespace HermesPOS.Views
{
    public partial class QrReceptionView : UserControl
    {
        public QrReceptionView()
        {
            InitializeComponent();
            Loaded += QrReceptionView_Loaded;
        }

        private void QrReceptionView_Loaded(object sender, RoutedEventArgs e)
        {
            QrTextBox.Focus();
        }
    }
}
