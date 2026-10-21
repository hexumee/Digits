using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.Security.Credentials.UI;
using Windows.System;
using OtpNet;
using Digits.Security;
using Digits.Pages;

namespace Digits {
    public sealed partial class MainPage : Page {
        private enum BackupAction {
            Import,
            Export
        }

        private static BackupAction? _pendingBackupAction;
        private readonly DatabaseManager _databaseManager = new DatabaseManager();

        public Dictionary<int, Otp> otps = new Dictionary<int, Otp>();

        private readonly Dictionary<DatabaseElement, DispatcherTimer> _countdownTimers = new Dictionary<DatabaseElement, DispatcherTimer>();
        private readonly Dictionary<DatabaseElement, FrameworkElement> _revealedItems = new Dictionary<DatabaseElement, FrameworkElement>();
        private readonly Dictionary<DatabaseElement, DateTimeOffset> _revealExpiry = new Dictionary<DatabaseElement, DateTimeOffset>();
        private readonly HashSet<DatabaseElement> _transitioningItems = new HashSet<DatabaseElement>();
        private readonly HashSet<DatabaseElement> _suppressNextRevealClick = new HashSet<DatabaseElement>();
        private readonly UISettings _uiSettings = new UISettings();
        private StackPanel emptyState;
        private TextBlock emptyStateHint;
        private Grid lockOverlay;
        private Border lockOverlayBorder;
        private Button unlockButton;
        private bool _isPageActive;
        private bool _isDarkTheme;
        private bool _isLocked;
        private bool _appLockEnabled;
        private bool _isUnlocking;
        private int _lockVersion;
        private TaskCompletionSource<bool> _unlockCompletion;

