using WebApp.Services.Integration.CustomerSync.Domain;
using WebApp.Services.Integration.CustomerSync.Mapping;

namespace WebApp.Tests.CustomerSync;

public sealed class CustomerSyncNormalizerTests
{
    [Fact]
    public void Normalize_Removes_Noise_From_Identity_Fields()
    {
        var normalizer = new CustomerSyncNormalizer();

        Assert.Equal("5566778899", normalizer.NormalizeOrganizationNumber(" 556677-8899 "));
        Assert.Equal("ACME AB", normalizer.NormalizeName("  Acme   AB "));
        Assert.Equal("info@example.com", normalizer.NormalizeEmail(" INFO@EXAMPLE.COM "));
        Assert.Equal("+46701234567", normalizer.NormalizePhone(" +46 (0)70-123 45 67 "));
    }

    [Fact]
    public void Mapper_Uses_Same_Normalization_For_Both_Directions()
    {
        var mapper = new CustomerSyncMapper(new CustomerSyncNormalizer());

        var result = mapper.Normalize(new SyncedCustomer(
            JeevesCustomerNumber: "100",
            HubSpotCompanyId: "200",
            HubSpotContactId: null,
            OrganizationNumber: " 556677-8899 ",
            Name: "  Acme   AB ",
            Email: " INFO@EXAMPLE.COM ",
            Phone: " +46 (0)70-123 45 67 ",
            UpdatedAtUtc: null));

        Assert.Equal("5566778899", result.OrganizationNumber);
        Assert.Equal("ACME AB", result.Name);
        Assert.Equal("info@example.com", result.Email);
        Assert.Equal("+46701234567", result.Phone);
    }
}
