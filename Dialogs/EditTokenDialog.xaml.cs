using Windows.UI.Xaml.Controls;

namespace Digits.Pages {
    public sealed partial class EditTokenDialog : ContentDialog {
        public EditTokenDialog(string issuer, string account) {
            InitializeComponent();

            IssuerBox.Text = issuer ?? string.Empty;
            AccountBox.Text = account ?? string.Empty;
        }

        public string Issuer => IssuerBox.Text;
        public string Account => AccountBox.Text;
    }
}
