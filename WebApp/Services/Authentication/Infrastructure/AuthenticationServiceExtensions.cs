using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using WebApp.Models.Identity;
using WebApp.Services.Application;

namespace WebApp.Services.Authentication.Infrastructure;

// Registers portal cookie authentication and Hub API bearer token validation.
public static class AuthenticationServiceExtensions
{
        public static void ConfigureIdentitySettings(this IServiceCollection services, IConfiguration configuration)
        {
            var authenticationSection = configuration.GetSection(PortalAuthenticationOptions.SectionName);
            var securityStampValidationMinutes = Math.Max(
                1,
                authenticationSection.GetValue<int?>(nameof(PortalAuthenticationOptions.SecurityStampValidationMinutes)) ?? 5);

            services.Configure<PortalAuthenticationOptions>(authenticationSection);
            services.Configure<IdentityOptions>(options =>
            {
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@";
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 3;
                options.Lockout.AllowedForNewUsers = true;
            });

            services.Configure<SecurityStampValidatorOptions>(options =>
            {
                options.ValidationInterval = TimeSpan.FromMinutes(securityStampValidationMinutes);
            });
        }

        public static void AddAuthenticationCookie(this IServiceCollection services)
        {
            services.AddAuthentication()
                .AddCookie();
        }

        public static void AddHubApiAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var hubApiSection = configuration.GetSection(HubApiAuthenticationOptions.SectionName);
            var hubApiOptions = hubApiSection.Get<HubApiAuthenticationOptions>() ?? new HubApiAuthenticationOptions();
            services.Configure<HubApiAuthenticationOptions>(hubApiSection);

            if (!hubApiOptions.Enabled)
                return;

            ValidateHubApiAuthenticationOptions(hubApiOptions);

            services.AddAuthentication()
                .AddJwtBearer(HubApiAuthenticationOptions.AuthenticationScheme, options =>
                {
                    options.Authority = hubApiOptions.Authority;
                    options.Audience = hubApiOptions.Audience;
                    options.RequireHttpsMetadata = hubApiOptions.RequireHttpsMetadata;
                    options.TokenValidationParameters = BuildHubApiTokenValidationParameters(hubApiOptions);
                });
        }

        public static void ConfigureApplicationCookie(this IServiceCollection services, IConfiguration configuration)
        {
            var authenticationSection = configuration.GetSection(PortalAuthenticationOptions.SectionName);
            var cookieLifetimeMinutes = Math.Max(
                15,
                authenticationSection.GetValue<int?>(nameof(PortalAuthenticationOptions.CookieLifetimeMinutes)) ?? 60);
            var enforceSecureCookies =
                authenticationSection.GetValue<bool?>(nameof(PortalAuthenticationOptions.EnforceSecureCookies)) ?? true;

            services.ConfigureApplicationCookie(options =>
            {
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
                options.Cookie.Name = "WebSecurityCookie";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = enforceSecureCookies
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(cookieLifetimeMinutes);
                options.Cookie.MaxAge = options.ExpireTimeSpan;
                options.LoginPath = "/Identity/Account/Login";
                options.LogoutPath = "/Identity/Account/Logout";
                options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
                options.SlidingExpiration = true;
                options.EventsType = typeof(PortalCookieEvents);
            });

            services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
                .Configure<PortalAuthenticationTicketStore>((options, ticketStore) =>
                {
                    options.SessionStore = ticketStore;
                });
        }

        private static TokenValidationParameters BuildHubApiTokenValidationParameters(HubApiAuthenticationOptions options)
        {
            var validAudiences = ResolveValidAudiences(options).ToArray();

            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = string.IsNullOrWhiteSpace(options.ValidIssuer) ? null : options.ValidIssuer,
                ValidateAudience = validAudiences.Length > 0,
                ValidAudiences = validAudiences,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(Math.Clamp(options.ClockSkewMinutes, 0, 10))
            };
        }

        private static IEnumerable<string> ResolveValidAudiences(HubApiAuthenticationOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.Audience))
                yield return options.Audience;

            foreach (var audience in options.ValidAudiences.Where(value => !string.IsNullOrWhiteSpace(value)))
                yield return audience;
        }

        private static void ValidateHubApiAuthenticationOptions(HubApiAuthenticationOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Authority))
                throw new InvalidOperationException("Security:HubApi:Authority must be configured when Hub API authentication is enabled.");

            if (!ResolveValidAudiences(options).Any())
                throw new InvalidOperationException("Security:HubApi:Audience or Security:HubApi:ValidAudiences must be configured when Hub API authentication is enabled.");
        }
}
