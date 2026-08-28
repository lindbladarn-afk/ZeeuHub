using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;
using WebApp.Models;
using Microsoft.AspNetCore.Identity;
using WebApp.Models.Identity;
using Entities.Application;
using WebApp.Helpers;
using WebApp.Services;
using Microsoft.Extensions.Options;

namespace WebApp.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IReadOnlyCollection<string> _supportedCultures;

        public HomeController(
            ILogger<HomeController> logger,
            UserManager<ApplicationUser> userManager,
            IOptions<RequestLocalizationOptions> localizationOptions)
        {
            _logger = logger;
            _userManager = userManager;
            _supportedCultures = localizationOptions.Value.SupportedUICultures?
                .Select(culture => culture.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray()
                ?? Array.Empty<string>();
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetLanguage(string culture, string returnUrl = "/")
        {
            if (string.IsNullOrWhiteSpace(culture))
            {
                return LocalRedirect(returnUrl);
            }

            var normalizedCulture = NormalizeSupportedCulture(culture.Trim());
            if (_supportedCultures.Count > 0 &&
                !_supportedCultures.Contains(normalizedCulture, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Rejected unsupported culture '{Culture}'", normalizedCulture);
                return LocalRedirect(returnUrl);
            }

            // Persist on user if signed in
            if (User?.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    user.Language = normalizedCulture;
                    await _userManager.UpdateAsync(user);
                }
            }

            // Uppdatera session så att PortalSessionMiddleware inte skriver över med gammalt språk
            var sessionUser = HttpContext.Session.Get<UserSession>("UserObject");
            if (sessionUser != null)
            {
                sessionUser.Language = normalizedCulture;
                HttpContext.Session.Set("UserObject", sessionUser);
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalizedCulture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });

            return LocalRedirect(returnUrl);
        }

        private string NormalizeSupportedCulture(string culture)
        {
            if (_supportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
                return culture;

            try
            {
                var neutralCulture = CultureInfo.GetCultureInfo(culture).TwoLetterISOLanguageName;
                var supportedCulture = _supportedCultures.FirstOrDefault(supported =>
                    string.Equals(supported, neutralCulture, StringComparison.OrdinalIgnoreCase));

                return supportedCulture ?? culture;
            }
            catch (CultureNotFoundException)
            {
                return culture;
            }
        }
    }
}
