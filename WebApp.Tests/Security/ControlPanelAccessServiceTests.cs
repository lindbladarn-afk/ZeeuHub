using Entities.Application;
using Microsoft.Extensions.Options;
using WebApp.Models.ControlPanel;
using WebApp.Services.ControlPanel;

namespace WebApp.Tests;

public class ControlPanelAccessServiceTests
{
    [Fact]
    public void IsAuthorizedTenant_Allows_ZeeUCompanyName_CaseInsensitive()
    {
        var service = CreateService();
        var sessionUser = new UserSession
        {
            UserId = "user-1",
            CompanyName = "zeeu ab"
        };

        var result = service.IsAuthorizedTenant(sessionUser);

        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Manufacturer Inc")]
    public void IsAuthorizedTenant_Blocks_MissingOrDifferentCompanyName(string? companyName)
    {
        var service = CreateService();
        var sessionUser = companyName is null
            ? null
            : new UserSession
            {
                UserId = "user-1",
                CompanyName = companyName
            };

        var result = service.IsAuthorizedTenant(sessionUser);

        Assert.False(result);
    }

    [Fact]
    public void IsAuthorizedTenant_Allows_WhitespaceAroundCompanyName()
    {
        var service = CreateService();
        var sessionUser = new UserSession
        {
            UserId = "user-1",
            CompanyName = "  ZeeU AB  "
        };

        var result = service.IsAuthorizedTenant(sessionUser);

        Assert.True(result);
    }

    private static ControlPanelAccessService CreateService(string allowedCompanyName = "ZeeU AB")
    {
        return new ControlPanelAccessService(
            Options.Create(new ControlPanelOptions
            {
                AllowedCompanyName = allowedCompanyName
            }));
    }
}
