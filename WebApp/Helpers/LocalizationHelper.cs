using Entities.Contracts;
using System.Globalization;

namespace WebApp.Helpers
{
    public static class LocalizationHelper
    {
        public static void SetCulture(IUser user)
        {
            SetCulture(user?.Language);
        }

        public static void SetCulture(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return;

            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture(language);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(language);
        }

        public static void SetLanguageCookie(IUser user)
        {
            
        }
    }
}
