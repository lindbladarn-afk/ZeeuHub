using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync.Presentation;

namespace WebApp.Tests.CustomerSync;

public sealed class CustomerSyncConfigurationPresenterTests
{
    [Fact]
    public void Build_Recognizes_KeyVault_Reference_And_Extracts_Secret_Name()
    {
        var presenter = new CustomerSyncConfigurationPresenter();

        var model = presenter.Build(new CustomerSyncOptions
        {
            Enabled = true,
            Companies =
            {
                new CustomerSyncCompanyOptions
                {
                    CompanyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                    JeevesCompanyCode = 7,
                    HubSpot = new CustomerSyncHubSpotOptions
                    {
                        Token = "@Microsoft.KeyVault(SecretUri=https://example.vault.azure.net/secrets/ZeeU-CustomerSync-HubSpot-Token/123)"
                    }
                }
            }
        });

        var company = Assert.Single(model.Companies);
        Assert.True(company.HasHubSpotConnection);
        Assert.Equal("Kontakt finns", company.HubSpotConnectionLabel);
        Assert.True(company.HubSpotToken.IsConfigured);
        Assert.Equal("Key Vault-referens", company.HubSpotToken.StatusLabel);
        Assert.Equal("success", company.HubSpotToken.StatusTone);
        Assert.Equal("ZeeU-CustomerSync-HubSpot-Token", company.HubSpotToken.SecretName);
        Assert.Equal("Läst från Azure Key Vault", company.HubSpotToken.SourceLabel);
    }

    [Fact]
    public void Build_Recognizes_Direct_Token_Without_Revealing_It()
    {
        var presenter = new CustomerSyncConfigurationPresenter();

        var model = presenter.Build(new CustomerSyncOptions
        {
            Companies =
            {
                new CustomerSyncCompanyOptions
                {
                    CompanyId = Guid.NewGuid(),
                    JeevesCompanyCode = 5,
                    HubSpot = new CustomerSyncHubSpotOptions
                    {
                        Token = "pat-eu1-example"
                    }
                }
            }
        });

        var company = Assert.Single(model.Companies);
        Assert.True(company.HasHubSpotConnection);
        Assert.True(company.HubSpotToken.IsConfigured);
        Assert.Equal("Konfigurerad", company.HubSpotToken.StatusLabel);
        Assert.Equal("success", company.HubSpotToken.StatusTone);
        Assert.Null(company.HubSpotToken.SecretName);
        Assert.Equal("Lagrad direkt i konfigurationen", company.HubSpotToken.SourceLabel);
    }

    [Fact]
    public void Build_Includes_Named_Companies()
    {
        var companyId = Guid.Parse("a13e08ad-ff3e-43b2-afa9-7a105d9946c0");
        var presenter = new CustomerSyncConfigurationPresenter();

        var model = presenter.Build(new CustomerSyncOptions
        {
            NamedCompanies =
            {
                ["ZeeU"] = new CustomerSyncCompanyOptions
                {
                    CompanyId = companyId,
                    JeevesCompanyCode = 1000,
                    HubSpot = new CustomerSyncHubSpotOptions
                    {
                        Token = "pat-eu1-example"
                    }
                }
            }
        });

        var company = Assert.Single(model.Companies);
        Assert.Equal(companyId, company.CompanyId);
        Assert.Equal(1000, company.JeevesCompanyCode);
        Assert.True(company.HubSpotToken.IsConfigured);
        Assert.Equal("ZeeU", company.DisplayName);
        Assert.True(company.HasHubSpotConnection);
    }

    [Fact]
    public void Build_Prefers_Named_Company_Label_When_Both_Entries_Describe_Same_Company()
    {
        var companyId = Guid.Parse("a13e08ad-ff3e-43b2-afa9-7a105d9946c0");
        var presenter = new CustomerSyncConfigurationPresenter();

        var model = presenter.Build(new CustomerSyncOptions
        {
            Companies =
            {
                new CustomerSyncCompanyOptions
                {
                    CompanyId = companyId,
                    JeevesCompanyCode = 9900,
                    HubSpot = new CustomerSyncHubSpotOptions()
                }
            },
            NamedCompanies =
            {
                ["ZeeU"] = new CustomerSyncCompanyOptions
                {
                    CompanyId = companyId,
                    JeevesCompanyCode = 9900,
                    HubSpot = new CustomerSyncHubSpotOptions()
                }
            }
        });

        var company = Assert.Single(model.Companies);
        Assert.Equal("ZeeU", company.DisplayName);
        Assert.Equal(companyId, company.CompanyId);
        Assert.Equal(9900, company.JeevesCompanyCode);
    }
}
