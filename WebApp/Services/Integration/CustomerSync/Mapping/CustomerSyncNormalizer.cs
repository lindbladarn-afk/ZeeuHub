using System.Text;

namespace WebApp.Services.Integration.CustomerSync.Mapping;

// Normalizes customer identity fields before matching and persistence.
public sealed class CustomerSyncNormalizer : ICustomerSyncNormalizer
{
    public string? NormalizeOrganizationNumber(string? value)
    {
        var trimmed = NormalizeWhitespace(value);
        if (trimmed is null)
            return null;

        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(char.ToUpperInvariant(ch));
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    public string? NormalizeName(string? value)
    {
        var trimmed = NormalizeWhitespace(value);
        return trimmed?.ToUpperInvariant();
    }

    public string? NormalizeEmail(string? value)
    {
        var trimmed = NormalizeWhitespace(value);
        return trimmed?.ToLowerInvariant();
    }

    public string? NormalizePhone(string? value)
    {
        var trimmed = NormalizeWhitespace(value)?.Replace("(0)", string.Empty, StringComparison.Ordinal);
        if (trimmed is null)
            return null;

        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsDigit(ch) || ch == '+')
                builder.Append(ch);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static string? NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? null : string.Join(' ', parts);
    }
}
