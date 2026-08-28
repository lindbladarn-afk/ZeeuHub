using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Text.RegularExpressions;

namespace WebApp.Helpers;

public static class KeyVaultHelper
{
    // Matches: @Microsoft.KeyVault(SecretUri= https://... )
    private static readonly Regex KvRefPattern = new(@"^\s*@Microsoft\.KeyVault\s*\(\s*SecretUri\s*=\s*(?<uri>[^)]+)\)\s*$",RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);


    public static async Task<string> ResolveAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.Trim().Trim('"', '\'');

        // Case 1: @Microsoft.KeyVault(SecretUri=...)
        var m = KvRefPattern.Match(trimmed);
        if (m.Success)
        {
            var uri = m.Groups["uri"].Value.Trim().Trim('"', '\'');
            return await GetSecretValueAsync(uri);
        }

        // Case 2: bare SecretUri
        if (Uri.IsWellFormedUriString(trimmed, UriKind.Absolute) &&
            trimmed.Contains(".vault.azure.net", StringComparison.OrdinalIgnoreCase))
        {
            return await GetSecretValueAsync(trimmed);
        }

        // Case 3: already a plain connection string
        return value;
    }

    private static async Task<string> GetSecretValueAsync(string secretUri)
    {
        var uri = new Uri(secretUri);
        var vaultBase = $"{uri.Scheme}://{uri.Host}";

        // Expect: /secrets/{name}/{version?}
        var parts = uri.Segments.Select(s => s.Trim('/')).Where(s => s.Length > 0).ToArray();
        if (parts.Length < 2 || !parts[0].Equals("secrets", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid Key Vault SecretUri.", nameof(secretUri));

        var name = parts[1];
        var version = parts.Length >= 3 ? parts[2] : null;

        var client = new SecretClient(new Uri(vaultBase), new DefaultAzureCredential());
        var secret = version is null ? await client.GetSecretAsync(name)
                                     : await client.GetSecretAsync(name, version);

        return secret.Value.Value;
    }

}
