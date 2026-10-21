using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.System;
using Windows.System.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using OtpNet;
using ZXing;
using Digits.Pages;

namespace Digits {
    public sealed partial class AddPage : Page {
        private const int ScanIntervalMilliseconds = 150;
        private readonly DatabaseManager _databaseManager = new DatabaseManager();

        private MediaCapture _mediaCapture;
        private VideoFrame _scanFrame;
        private byte[] _luminanceBuffer;
        private readonly DisplayRequest _displayRequest = new DisplayRequest();

        private Task _cameraInitializationTask;
        private Task _scanTask;
        private bool _isScannerActive;
        private bool _isPreviewStarted;
        private bool _displayRequestIsActive;
        private bool _rotationPreferenceSaved;
        private DisplayOrientations _previousRotationPreference;
        private bool _invalidQrMessageShown;
        private bool _duplicateQrMessageShown;
        private Task _backgroundStopTask;

        public AddPage() {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            _backgroundStopTask = null;
            _isScannerActive = true;
            _invalidQrMessageShown = false;
            _duplicateQrMessageShown = false;

            _cameraInitializationTask = InitializeCameraAsync();
            await _cameraInitializationTask;

            if (!_isScannerActive || _mediaCapture == null) {
                return;
            }

            _scanTask = ScanQrCodeAsync();
            await _scanTask;
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e) {
            await StopForBackgroundAsync();
            base.OnNavigatedFrom(e);
        }

        public Task StopForBackgroundAsync() {
            if (_backgroundStopTask == null || _backgroundStopTask.IsCompleted) {
                _backgroundStopTask = StopForBackgroundCoreAsync();
            }

            return _backgroundStopTask;
        }

        private async Task StopForBackgroundCoreAsync() {
            _isScannerActive = false;

            try {
                if (_cameraInitializationTask != null) {
                    await _cameraInitializationTask;
                }

                if (_scanTask != null) {
                    await _scanTask;
                }
            } catch (Exception ex) {
                Debug.WriteLine(AppText.Format("ScannerShutdownLog", ex.Message));
            }

            await StopCameraAsync();
        }

        private async Task InitializeCameraAsync() {
            var capture = new MediaCapture();

            try {
                var cameras = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                var backCamera = cameras.FirstOrDefault(camera => camera.EnclosureLocation != null && camera.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Back);

                var settings = new MediaCaptureInitializationSettings {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu
                };

                if (backCamera != null) {
                    settings.VideoDeviceId = backCamera.Id;
                }

                await capture.InitializeAsync(settings);

                if (!_isScannerActive) {
                    capture.Dispose();
                    return;
                }

                _mediaCapture = capture;

                var previewProperties = capture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;
                if (previewProperties == null || previewProperties.Width <= 0 || previewProperties.Height <= 0) {
                    throw new InvalidOperationException(AppText.Get("CameraFrameSizeError"));
                }

                int sourceWidth = (int)previewProperties.Width;
                int sourceHeight = (int)previewProperties.Height;

                _scanFrame = new VideoFrame(BitmapPixelFormat.Gray8, sourceWidth, sourceHeight, BitmapAlphaMode.Ignore);
                _luminanceBuffer = new byte[sourceWidth * sourceHeight];

                _previousRotationPreference = DisplayInformation.AutoRotationPreferences;
                _rotationPreferenceSaved = true;
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
                capture.SetPreviewRotation(VideoRotation.Clockwise90Degrees);

                PreviewElement.Source = capture;
                await capture.StartPreviewAsync();

                _isPreviewStarted = true;
                _displayRequest.RequestActive();
                _displayRequestIsActive = true;

                await ConfigureAutofocusAsync(capture);
            } catch (UnauthorizedAccessException) {
                if (!ReferenceEquals(_mediaCapture, capture)) {
                    capture.Dispose();
                }

                await StopCameraAsync();

                if (_isScannerActive) {
                    var dialog = new MessageDialog(AppText.Get("CameraPermissionBody"), "CameraPermissionTitle", "OpenSettingsButton", "CancelDialogButton");

                    if (await dialog.ShowAsync() == ContentDialogResult.Primary) {
                        await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-webcam"));
                    }
                }
            } catch (Exception ex) {
                if (!ReferenceEquals(_mediaCapture, capture)) {
                    capture.Dispose();
                }

                await StopCameraAsync();

                Debug.WriteLine(AppText.Format("CameraInitializationFailedLog", ex));

                if (_isScannerActive) {
                    var dialog = new MessageDialog(AppText.Format("CameraStartError", ex.Message), "CameraErrorTitle");
                    await dialog.ShowAsync();
                }
            }
        }

