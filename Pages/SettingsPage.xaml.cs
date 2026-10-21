using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Security.Credentials.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Digits.Pages {
    public sealed partial class SettingsPage : Page {
        private bool _updatingToggle;

        public SettingsPage() {
            InitializeComponent();

            SetLockToggle(AppPreferences.IsAppLockEnabled);
            AppLockToggle.Toggled += AppLockToggle_Toggled;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            SetLockToggle(AppPreferences.IsAppLockEnabled);
        }

        private void ImportBackup_Click(object sender, RoutedEventArgs e) {
            if (Frame.CanGoBack) {
                MainPage.RequestBackupImportOnReturn();
                Frame.GoBack();
            }
        }

        private void ExportBackup_Click(object sender, RoutedEventArgs e) {
            if (Frame.CanGoBack) {
                MainPage.RequestBackupExportOnReturn();
                Frame.GoBack();
            }
        }

        private void SetLockToggle(bool isEnabled) {
            _updatingToggle = true;
            AppLockToggle.IsOn = isEnabled;
            _updatingToggle = false;
        }

        private async void AppLockToggle_Toggled(object sender, RoutedEventArgs e) {
            if (_updatingToggle) {
                return;
            }

            bool enableLock = AppLockToggle.IsOn;
            if (!enableLock) {
                AppPreferences.IsAppLockEnabled = false;
                return;
            }

            AppLockToggle.IsEnabled = false;

            try {
                var availability = await UserConsentVerifier.CheckAvailabilityAsync();

                if (availability != UserConsentVerifierAvailability.Available) {
                    SetLockToggle(false);
                    await ShowMessageAsync(AppText.Get("LockNeedsDeviceProtection"));

                    return;
                }

                AppPreferences.IsAppLockEnabled = true;
            } catch (Exception ex) {
                SetLockToggle(false);
                await ShowMessageAsync(AppText.Format("DeviceProtectionCheckFailed", ex.Message));
            } finally {
                AppLockToggle.IsEnabled = true;
            }
        }

        private async void About_Click(object sender, RoutedEventArgs e) {
            var version = Package.Current.Id.Version;
            var dialog = new AboutDialog();
            dialog.SetVersion(string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision));
            await dialog.ShowAsync();
        }

        private async Task ShowMessageAsync(string message) {
            await new MessageDialog(message).ShowAsync();
        }
    }
}
