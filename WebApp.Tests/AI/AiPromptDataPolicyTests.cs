// Verifies that sensitive database values are excluded from model prompts.
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiPromptDataPolicyTests
{
    private readonly AiPromptDataPolicy _policy = new();

    [Theory]
    [InlineData("Email", "anna@example.com")]
    [InlineData("Telefon", "+46700000000")]
    [InlineData("Personnummer", "199001011234")]
    [InlineData("IBAN", "SE3550000000054910000003")]
    [InlineData("ApiToken", "secret")]
    public void FormatCell_MasksSensitiveColumns(string column, string value)
    {
        Assert.Equal("[MASKERAT]", _policy.FormatCell(column, value));
    }

    [Fact]
    public void FormatCell_PreservesBusinessMeasures()
    {
        Assert.Equal("1250.5", _policy.FormatCell("Revenue", 1250.5m));
    }
}
