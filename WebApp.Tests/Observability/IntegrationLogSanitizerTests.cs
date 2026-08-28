using System.Net;
using WebApp.Services.Integration;

namespace WebApp.Tests;

// Integration logging tests keep external diagnostics redacted before they are logged or returned.
public sealed class IntegrationLogSanitizerTests
{
    [Fact]
    public void Diagnostic_RedactsSensitiveValuesAndCollapsesWhitespace()
    {
        var input = """
            error: {"token":"abc123","password":"hunter2","message":"  too   much   space  "}
            """;

        var result = IntegrationLogSanitizer.Diagnostic(input);

        Assert.DoesNotContain("abc123", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hunter2", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted]", result, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", result, StringComparison.Ordinal);
        Assert.Contains("too much space", result, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpFailure_IncludesStatusCodeAndSanitizedDiagnostic()
    {
        var result = IntegrationLogSanitizer.HttpFailure(HttpStatusCode.BadGateway, "api_key=secret-value   ");

        Assert.StartsWith("HTTP 502 BadGateway", result, StringComparison.Ordinal);
        Assert.Contains("[redacted]", result, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Authorization: Bearer top-secret", "top-secret")]
    [InlineData("Cookie: session=private-value", "private-value")]
    [InlineData("Server=db;User ID=portal;Password=secret-password;Database=hub", "secret-password")]
    [InlineData("personnummer=198001011234", "198001011234")]
    public void Diagnostic_RedactsHeadersConnectionStringsAndIdentityValues(
        string input,
        string sensitiveValue)
    {
        var result = IntegrationLogSanitizer.Diagnostic(input);

        Assert.Contains("[redacted]", result, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveValue, result, StringComparison.OrdinalIgnoreCase);
    }
}