        private async Task ConfigureAutofocusAsync(MediaCapture capture) {
            // Let the CaptureElement attach and the first preview frames reach the camera driver before configuring focus
            await Task.Delay(300);

            if (!_isScannerActive || !ReferenceEquals(_mediaCapture, capture)) {
                return;
            }

            var focus = capture.VideoDeviceController.FocusControl;
            if (!focus.Supported) {
                return;
            }

            var supportedModes = focus.SupportedFocusModes;
            FocusMode mode;
            if (supportedModes.Contains(FocusMode.Continuous)) {
                mode = FocusMode.Continuous;
            } else if (supportedModes.Contains(FocusMode.Single)) {
                mode = FocusMode.Single;
            } else if (supportedModes.Contains(FocusMode.Auto)) {
                mode = FocusMode.Auto;
            } else {
                return;
            }

            try {
                await focus.UnlockAsync();

                var focusSettings = new FocusSettings { Mode = mode };
                if (focus.SupportedFocusRanges.Contains(AutoFocusRange.FullRange)) {
                    focusSettings.AutoFocusRange = AutoFocusRange.FullRange;
                } else if (focus.SupportedFocusRanges.Count > 0) {
                    focusSettings.AutoFocusRange = focus.SupportedFocusRanges[0];
                }

                if (focus.WaitForFocusSupported) {
                    focusSettings.WaitForFocus = true;
                }

                // Configure selects the mode; FocusAsync actually starts the focus operation, including for continuous autofocus
                focus.Configure(focusSettings);
                await focus.FocusAsync();
            } catch (Exception ex) {
                // Autofocus support varies by phone and camera driver; scanning can still proceed
                Debug.WriteLine(AppText.Format("AutofocusUnavailableLog", ex.Message));
            }
        }

        private async Task ScanQrCodeAsync() {
            var barcodeReader = new BarcodeReader {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions {
                    TryHarder = false,
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
                }
            };

            while (_isScannerActive && _mediaCapture != null && _scanFrame != null) {
                try {
                    await _mediaCapture.GetPreviewFrameAsync(_scanFrame);

                    // Reuse the destination frame and luminance array to avoid allocating a full camera frame and grayscale bitmap on every scan attempt
                    if (!_isScannerActive) {
                        break;
                    }

                    _scanFrame.SoftwareBitmap.CopyToBuffer(_luminanceBuffer.AsBuffer());
                    var result = barcodeReader.Decode(_luminanceBuffer, _scanFrame.SoftwareBitmap.PixelWidth, _scanFrame.SoftwareBitmap.PixelHeight, RGBLuminanceSource.BitmapFormat.Gray8);

                    if (result != null && result.Text.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase)) {
                        var service = ParseOtpAuthUri(result.Text);
                        if (service == null) {
                            if (!_invalidQrMessageShown) {
                                _invalidQrMessageShown = true;
                                await ShowScannerMessageAsync(AppText.Get("QrUnsupported"));
                            }
                        } else {
                            try {
                                bool added = await _databaseManager.TryAddTokenAsync(service);
                                if (!added) {
                                    if (!_duplicateQrMessageShown) {
                                        _duplicateQrMessageShown = true;
                                        await ShowScannerMessageAsync(AppText.Get("TokenAlreadyAdded"));
                                    }

                                    continue;
                                }
                            } catch (Exception ex) {
                                Debug.WriteLine(AppText.Format("ScannedTokenSaveFailureLog", ex));
                                await ShowScannerMessageAsync(AppText.Get("SaveScannedTokenError"));
                                continue;
                            }

                            _isScannerActive = false;
                            if (Frame.CanGoBack) {
                                Frame.GoBack();
                            }

                            return;
                        }
                    }
                } catch (Exception ex) {
                    Debug.WriteLine(AppText.Format("QrScanFailureLog", ex.Message));
                }

                await Task.Delay(ScanIntervalMilliseconds);
            }
        }

