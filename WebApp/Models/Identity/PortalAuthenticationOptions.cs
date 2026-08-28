namespace WebApp.Models.Identity;

public sealed class PortalAuthenticationOptions
{
    public const string SectionName = "Security:Authentication";

    public int CookieLifetimeMinutes { get; set; } = 60;
    public int SessionIdleMinutes { get; set; } = 45;
    public int SecurityStampValidationMinutes { get; set; } = 5;
    public bool EnforceSecureCookies { get; set; } = true;
    public bool SignOutWhenSessionBootstrapFails { get; set; } = true;
}
