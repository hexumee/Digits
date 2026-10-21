using Windows.UI.Xaml.Controls;

namespace Digits.Pages {
    public sealed partial class MessageDialog : ContentDialog {
        public MessageDialog(string message, string titleResourceKey = null, string primaryButtonResourceKey = null, string secondaryButtonResourceKey = null) {
            InitializeComponent();

            MessageText.Text = message ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(titleResourceKey)) {
                Title = AppText.Get(titleResourceKey);
            }

            if (!string.IsNullOrWhiteSpace(primaryButtonResourceKey)) {
                PrimaryButtonText = AppText.Get(primaryButtonResourceKey);
            }

            if (!string.IsNullOrWhiteSpace(secondaryButtonResourceKey)) {
                SecondaryButtonText = AppText.Get(secondaryButtonResourceKey);
            }
        }
    }
}