        private DatabaseElement ParseOtpAuthUri(string value) {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var parsedUri) || !string.Equals(parsedUri.Scheme, "otpauth", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            string type = parsedUri.Host?.ToLowerInvariant();
            if (type != "totp" && type != "hotp") {
                return null;
            }

            var service = new DatabaseElement {
                Type = type,
                Label = Uri.UnescapeDataString(parsedUri.AbsolutePath.TrimStart('/')),
                Algorithm = "SHA1",
                Digits = 6,
                Counter = 0,
                Period = 30
            };

            try {
                var query = new WwwFormUrlDecoder(parsedUri.Query);

                foreach (var parameter in query) {
                    switch (parameter.Name.ToLowerInvariant()) {
                        case "secret":
                            service.Secret = parameter.Value?.Replace(" ", string.Empty).Trim();
                            break;
                        case "issuer":
                            service.Issuer = parameter.Value?.Trim();
                            break;
                        case "algorithm":
                            service.Algorithm = parameter.Value?.Trim().ToUpperInvariant();
                            break;
                        case "digits":
                            if (!int.TryParse(parameter.Value, out var digits) || digits < 1 || digits > 10) {
                                return null;
                            }

                            service.Digits = digits;
                            break;
                        case "counter":
                            if (!int.TryParse(parameter.Value, out var counter) || counter < 0) {
                                return null;
                            }

                            service.Counter = counter;
                            break;
                        case "period":
                            if (!int.TryParse(parameter.Value, out var period) || period < 1) {
                                return null;
                            }

                            service.Period = period;
                            break;
                    }
                }
            } catch (Exception ex) {
                Debug.WriteLine(AppText.Format("InvalidOtpUriQueryLog", ex.Message));
                return null;
            }

            if (string.IsNullOrWhiteSpace(service.Secret) || (service.Algorithm != "SHA1" && service.Algorithm != "SHA256" && service.Algorithm != "SHA512")) {
                return null;
            }

            try {
                if (Base32Encoding.ToBytes(service.Secret).Length == 0) {
                    return null;
                }
            } catch {
                return null;
            }

            // Labels may include the issuer before the account name
            int issuerSeparator = service.Label.IndexOf(':');
            if (issuerSeparator > 0) {
                string labelIssuer = service.Label.Substring(0, issuerSeparator).Trim();

                if (string.IsNullOrWhiteSpace(service.Issuer)) {
                    service.Issuer = labelIssuer;
                }

                if (string.Equals(labelIssuer, service.Issuer, StringComparison.OrdinalIgnoreCase)) {
                    service.Label = service.Label.Substring(issuerSeparator + 1);
                }
            }

            if (string.IsNullOrWhiteSpace(service.Label)) {
                service.Label = service.Issuer ?? AppText.Get("GenericTokenLabel");
            }

            return service;
        }

        private async Task ShowScannerMessageAsync(string message) {
            if (!_isScannerActive) {
                return;
            }

            await new MessageDialog(message).ShowAsync();
        }

        private async Task StopCameraAsync() {
            var capture = _mediaCapture;
            _mediaCapture = null;

            if (_isPreviewStarted && capture != null) {
                try {
                    await capture.StopPreviewAsync();
                } catch (Exception ex) {
                    Debug.WriteLine(AppText.Format("StopPreviewFailureLog", ex.Message));
                }
            }

            _isPreviewStarted = false;

            if (PreviewElement != null) {
                PreviewElement.Source = null;
            }

            if (capture != null) {
                capture.Dispose();
            }

            if (_scanFrame != null) {
                _scanFrame.Dispose();
                _scanFrame = null;
            }

            _luminanceBuffer = null;

            if (_displayRequestIsActive) {
                _displayRequest.RequestRelease();
                _displayRequestIsActive = false;
            }

            if (_rotationPreferenceSaved) {
                DisplayInformation.AutoRotationPreferences = _previousRotationPreference;
                _rotationPreferenceSaved = false;
            }
        }

        private void AppBarButton_Close_Click(object sender, RoutedEventArgs e) {
            _isScannerActive = false;

            if (Frame.CanGoBack) {
                Frame.GoBack();
            }
        }
    }
}
