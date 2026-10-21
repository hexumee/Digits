using System.Globalization;
using Windows.ApplicationModel.Resources;

namespace Digits {
    internal static class AppText {
        private static readonly ResourceLoader Resources = ResourceLoader.GetForViewIndependentUse();

        internal static string Get(string key) {
            return Resources.GetString(key);
        }

        internal static string Format(string key, params object[] arguments) {
            return string.Format(CultureInfo.CurrentUICulture, Get(key), arguments);
        }
    }
}
