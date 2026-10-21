using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using OtpNet;
using Digits.Pages;

namespace Digits {
    public sealed partial class ManualAddPage : Page {
        private sealed class SelectionOption {
            public string Value { get; private set; }
            public string Label { get; private set; }

            public SelectionOption(string value, string label) {
                Value = value;
                Label = label;
            }
        }

        private bool _isSaving;
        private readonly DatabaseManager _databaseManager = new DatabaseManager();

        public ManualAddPage() {
            InitializeComponent();

            Description.Text = AppText.Get("ManualPageDescription");
            TypeBox.Header = AppText.Get("ManualTokenTypeHeader");
            TypeBox.DisplayMemberPath = nameof(SelectionOption.Label);
            TypeBox.ItemsSource = new List<SelectionOption> {
                new SelectionOption("totp", AppText.Get("TotpOption")),
                new SelectionOption("hotp", AppText.Get("HotpOption"))
            };
            TypeBox.SelectedIndex = 0;
            TypeBox.SelectionChanged += TypeBox_SelectionChanged;

            IssuerBox.Header = AppText.Get("IssuerHeader");
            IssuerBox.PlaceholderText = AppText.Get("IssuerPlaceholder");
            AccountBox.Header = AppText.Get("AccountHeader");
            AccountBox.PlaceholderText = AppText.Get("AccountPlaceholder");
            SecretBox.Header = AppText.Get("SecretHeader");
            SecretBox.PlaceholderText = AppText.Get("SecretPlaceholder");

            AlgorithmBox.Header = AppText.Get("AlgorithmHeader");
            AlgorithmBox.DisplayMemberPath = nameof(SelectionOption.Label);
            AlgorithmBox.ItemsSource = new List<SelectionOption> {
                new SelectionOption("SHA1", AppText.Get("Sha1Option")),
                new SelectionOption("SHA256", AppText.Get("Sha256Option")),
                new SelectionOption("SHA512", AppText.Get("Sha512Option"))
            };
            AlgorithmBox.SelectedIndex = 0;

            DigitsBox.Header = AppText.Get("TokenDigitsHeader");
            DigitsBox.DisplayMemberPath = nameof(SelectionOption.Label);
            DigitsBox.ItemsSource = new List<SelectionOption> {
                new SelectionOption("6", AppText.Get("SixDigitsOption")),
                new SelectionOption("8", AppText.Get("EightDigitsOption"))
            };
            DigitsBox.SelectedIndex = 0;

            PeriodBox.Header = AppText.Get("TotpPeriodHeader");
            CounterBox.Header = AppText.Get("HotpCounterHeader");

            UpdateTypeFields();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e) {
            SecretBox.Password = string.Empty;
            base.OnNavigatedFrom(e);
        }

        private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            UpdateTypeFields();
        }

        private void UpdateTypeFields() {
            var selectedType = TypeBox.SelectedItem as SelectionOption;
            bool isHotp = selectedType != null && string.Equals(selectedType.Value, "hotp", StringComparison.Ordinal);
            PeriodBox.Visibility = isHotp ? Visibility.Collapsed : Visibility.Visible;
            CounterBox.Visibility = isHotp ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            GoBack();
        }

        private void GoBack() {
            if (!_isSaving && Frame.CanGoBack) {
                Frame.GoBack();
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e) {
            if (_isSaving) {
                return;
            }

            if (!TryCreateManualToken(out var service, out var error)) {
                await ShowMessageAsync(error);
                return;
            }

            _isSaving = true;
            SaveButton.IsEnabled = false;

            try {
                if (!await _databaseManager.TryAddTokenAsync(service)) {
                    await ShowMessageAsync(AppText.Get("TokenAlreadyAdded"));
                    return;
                }

                SecretBox.Password = string.Empty;
                if (Frame.CanGoBack) {
                    Frame.GoBack();
                }
            } catch (Exception ex) {
                Debug.WriteLine(AppText.Format("ManualTokenSaveFailureLog", ex));
                await ShowMessageAsync(AppText.Get("SaveManualTokenError"));
            } finally {
                _isSaving = false;
                SaveButton.IsEnabled = true;
            }
        }

        private bool TryCreateManualToken(out DatabaseElement service, out string error) {
            service = null;
            error = null;

            var selectedType = TypeBox.SelectedItem as SelectionOption;
            string type = selectedType?.Value;
            string secret = SecretBox.Password?.Trim().Replace(" ", string.Empty);
            string issuer = IssuerBox.Text?.Trim() ?? string.Empty;
            string label = AccountBox.Text?.Trim() ?? string.Empty;

            if (type != "totp" && type != "hotp") {
                error = AppText.Get("TokenTypeInvalid");
                return false;
            }

            if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(issuer)) {
                error = AppText.Get("ServiceOrAccountRequired");
                return false;
            }

            if (string.IsNullOrWhiteSpace(secret)) {
                error = AppText.Get("SecretRequired");
                return false;
            }

            try {
                if (Base32Encoding.ToBytes(secret).Length == 0) {
                    error = AppText.Get("SecretZeroLength");
                    return false;
                }
            } catch {
                error = AppText.Get("SecretInvalid");
                return false;
            }

            var selectedDigits = DigitsBox.SelectedItem as SelectionOption;
            if (selectedDigits == null || !int.TryParse(selectedDigits.Value, out int digits)) {
                error = AppText.Get("TokenDigitsInvalid");
                return false;
            }

            int period = 30;
            int counter = 0;

            if (type == "totp" && (!int.TryParse(PeriodBox.Text, out period) || period < 1 || period > 3600)) {
                error = AppText.Get("TotpPeriodInvalid");
                return false;
            }
            if (type == "hotp" && (!int.TryParse(CounterBox.Text, out counter) || counter < 0)) {
                error = AppText.Get("HotpCounterInvalid");
                return false;
            }

            service = new DatabaseElement {
                Type = type,
                Issuer = issuer,
                Label = string.IsNullOrWhiteSpace(label) ? issuer : label,
                Secret = secret,
                Algorithm = (AlgorithmBox.SelectedItem as SelectionOption)?.Value ?? "SHA1",
                Digits = digits,
                Period = period,
                Counter = counter
            };

            return true;
        }

        private async Task ShowMessageAsync(string message) {
            await new MessageDialog(message).ShowAsync();
        }
    }
}
