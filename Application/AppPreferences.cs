using Windows.Storage;

namespace Digits {
    public static class AppPreferences {
        private const string AppLockEnabledKey = "AppLockEnabled";

        public static bool IsAppLockEnabled {
            get {
                object value;
                return ApplicationData.Current.LocalSettings.Values.TryGetValue(AppLockEnabledKey, out value) && value is bool && (bool)value;
            }
            set {
                ApplicationData.Current.LocalSettings.Values[AppLockEnabledKey] = value;
            }
        }

        public static bool LockOnNextMainPageNavigation {
            get;
            set;
        }
    }
}
