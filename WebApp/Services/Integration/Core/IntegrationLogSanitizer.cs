using System.Text.RegularExpressions;

namespace WebApp.Services.Integration;

// Sanitizes external integration diagnostics before they reach application logs or user-facing error text.
public static partial class IntegrationLogSanitizer
{
    private const int MaxDiagnosticLength = 300;
    private static readonly string[] SensitiveTerms =
    [
        "token",
        "secret",
        "password",
        "authorization",
        "api_key",
        "apikey",
        "access_token",
        "refresh_token",
        "connectionstring",
        "connection_string"
    ];

    public static string HttpFailure(System.Net.HttpStatusCode statusCode, string? body)
    {
        var diagnostic = Diagnostic(body);
        return string.IsNullOrWhiteSpace(diagnostic)
            ? $"HTTP {(int)statusCode} {statusCode}"
            : $"HTTP {(int)statusCode} {statusCode}: {diagnostic}";
    }

    public static string Diagnostic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sanitized = SensitiveJsonValueRegex().Replace(value, "$1\"[redacted]\"");
        sanitized = SensitiveFormValueRegex().Replace(sanitized, "$1[redacted]");
        sanitized = SensitiveHeaderValueRegex().Replace(sanitized, "$1[redacted]");
        sanitized = SensitiveConnectionStringValueRegex().Replace(sanitized, "$1[redacted]");
        sanitized = SensitiveIdentityValueRegex().Replace(sanitized, "$1[redacted]");
        sanitized = CollapseWhitespaceRegex().Replace(sanitized, " ").Trim();

        return sanitized.Length <= MaxDiagnosticLength
            ? sanitized
            : sanitized[..MaxDiagnosticLength];
    }

    public static bool LooksSensitive(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return SensitiveTerms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("(\"(?:access_token|refresh_token|token|secret|client_secret|password|authorization|api_key|apikey|connectionString|connection_string)\"\\s*:\\s*)\"[^\"]*\"", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveJsonValueRegex();

    [GeneratedRegex("((?:access_token|refresh_token|token|secret|client_secret|password|authorization|api_key|apikey|connectionString|connection_string)=)[^&\\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveFormValueRegex();

    [GeneratedRegex("((?:authorization|cookie|set-cookie)\\s*:\\s*)[^\\r\\n]+", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveHeaderValueRegex();

    [GeneratedRegex("((?:password|pwd|user id|uid|accountkey|sharedaccesskey|clientsecret)\\s*=\\s*)[^;\\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveConnectionStringValueRegex();

    [GeneratedRegex("((?:personnummer|personalnumber|bankid)\\s*[:=]\\s*)[0-9]{6,8}[-+]?[0-9]{4}", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveIdentityValueRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex CollapseWhitespaceRegex();
}