        private static readonly Dictionary<string, string> PackagedIconAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "google", "google" },
            { "googleauthenticator", "google" },
            { "googlecloud", "google" },
            { "googleworkspace", "google" },
            { "gmail", "google" },
            { "googleplus", "google" },
            { "гугл", "google" },
            { "googledrive", "google" },
            { "googleplay", "google" },
            { "googlewallet", "google" },
            { "googlechrome", "google" },
            { "chrome", "google" },
            { "youtube", "google" },
            { "blogger", "google" },
            { "githubcom", "github" },
            { "gitlabcom", "gitlab" },
            { "amazonwebservices", "amazon" },
            { "amazonpay", "amazon" },
            { "амазон", "amazon" },
            { "goodreads", "amazon" },
            { "imdb", "amazon" },
            { "microsoftedge", "microsoft" },
            { "edge", "microsoft" },
            { "windows", "microsoft" },
            { "windows10", "microsoft" },
            { "xbox", "microsoft" },
            { "skype", "microsoft" },
            { "майкрософт", "microsoft" },
            { "facebookf", "facebook" },
            { "facebookmessenger", "facebook" },
            { "slideshare", "linkedin" },
            { "yandexinternational", "yandex" },
            { "яндекс", "yandex" },
            { "nintendoswitch", "nintendo" },
            { "stackoverflow", "stackexchange" },
            { "weebly", "square" },
            { "slacknew", "slack" },
            { "bandcamp", "bandcampbutton" },
            { "stripe", "stripes" },
            { "vkontakte", "vk" },
            { "вконтакте", "vk" },
            { "vkid", "vk" },
            { "одноклассники", "odnoklassniki" },
            { "wordpresscom", "wordpress" },
            { "tumbler", "tumblr" }
        };

        private static readonly HashSet<string> PackagedIconNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "500px", "accusoft", "adversal", "affiliatetheme", "algolia", "amazon", "amilia", "angellist",
            "angrycreative", "apper", "asymmetrik", "avianex", "bandcampbutton",
            "behance", "bimobject", "bitbucket", "bity", "buromobelexperte", "buysellads", "centercode",
            "cloudscale", "cloudsmith", "cloudversify", "codepen", "codiepie", "connectdevelop", "contao",
            "cpanel", "cuttlefish", "dashcube", "deploydog", "deskpro", "deviantart", "digg", "digitalocean",
            "discord", "discourse", "dochub", "docker", "draft2digital", "dribbble", "dropbox", "drupal",
            "earlybirds", "ebay", "elementor", "envira", "epicgames", "etsy", "expeditedssl",
            "facebook", "fedora", "firstdraft", "flickr", "flipboard", "forumbee", "freecodecamp", "fulcrum",
            "gg", "github", "gitkraken", "gitlab", "gitter", "glide", "gofore", "google",
            "grav", "gripfire", "hips", "hireahelper", "hornbill", "hotjar", "hubspot", "instagram", "ioxhost",
            "itchio", "jenkins", "joget", "jsfiddle", "keybase", "keycdn", "kickstarter", "korvue", "laravel",
            "lastfm", "leanpub", "line", "linkedin", "linode", "lyft", "mailchimp", "mastodon", "maxcdn",
            "medapps", "medium", "medrt", "meetup", "megaport", "microsoft", "mixcloud", "mizuni", "modx",
            "napster", "nimblr", "nintendo", "npm", "ns8", "nutritionix", "odnoklassniki", "page4", "pagelines",
            "palfed", "patreon", "paypal", "phabricator", "pinterest", "playstation", "producthunt", "pushed",
            "qq", "quinscape", "quora", "ravelry", "readme", "rebel", "reddit", "redhat", "redriver", "renren",
            "replyd", "researchgate", "resolving", "rocketchat", "rockrms", "schlix", "scribd", "sellcast", "sellsy",
            "servicestack", "shirtsinbulk", "shopware", "simplybuilt", "sistrix", "skyatlas", "slack", "snapchat",
            "soundcloud", "speakap", "spotify", "square", "squarespace", "stackexchange", "staylinked", "steam",
            "stickermule", "strava", "stripes", "studiovinari", "teamspeak", "themeco", "themeisle", "tripadvisor",
            "tumblr", "twitch", "twitter", "uber", "untappd", "ussunnah", "vaadin", "vimeo", "vk",
            "vnv", "weibo", "whmcs", "wikipedia", "wix", "wordpress", "xing", "yahoo", "yandex", "ycombinator", "yelp"
        };

        public MainPage() {
            try {
                InitializeComponent();
            } catch (XamlParseException ex) {
                System.Diagnostics.Debug.WriteLine(AppText.Format("MainPageXamlParseLog", ex.HResult.ToString("X8"), ex));

                foreach (var key in ex.Data.Keys) {
                    System.Diagnostics.Debug.WriteLine(AppText.Format("XamlParserDataLog", key, ex.Data[key]));
                }

                throw;
            }

            _isDarkTheme = IsSystemDarkTheme();
            CreateRuntimeOverlays();
            _appLockEnabled = AppPreferences.IsAppLockEnabled;
            _isLocked = _appLockEnabled;
            lockOverlay.Visibility = _isLocked ? Visibility.Visible : Visibility.Collapsed;
            commandBar.IsEnabled = !_isLocked;
            _unlockCompletion = new TaskCompletionSource<bool>();
        }

        internal static void RequestBackupImportOnReturn() {
            _pendingBackupAction = BackupAction.Import;
        }

        internal static void RequestBackupExportOnReturn() {
            _pendingBackupAction = BackupAction.Export;
        }

        private void CreateRuntimeOverlays() {
            var pageRoot = Content as Grid;
            if (pageRoot == null) {
                throw new InvalidOperationException(AppText.Get("MainPageLayoutError"));
            }

            emptyState = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 320,
                Visibility = Visibility.Collapsed
            };

            emptyState.Children.Add(new TextBlock {
                Text = AppText.Get("MainEmptyTitle"),
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            emptyStateHint = new TextBlock {
                Text = AppText.Get("MainEmptyBody"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };

            emptyState.Children.Add(emptyStateHint);

            Grid.SetRow(emptyState, 1);
            pageRoot.Children.Add(emptyState);

            lockOverlay = new Grid { Visibility = Visibility.Visible };
            Grid.SetRowSpan(lockOverlay, 2);

            lockOverlayBorder = new Border { Padding = new Thickness(24) };

            var lockPanel = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 360
            };

            lockPanel.Children.Add(new TextBlock {
                Text = AppText.Get("LockTitle"),
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            lockPanel.Children.Add(new TextBlock {
                Text = AppText.Get("LockBody"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            unlockButton = new Button {
                Content = AppText.Get("UnlockButton"),
                HorizontalAlignment = HorizontalAlignment.Center,
                MinWidth = 180,
                Margin = new Thickness(0, 0, 0, 12)
            };

            unlockButton.Click += UnlockButton_Click;
            lockPanel.Children.Add(unlockButton);

            lockOverlayBorder.Child = lockPanel;
            lockOverlay.Children.Add(lockOverlayBorder);

            pageRoot.Children.Add(lockOverlay);

            UpdateRuntimeOverlayTheme();
        }

        private void UpdateRuntimeOverlayTheme() {
            var pageBackground = new SolidColorBrush(_uiSettings.GetColorValue(UIColorType.Background));

            if (lockOverlayBorder != null) {
                lockOverlayBorder.Background = pageBackground;
            }

            if (emptyStateHint != null) {
                emptyStateHint.Foreground = new SolidColorBrush(_isDarkTheme ? Windows.UI.Colors.LightGray : Windows.UI.Colors.DimGray);
            }
        }

        private async Task<bool> RefreshServicesAsync(bool decryptSecrets = false) {
            StopAllCountdownTimers();

            _revealedItems.Clear();
            _revealExpiry.Clear();
            _suppressNextRevealClick.Clear();

            int lockVersion = _lockVersion;

            var registeredServices = await _databaseManager.GetServicesAsync(decryptSecrets, () => lockVersion != _lockVersion || (decryptSecrets && _isLocked));
            if (registeredServices == null) {
                return false;
            }

            foreach (var service in registeredServices) {
                // Only a packaged issuer icon or the monogram fallback is displayed
                service.IconPath = GetPackagedIconPath(service);
            }

            servicesList.ItemsSource = registeredServices;
            emptyState.Visibility = registeredServices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (decryptSecrets) {
                ReinitializeOtps(registeredServices);
            } else {
                otps.Clear();
            }

            return true;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            _isPageActive = true;

            bool appLockEnabled = AppPreferences.IsAppLockEnabled;
            if (appLockEnabled != _appLockEnabled) {
                _appLockEnabled = appLockEnabled;

                if (appLockEnabled) {
                    LockForBackground();
                } else {
                    _isLocked = false;
                    _isUnlocking = false;
                    _lockVersion++;

                    if (_unlockCompletion != null && !_unlockCompletion.Task.IsCompleted) {
                        _unlockCompletion.TrySetResult(true);
                    }

                    lockOverlay.Visibility = Visibility.Collapsed;
                    commandBar.IsEnabled = true;
                }
            }

            if (AppPreferences.LockOnNextMainPageNavigation) {
                AppPreferences.LockOnNextMainPageNavigation = false;

                if (appLockEnabled) {
                    LockForBackground();
                }
            }

            _uiSettings.ColorValuesChanged -= UISettings_ColorValuesChanged;
            _uiSettings.ColorValuesChanged += UISettings_ColorValuesChanged;

            UpdateIconThemeFromSystem();

            try {
                await RefreshServicesAsync(!_isLocked);
            } catch (Exception ex) {
                if (!_isLocked) {
                    LockForBackground();
                } else {
                    commandBar.IsEnabled = false;
                }

                lockOverlay.Visibility = Visibility.Visible;

                await ShowMessage(AppText.Format("StorageError", ex.Message));
            }

            if (_isLocked) {
                _pendingBackupAction = null;
                return;
            }

            if (_pendingBackupAction.HasValue) {
                BackupAction requestedAction = _pendingBackupAction.Value;
                _pendingBackupAction = null;

                if (requestedAction == BackupAction.Import) {
                    await ImportBackupAsync();
                } else {
                    await ExportBackupAsync();
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e) {
            _isPageActive = false;

            _uiSettings.ColorValuesChanged -= UISettings_ColorValuesChanged;

            StopAllCountdownTimers();

            _revealedItems.Clear();
            _revealExpiry.Clear();
            _suppressNextRevealClick.Clear();

            base.OnNavigatedFrom(e);
        }

        private async void UISettings_ColorValuesChanged(UISettings sender, object args) {
            if (!_isPageActive) {
                return;
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, UpdateIconThemeFromSystem);
        }

        private void UpdateIconThemeFromSystem() {
            bool isDarkTheme = IsSystemDarkTheme();
            if (_isDarkTheme == isDarkTheme) {
                return;
            }

            _isDarkTheme = isDarkTheme;

            UpdateRuntimeOverlayTheme();

            foreach (var service in servicesList.Items.OfType<DatabaseElement>()) {
                service.IconPath = GetPackagedIconPath(service);
            }
        }

        private bool IsSystemDarkTheme() {
            var background = _uiSettings.GetColorValue(UIColorType.Background);
            double luminance = (background.R * 299 + background.G * 587 + background.B * 114) / 1000.0;

            return luminance < 128;
        }

        private void AppBarButton_Add_Click(object sender, RoutedEventArgs e) {
            if (_isLocked) {
                return;
            }

            Frame.Navigate(typeof(AddPage), null, new DrillInNavigationTransitionInfo());
        }

        private void ManualEntry_Click(object sender, RoutedEventArgs e) {
            if (_isLocked) {
                return;
            }

            Frame.Navigate(typeof(ManualAddPage), null, new DrillInNavigationTransitionInfo());
        }

        private void Settings_Click(object sender, RoutedEventArgs e) {
            if (_isLocked) {
                return;
            }

            Frame.Navigate(typeof(SettingsPage), null, new DrillInNavigationTransitionInfo());
        }

        private async void About_Click(object sender, RoutedEventArgs e) {
            var version = Package.Current.Id.Version;
            var dialog = new AboutDialog();
            dialog.SetVersion(string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision));
            await dialog.ShowAsync();
        }

        private async void UnlockButton_Click(object sender, RoutedEventArgs e) {
            if (_isUnlocking || !_isLocked) {
                return;
            }

            _isUnlocking = true;
            int unlockVersion = _lockVersion;
            unlockButton.IsEnabled = false;

            try {
                var availability = await UserConsentVerifier.CheckAvailabilityAsync();
                if (availability != UserConsentVerifierAvailability.Available) {
                    var dialog = new MessageDialog(AppText.Get("DeviceProtectionRequiredBody"), "DeviceProtectionRequiredTitle", "NoDeviceSettingsButton", "CancelDialogButton");
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary) {
                        await Launcher.LaunchUriAsync(new Uri("ms-settings:signinoptions"));
                    }

                    return;
                }

                var result = await UserConsentVerifier.RequestVerificationAsync(AppText.Get("UnlockVerificationPrompt"));
                if (result != UserConsentVerificationResult.Verified || !_isUnlocking || unlockVersion != _lockVersion || !_isPageActive) {
                    return;
                }

                if (await RefreshServicesAsync(true)) {
                    _isLocked = false;
                    lockOverlay.Visibility = Visibility.Collapsed;
                    commandBar.IsEnabled = true;
                    _unlockCompletion.TrySetResult(true);
                }
            } catch (Exception ex) {
                await ShowMessage(AppText.Format("LockUnlockError", ex.Message));
            } finally {
                _isUnlocking = false;
                unlockButton.IsEnabled = true;
            }
        }

        public void LockForBackground() {
            if (!AppPreferences.IsAppLockEnabled) {
                return;
            }

            _isLocked = true;
            _isUnlocking = false;
            _lockVersion++;

            if (_unlockCompletion != null && !_unlockCompletion.Task.IsCompleted) {
                _unlockCompletion.TrySetResult(false);
            }

            _unlockCompletion = new TaskCompletionSource<bool>();

            StopAllCountdownTimers();

            foreach (var pair in _revealedItems.ToList()) {
                var panel = pair.Value;

                if (panel.FindName("revealedOtpCode") is TextBlock code) {
                    code.Text = string.Empty;
                }

                if (panel.FindName("otpOverlay") is FrameworkElement otpOverlay) {
                    otpOverlay.Visibility = Visibility.Collapsed;
                }

                if (panel.FindName("serviceOverlay") is FrameworkElement serviceOverlay) {
                    serviceOverlay.Visibility = Visibility.Visible;
                    serviceOverlay.Opacity = 1;
                }

                if (panel.FindName("countdownBar") is FrameworkElement countdownBar) {
                    countdownBar.Visibility = Visibility.Collapsed;
                }
            }

            _revealedItems.Clear();
            _revealExpiry.Clear();
            _transitioningItems.Clear();
            _suppressNextRevealClick.Clear();
            otps.Clear();

            foreach (var service in servicesList.Items.OfType<DatabaseElement>()) {
                service.Secret = null;
            }

            lockOverlay.Visibility = Visibility.Visible;
            commandBar.IsEnabled = false;
        }

        private async Task<bool> WaitUntilUnlockedAsync() {
            while (_isLocked) {
                var pendingUnlock = _unlockCompletion;
                if (pendingUnlock == null) {
                    return false;
                }

                await pendingUnlock.Task;
            }

            return true;
        }

        private async Task ImportBackupAsync() {
            if (_isLocked) {
                return;
            }

            var picker = new FileOpenPicker {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.Downloads
            };

            picker.FileTypeFilter.Add(".xml");
            picker.FileTypeFilter.Add(".json");

            var file = await picker.PickSingleFileAsync();
            if (file == null || !await WaitUntilUnlockedAsync()) {
                return;
            }

            int operationVersion = _lockVersion;

            try {
                var properties = await file.GetBasicPropertiesAsync();
                if (properties.Size > 64 * 1024 * 1024) {
                    await ShowMessage(AppText.Get("FileTooLarge"));
                    return;
                }

                string content;
                using (var stream = await file.OpenReadAsync()) {
                    using (var reader = new StreamReader(stream.AsStreamForRead())) {
                        content = await reader.ReadToEndAsync();
                    }
                }

                EncryptedBackupEnvelope envelope;
                List<DatabaseElement> imported = null;
                bool isXml = file.FileType.Equals(".xml", StringComparison.OrdinalIgnoreCase) || content.TrimStart().StartsWith("<");
                if (isXml) {
                    envelope = TryDeserializeEncryptedXml(content);
                } else {
                    envelope = TryDeserializeEncryptedJson(content);
                }

                if (envelope == null || !BackupEncryption.IsSupportedFormat(envelope.Format)) {
                    await ShowMessage(AppText.Get("InvalidBackupFormat"));
                    return;
                }

                string plaintext = await DecryptBackupAsync(envelope);
                if (plaintext == null) {
                    return;
                }

                imported = DeserializeJson(plaintext);

                if (_isLocked || operationVersion != _lockVersion) {
                    return;
                }

                await ImportServicesAsync(imported);
            } catch (Exception ex) {
                await ShowMessage(AppText.Format("ImportError", ex.Message));
            }
        }

        private async Task ExportBackupAsync() {
            if (_isLocked) {
                return;
            }

            var picker = new FileSavePicker {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"{AppText.Get("BackupFileNamePrefix")}-{DateTime.Now:yyyyMMdd-HHmmss}"
            };

            picker.FileTypeChoices.Add(AppText.Get("DigitsExportFileTypeJson"), new List<string> { ".json" });
            picker.FileTypeChoices.Add(AppText.Get("DigitsExportFileTypeXml"), new List<string> { ".xml" });

            StorageFile file = await picker.PickSaveFileAsync();
            if (file == null || !await WaitUntilUnlockedAsync()) {
                return;
            }

            List<DatabaseElement> services = null;
            string password = null;

            try {
                password = await PromptBackupPasswordAsync(true);
                if (password == null || !await WaitUntilUnlockedAsync()) {
                    return;
                }

                int operationVersion = _lockVersion;

                services = await _databaseManager.GetServicesForBackupAsync(() => _isLocked || operationVersion != _lockVersion);
                if (services == null || _isLocked || operationVersion != _lockVersion) {
                    return;
                }

                if (!services.Any()) {
                    await ShowMessage(AppText.Get("EmptyExport"));
                    return;
                }

                string payload = SerializeJson(services);
                var envelope = await BackupEncryption.EncryptAsync(payload, password);
                payload = null;

                if (_isLocked || operationVersion != _lockVersion) {
                    return;
                }

                string serialized = file.FileType.Equals(".xml", StringComparison.OrdinalIgnoreCase) ? SerializeEncryptedXml(envelope) : SerializeEncryptedJson(envelope);
                CachedFileManager.DeferUpdates(file);
                await FileIO.WriteTextAsync(file, serialized);

                if (_isLocked || operationVersion != _lockVersion) {
                    return;
                }

                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status == FileUpdateStatus.Complete) {
                    await ShowMessage(AppText.Format("DigitsExportedCount", services.Count));
                } else {
                    await ShowMessage(AppText.Get("ExportFinalizeError"));
                }
            } catch (Exception ex) {
                await ShowMessage(AppText.Format("ExportError", ex.Message));
            } finally {
                if (services != null) {
                    foreach (var service in services) {
                        service.Secret = null;
                    }
                }

                password = null;
            }
        }

        private async void RevealCode_Click(object sender, RoutedEventArgs e) {
            if (!(sender is Button button) || !(button.DataContext is DatabaseElement model) || !(button.ContentTemplateRoot is RelativePanel root)) {
                return;
            }

            if (_suppressNextRevealClick.Remove(model)) {
                return;
            }

            var serviceOverlay = root.FindName("serviceOverlay") as StackPanel;
            var otpOverlay = root.FindName("otpOverlay") as StackPanel;
            var revealedOtpCode = root.FindName("revealedOtpCode") as TextBlock;
            var countdownBar = root.FindName("countdownBar") as ProgressBar;

            if (_isLocked || !_isPageActive ||
                _revealedItems.ContainsKey(model) || _transitioningItems.Contains(model) ||
                serviceOverlay == null || otpOverlay == null || revealedOtpCode == null || countdownBar == null ||
                serviceOverlay.Visibility != Visibility.Visible ||
                !otps.TryGetValue(model.Id, out var otp))
            {
                return;
            }

            int revealLockVersion = _lockVersion;
            DateTime timestamp = DateTime.UtcNow;
            long period = model.Period > 0 ? model.Period : 30;
            bool isTotp = model.Type.Equals("totp", StringComparison.OrdinalIgnoreCase);
            DateTimeOffset expiresAt;
            string code;

            if (isTotp) {
                code = ((Totp)otp).ComputeTotp(timestamp);
                var generatedAt = new DateTimeOffset(timestamp);
                long unixSeconds = generatedAt.ToUnixTimeSeconds();
                long nextWindow = unixSeconds + period - (unixSeconds % period);
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(nextWindow);

                // Still show the current code during its final second
                // It expires at the next TOTP boundary, not one second before it
                if (expiresAt <= DateTimeOffset.UtcNow) {
                    return;
                }
            } else {
                if (!_transitioningItems.Add(model)) {
                    return;
                }

                try {
                    if (model.Counter == int.MaxValue) {
                        await ShowMessage(AppText.Get("HotpCounterLimit"));
                        return;
                    }

                    code = ((Hotp)otp).ComputeHOTP(model.Counter);
                    var updateResult = await _databaseManager.AdvanceHotpCounterAsync(model.Id, model.Counter + 1, () => _isLocked || revealLockVersion != _lockVersion);

                    if (_isLocked || revealLockVersion != _lockVersion || updateResult == HotpCounterUpdateResult.Cancelled) {
                        return;
                    }

                    if (updateResult == HotpCounterUpdateResult.NotFound) {
                        await ShowMessage(AppText.Get("NotFound"));
                        await RefreshServicesAsync(!_isLocked);

                        return;
                    }

                    model.Counter++;
                } catch (Exception ex) {
                    await ShowMessage(AppText.Format("HotpCounterUpdateError", ex.Message));
                    return;
                } finally {
                    _transitioningItems.Remove(model);
                }

                expiresAt = DateTimeOffset.MinValue;
            }

            if (_isLocked || revealLockVersion != _lockVersion) {
                return;
            }

            revealedOtpCode.Text = FormatOtpCode(code, model.Digits);

            otpOverlay.Opacity = 1;
            serviceOverlay.Opacity = 1;
            otpOverlay.Visibility = Visibility.Visible;
            countdownBar.Visibility = Visibility.Visible;

            double remaining = isTotp ? (expiresAt - DateTimeOffset.UtcNow).TotalSeconds : period;
            countdownBar.Value = Math.Max(0, Math.Min(1, remaining / period));

            _transitioningItems.Add(model);

            try {
                await AnimateOverlaySwitchAsync(serviceOverlay, otpOverlay, true);

                if (isTotp && expiresAt <= DateTimeOffset.UtcNow) {
                    await AnimateOverlaySwitchAsync(serviceOverlay, otpOverlay, false);
                    countdownBar.Visibility = Visibility.Collapsed;

                    return;
                }
            } finally {
                _transitioningItems.Remove(model);
            }

            if (_isLocked || !_isPageActive || revealLockVersion != _lockVersion) {
                return;
            }

            // TOTP codes must disappear when their shared time window expires
            // HOTP rows get a full independent display interval after their reveal
            if (!isTotp) {
                expiresAt = DateTimeOffset.UtcNow.AddSeconds(period);
            }

            _revealedItems[model] = root;
            _revealExpiry[model] = expiresAt;

            StartCountdownTimer(model, root);
        }

        private void StartCountdownTimer(DatabaseElement model, FrameworkElement panel) {
            StopCountdownTimer(model);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (sender, args) => CountdownTimer_Tick(model, panel, timer);
            _countdownTimers[model] = timer;

            if (!UpdateCountdown(model, panel)) {
                StopCountdownTimer(model);
                _ = HideRevealedItemAsync(model);

                return;
            }

            timer.Start();
        }

        private void StopCountdownTimer(DatabaseElement model) {
            if (model != null && _countdownTimers.TryGetValue(model, out var timer)) {
                timer.Stop();
                _countdownTimers.Remove(model);
            }
        }

        private void StopAllCountdownTimers() {
            foreach (var timer in _countdownTimers.Values.ToList()) {
                timer.Stop();
            }

            _countdownTimers.Clear();
        }

        private void CountdownTimer_Tick(DatabaseElement model, FrameworkElement panel, DispatcherTimer timer) {
            if (!_isPageActive || !_revealedItems.TryGetValue(model, out var activePanel) || !ReferenceEquals(panel, activePanel)) {
                timer.Stop();
                _countdownTimers.Remove(model);

                return;
            }

            if (!UpdateCountdown(model, panel)) {
                timer.Stop();
                _countdownTimers.Remove(model);
                _ = HideRevealedItemAsync(model);
            }
        }

        private bool UpdateCountdown(DatabaseElement model, FrameworkElement panel) {
            var bar = panel?.FindName("countdownBar") as ProgressBar;
            if (bar == null || !_revealExpiry.TryGetValue(model, out var expiresAt)) {
                return false;
            }

            long period = model.Period > 0 ? model.Period : 30;
            double remaining = (expiresAt - DateTimeOffset.UtcNow).TotalSeconds;
            bar.Value = Math.Max(0, Math.Min(1, remaining / period));

            return remaining > 0;
        }

        private string FormatOtpCode(string code, int digits) {
            if (digits == 6) {
                return code.Insert(3, " ");
            }

            if (digits == 8) {
                return code.Insert(4, " ");
            }

            return code;
        }

        private async Task HideRevealedItemAsync(DatabaseElement model) {
            if (model == null || !_revealedItems.TryGetValue(model, out var panel)) {
                return;
            }

            _revealedItems.Remove(model);
            _revealExpiry.Remove(model);
            StopCountdownTimer(model);

            var serviceOverlay = panel.FindName("serviceOverlay") as FrameworkElement;
            var otpOverlay = panel.FindName("otpOverlay") as FrameworkElement;

            _transitioningItems.Add(model);

            try {
                if (serviceOverlay != null && otpOverlay != null) {
                    await AnimateOverlaySwitchAsync(serviceOverlay, otpOverlay, false);
                }
            } finally {
                _transitioningItems.Remove(model);
            }

            if (otpOverlay != null) {
                otpOverlay.Visibility = Visibility.Collapsed;
            }

            if (panel.FindName("countdownBar") is FrameworkElement countdownBar) {
                countdownBar.Visibility = Visibility.Collapsed;
            }

        }

        private async Task AnimateOverlaySwitchAsync(FrameworkElement serviceOverlay, FrameworkElement otpOverlay, bool revealCode) {
            var outgoing = revealCode ? serviceOverlay : otpOverlay;
            var incoming = revealCode ? otpOverlay : serviceOverlay;
            var serviceTransform = GetOrCreateTranslateTransform(serviceOverlay);
            var otpTransform = GetOrCreateTranslateTransform(otpOverlay);
            var outgoingTransform = revealCode ? serviceTransform : otpTransform;
            var incomingTransform = revealCode ? otpTransform : serviceTransform;

            if (!new UISettings().AnimationsEnabled) {
                outgoing.Visibility = Visibility.Collapsed;
                incoming.Visibility = Visibility.Visible;
                outgoing.Opacity = 1;
                incoming.Opacity = 1;
                outgoingTransform.Y = 0;
                incomingTransform.Y = 0;

                return;
            }

            incoming.Visibility = Visibility.Visible;
            outgoing.Visibility = Visibility.Visible;
            outgoingTransform.Y = 0;
            incomingTransform.Y = 24;
            outgoing.Opacity = 1;
            incoming.Opacity = 0;

            var storyboard = new Storyboard();
            storyboard.Children.Add(CreateTranslateAnimation(outgoingTransform, 0, -12));
            storyboard.Children.Add(CreateTranslateAnimation(incomingTransform, 24, 0));
            storyboard.Children.Add(CreateOpacityAnimation(outgoing, 1, 0));
            storyboard.Children.Add(CreateOpacityAnimation(incoming, 0, 1));

            var completed = new TaskCompletionSource<object>();
            storyboard.Completed += (sender, args) => completed.TrySetResult(null);
            storyboard.Begin();

            await completed.Task;

            outgoing.Visibility = Visibility.Collapsed;
            outgoingTransform.Y = 0;
            incomingTransform.Y = 0;
            outgoing.Opacity = 1;
            incoming.Opacity = 1;
        }

        private TranslateTransform GetOrCreateTranslateTransform(FrameworkElement element) {
            var transform = element.RenderTransform as TranslateTransform;
            if (transform == null) {
                transform = new TranslateTransform();
                element.RenderTransform = transform;
            }

            return transform;
        }

        private DoubleAnimation CreateTranslateAnimation(TranslateTransform transform, double from, double to) {
            var animation = new DoubleAnimation {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            Storyboard.SetTarget(animation, transform);
            Storyboard.SetTargetProperty(animation, "Y");

            return animation;
        }

        private DoubleAnimation CreateOpacityAnimation(FrameworkElement element, double from, double to) {
            var animation = new DoubleAnimation {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, "Opacity");

            return animation;
        }

        private string GetPackagedIconPath(DatabaseElement service) {
            string issuer = service?.Issuer;
            if (string.IsNullOrWhiteSpace(issuer) && !string.IsNullOrWhiteSpace(service?.Label)) {
                int separator = service.Label.IndexOf(':');

                if (separator > 0) {
                    issuer = service.Label.Substring(0, separator);
                }
            }

            if (string.IsNullOrWhiteSpace(issuer)) {
                return null;
            }

            string normalizedIssuer = new string(issuer.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            if (PackagedIconAliases.TryGetValue(normalizedIssuer, out var alias)) {
                normalizedIssuer = alias;
            }

            if (!PackagedIconNames.Contains(normalizedIssuer)) {
                return null;
            }

            string themeSuffix = _isDarkTheme ? "-dark" : string.Empty;
            return $"ms-appx:///Assets/Icons/{normalizedIssuer}-64{themeSuffix}.png";
        }

        private void ReinitializeOtps(IEnumerable<DatabaseElement> services) {
            otps.Clear();

            foreach (var service in services) {
                var secretKey = Base32Encoding.ToBytes(service.Secret);
                var hashMode = GetHashMode(service.Algorithm);

                if (service.Type.Equals("totp", StringComparison.OrdinalIgnoreCase)) {
                    int period = service.Period > 0 ? service.Period : 30;
                    otps[service.Id] = new Totp(secretKey, period, hashMode, service.Digits);
                } else {
                    otps[service.Id] = new Hotp(secretKey, hashMode, service.Digits);
                }
            }
        }

        private OtpHashMode GetHashMode(string algorithm) {
            if (string.Equals(algorithm, "SHA256", StringComparison.OrdinalIgnoreCase)) {
                return OtpHashMode.Sha256;
            }

            if (string.Equals(algorithm, "SHA512", StringComparison.OrdinalIgnoreCase)) {
                return OtpHashMode.Sha512;
            }

            return OtpHashMode.Sha1;
        }

        private async Task ImportServicesAsync(List<DatabaseElement> imported) {
            if (_isLocked) {
                if (imported != null) {
                    foreach (var item in imported) {
                        if (item != null) {
                            item.Secret = null;
                            item.ProtectedSecret = null;
                        }
                    }
                }

                return;
            }

            if (imported == null || !imported.Any()) {
                await ShowMessage(AppText.Get("EmptyImport"));
                return;
            }

            int operationVersion = _lockVersion;

            try {
                var result = await _databaseManager.ImportServicesAsync(imported, () => _isLocked || operationVersion != _lockVersion);
                if (result.Cancelled || _isLocked || operationVersion != _lockVersion) {
                    return;
                }

                await RefreshServicesAsync(true);

                if (!_isLocked && operationVersion == _lockVersion) {
                    await ShowMessage(AppText.Format("ImportSummary", result.ImportedCount, result.DuplicateCount, result.InvalidCount));
                }
            } finally {
                if (imported != null) {
                    foreach (var item in imported) {
                        if (item != null) {
                            item.Secret = null;
                            item.ProtectedSecret = null;
                        }
                    }
                }
            }
        }

        private List<DatabaseElement> DeserializeJson(string content) {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content))) {
                var serializer = new DataContractJsonSerializer(typeof(List<DatabaseElement>));

                return serializer.ReadObject(stream) as List<DatabaseElement> ?? new List<DatabaseElement>();
            }
        }

        private string SerializeJson(List<DatabaseElement> services) {
            using (var stream = new MemoryStream()) {
                var serializer = new DataContractJsonSerializer(typeof(List<DatabaseElement>));
                serializer.WriteObject(stream, services);

                stream.Position = 0;

                using (var reader = new StreamReader(stream)) {
                    return reader.ReadToEnd();
                }
            }
        }

        private string SerializeEncryptedJson(EncryptedBackupEnvelope envelope) {
            using (var stream = new MemoryStream()) {
                var serializer = new DataContractJsonSerializer(typeof(EncryptedBackupEnvelope));
                serializer.WriteObject(stream, envelope);

                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private EncryptedBackupEnvelope TryDeserializeEncryptedJson(string content) {
            try {
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content))) {
                    return new DataContractJsonSerializer(typeof(EncryptedBackupEnvelope)).ReadObject(stream) as EncryptedBackupEnvelope;
                }
            } catch {
                return null;
            }
        }

        private string SerializeEncryptedXml(EncryptedBackupEnvelope envelope) {
            var serializer = new XmlSerializer(typeof(EncryptedBackupEnvelope));

            using (var writer = new Utf8StringWriter()) {
                serializer.Serialize(writer, envelope);
                return writer.ToString();
            }
        }

        private sealed class Utf8StringWriter : StringWriter {
            public override System.Text.Encoding Encoding => new System.Text.UTF8Encoding(false);
        }

        private EncryptedBackupEnvelope TryDeserializeEncryptedXml(string content) {
            try {
                var settings = new XmlReaderSettings {
                    DtdProcessing = DtdProcessing.Prohibit
                };

                using (var input = new StringReader(content)) {
                    using (var reader = XmlReader.Create(input, settings)) {
                        reader.MoveToContent();

                        var root = new XmlRootAttribute(reader.LocalName) {
                            Namespace = reader.NamespaceURI
                        };

                        var serializer = new XmlSerializer(typeof(EncryptedBackupEnvelope), root);
                        return serializer.Deserialize(reader) as EncryptedBackupEnvelope;
                    }
                }
            } catch {
                return null;
            }
        }

        private async Task<string> PromptBackupPasswordAsync(bool confirmPassword) {
            var dialog = new BackupPasswordDialog(confirmPassword);

            ContentDialogResult result = await dialog.ShowAsync();
            string password = result == ContentDialogResult.Primary ? dialog.Passphrase : null;
            string confirmation = result == ContentDialogResult.Primary ? dialog.Confirmation : null;

            dialog.ClearPasswords();

            if (result != ContentDialogResult.Primary) {
                return null;
            }

            if (password.Length < 12) {
                await ShowMessage(AppText.Get("BackupPassphraseMinLength"));
                return null;
            }

            if (confirmPassword && !string.Equals(password, confirmation, StringComparison.Ordinal)) {
                await ShowMessage(AppText.Get("BackupPassphraseMismatch"));
                return null;
            }

            return password;
        }

        private async Task<string> DecryptBackupAsync(EncryptedBackupEnvelope envelope) {
            string password = await PromptBackupPasswordAsync(false);
            if (password == null) {
                return null;
            }

            try {
                return await BackupEncryption.DecryptAsync(envelope, password);
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine(AppText.Format("EncryptedBackupOpenFailureLog", ex.Message));
                await ShowMessage(AppText.Get("BackupDecryptError"));

                return null;
            }
        }

        private async Task ShowMessage(string message) {
            await new MessageDialog(message).ShowAsync();
        }

        private async void EditService_Click(object sender, RoutedEventArgs e) {
            if (_isLocked) {
                return;
            }

            if (TryGetServiceFromSender(sender, out var item)) {
                await EditServiceAsync(item.Id);
            }
        }

        private async void DeleteService_Click(object sender, RoutedEventArgs e) {
            if (_isLocked) {
                return;
            }

            if (TryGetServiceFromSender(sender, out var item)) {
                await DeleteServiceAsync(item.Id);
            }
        }

        private bool TryGetServiceFromSender(object sender, out DatabaseElement item) {
            item = null;

            if (sender is MenuFlyoutItem menuItem && menuItem.CommandParameter is DatabaseElement menuData) {
                item = menuData;
                return true;
            }

            if (sender is FrameworkElement element) {
                if (element.Tag is DatabaseElement tagData) {
                    item = tagData;
                    return true;
                }

                if (element.DataContext is DatabaseElement contextData) {
                    item = contextData;
                    return true;
                }
            }

            return false;
        }


        private async Task EditServiceAsync(int serviceId) {
            if (_isLocked) {
                return;
            }

            int operationVersion = _lockVersion;

            DatabaseElement service = await _databaseManager.GetServiceAsync(serviceId);

            if (_isLocked || operationVersion != _lockVersion) {
                return;
            }

            if (service == null) {
                await ShowMessage(AppText.Get("NotFound"));
                await RefreshServicesAsync(!_isLocked);

                return;
            }

            var dialog = new EditTokenDialog(service.Issuer, service.Label);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) {
                return;
            }

            if (_isLocked || operationVersion != _lockVersion) {
                return;
            }

            bool updated = await _databaseManager.UpdateServiceAsync(serviceId, dialog.Issuer, dialog.Account, () => _isLocked || operationVersion != _lockVersion);

            if (_isLocked || operationVersion != _lockVersion) {
                return;
            }

            if (!updated) {
                await ShowMessage(AppText.Get("NotFound"));
                return;
            }

            await RefreshServicesAsync(!_isLocked);
        }

        private async Task DeleteServiceAsync(int serviceId) {
            if (_isLocked) {
                return;
            }

            int operationVersion = _lockVersion;

            DatabaseElement service = await _databaseManager.GetServiceAsync(serviceId);

            if (_isLocked || operationVersion != _lockVersion) {
                return;
            }

            if (service == null) {
                await ShowMessage(AppText.Get("NotFound"));
                await RefreshServicesAsync(!_isLocked);

                return;
            }

            var name = $"{service.Issuer}\n{service.Label}".Trim();
            var dialog = new DeleteTokenDialog(
                string.IsNullOrWhiteSpace(name) ? AppText.Get("DeleteTokenBody") : name);

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) {
                return;
            }

            if (_isLocked || operationVersion != _lockVersion) {
                return;
            }

            bool deleted = await _databaseManager.DeleteServiceAsync(serviceId, () => _isLocked || operationVersion != _lockVersion);

            if (_isLocked || operationVersion != _lockVersion) {
                return;
            }

            if (!deleted) {
                await ShowMessage(AppText.Get("NotFound"));
                return;
            }

            await RefreshServicesAsync(!_isLocked);
        }

        private void Row_PointerPressed(object sender, PointerRoutedEventArgs e) {
            if (sender is FrameworkElement element && element.DataContext is DatabaseElement model) {
                // If the previous hold opened a flyout without producing a Click, allow this new press to act as a normal tap
                _suppressNextRevealClick.Remove(model);
            }
        }

        private void Button_Holding(object sender, HoldingRoutedEventArgs e) {
            if (sender is FrameworkElement element && element.DataContext is DatabaseElement model) {
                _suppressNextRevealClick.Add(model);
            }

            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }
    }
}
