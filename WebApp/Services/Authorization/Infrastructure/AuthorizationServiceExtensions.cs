using Microsoft.AspNetCore.Authorization;
using WebApp.Models.ControlPanel;
using WebApp.Models.Identity;
using WebApp.Services.ControlPanel;

namespace WebApp.Services.Authorization.Infrastructure;

// Keeps authorization policies in one place so portal and API rules stay explicit.
public static class AuthorizationServiceExtensions
{
        public static void AddAuthorizationPolicies(this IServiceCollection services, IConfiguration configuration)
        {
            var hubApiOptions = configuration
                .GetSection(HubApiAuthenticationOptions.SectionName)
                .Get<HubApiAuthenticationOptions>() ?? new HubApiAuthenticationOptions();

            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                options.AddPolicy("CanUpdate", policy => policy.RequireClaim("CanUpdate"));
                options.AddPolicy("CanAdd", policy => policy.RequireClaim("CanAdd"));
                options.AddPolicy("CanDelete", policy => policy.RequireClaim("CanDelete"));

                options.AddPolicy("ZeeU", policy => policy.RequireClaim("Company", "ZeeU"));
                options.AddPolicy("CTT", policy => policy.RequireClaim("Company", "CTT"));
                options.AddPolicy("Xvivo", policy => policy.RequireClaim("Company", "Xvivo"));

                options.AddPolicy("Sales", policy => policy.RequireClaim("Department", "Sales"));
                options.AddPolicy("Purchase", policy => policy.RequireClaim("Department", "Purchase"));
                options.AddPolicy("Production", policy => policy.RequireClaim("Department", "Production"));
                options.AddPolicy("IT", policy => policy.RequireClaim("Department", "IT"));

                options.AddPolicy("DashboardProduction", policy => policy.RequireClaim("DashBoard", "Production"));
                options.AddPolicy("DashboardSales", policy => policy.RequireClaim("DashBoard", "Sales"));
                options.AddPolicy(ControlPanelPolicies.Access, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.Requirements.Add(new ControlPanelAccessRequirement());
                });
                options.AddPolicy(HubApiAuthenticationOptions.AuthorizationPolicy, policy =>
                {
                    if (hubApiOptions.Enabled)
                        policy.AuthenticationSchemes.Add(HubApiAuthenticationOptions.AuthenticationScheme);

                    policy.RequireAuthenticatedUser();
                    policy.RequireAssertion(context => HasRequiredHubApiGrant(context, hubApiOptions));
                });
            });
        }

        private static bool HasRequiredHubApiGrant(AuthorizationHandlerContext context, HubApiAuthenticationOptions options)
        {
            if (!options.Enabled)
                return false;

            var requiredScopes = options.RequiredScopes
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var requiredRoles = options.RequiredAppRoles
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (requiredScopes.Count == 0 && requiredRoles.Count == 0)
                return true;

            var tokenScopes = context.User.Claims
                .Where(claim => claim.Type is "scp" or "scope")
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            if (tokenScopes.Any(requiredScopes.Contains))
                return true;

            var tokenRoles = context.User.Claims
                .Where(claim => claim.Type is "roles" or "role")
                .Select(claim => claim.Value);

            return tokenRoles.Any(requiredRoles.Contains);
        }
}
