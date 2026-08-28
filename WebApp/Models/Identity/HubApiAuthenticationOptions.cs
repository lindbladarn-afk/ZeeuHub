// External Hub API OAuth2 settings for machine-to-machine clients.
namespace WebApp.Models.Identity;

/// <summary>
/// Defines OAuth2/JWT validation settings for external Hub API clients.
/// </summary>
public sealed class HubApiAuthenticationOptions
{
    public const string SectionName = "Security:HubApi";
    public const string AuthenticationScheme = "HubApiBearer";
    public const string AuthorizationPolicy = "HubApi";

    public bool Enabled { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string ValidIssuer { get; set; } = string.Empty;
    public string[] ValidAudiences { get; set; } = [];
    public string[] RequiredScopes { get; set; } = [];
    public string[] RequiredAppRoles { get; set; } = [];
    public bool RequireHttpsMetadata { get; set; } = true;
    public int ClockSkewMinutes { get; set; } = 2;
}
