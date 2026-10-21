using Windows.UI.Xaml.Controls;

namespace Digits.Pages {
    public sealed partial class AboutDialog : ContentDialog {
        public AboutDialog() {
            InitializeComponent();
        }

        internal void SetVersion(string version) {
            VersionText.Text = AppText.Format("AboutVersion", version);
        }
    }
}
