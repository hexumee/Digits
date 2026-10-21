using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Digits.Pages {
    public sealed partial class BackupPasswordDialog : ContentDialog {
        public BackupPasswordDialog(bool confirmPassword) {
            InitializeComponent();

            IntroText.Text = AppText.Get(confirmPassword ? "BackupExportIntroText" : "BackupImportIntroText");
            Title = AppText.Get(confirmPassword ? "BackupEncryptTitle" : "BackupDecryptTitle");
            PrimaryButtonText = AppText.Get(confirmPassword ? "ExportButton" : "OpenButton");
            ConfirmationPanel.Visibility = confirmPassword ? Visibility.Visible : Visibility.Collapsed;
        }

        public string Passphrase => PassphraseBox.Password;
        public string Confirmation => ConfirmationBox.Password;

        public void ClearPasswords() {
            PassphraseBox.Password = string.Empty;
            ConfirmationBox.Password = string.Empty;
        }
    }
}
