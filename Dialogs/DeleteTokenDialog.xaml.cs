using Windows.UI.Xaml.Controls;

namespace Digits.Pages {
    public sealed partial class DeleteTokenDialog : ContentDialog {
        public DeleteTokenDialog(string message) {
            InitializeComponent();

            MessageText.Text = message ?? string.Empty;
        }
    }
}
