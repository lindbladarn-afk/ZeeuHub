using System.Runtime.CompilerServices;
using WebApp.Models.Integration.CustomerSync;

namespace WebApp.Services.Integration.CustomerSync;

// Normalizes CustomerSync companies so aliases and base entries resolve to one visible company.
internal static class CustomerSyncCompanyCatalog
{
    public static IReadOnlyList<CompanyEntry> GetCompanies(CustomerSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var companies = new Dictionary<string, CompanyEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var company in options.Companies)
        {
            var key = GetCompanyKey(company);
            companies.TryAdd(key, new CompanyEntry(BuildDefaultDisplayName(company), company));
        }

        foreach (var namedCompany in options.NamedCompanies)
        {
            var key = GetCompanyKey(namedCompany.Value);
            companies[key] = new CompanyEntry(namedCompany.Key, namedCompany.Value);
        }

        return companies.Values.ToArray();
    }

    public static IReadOnlyList<CustomerSyncCompanyOptions> GetUniqueCompanyOptions(CustomerSyncOptions options)
        => GetCompanies(options).Select(item => item.Company).ToArray();

    private static string GetCompanyKey(CustomerSyncCompanyOptions company)
    {
        if (company.CompanyId != Guid.Empty)
            return $"company:{company.CompanyId:N}";

        if (company.JeevesCompanyCode > 0)
            return $"jeeves:{company.JeevesCompanyCode}";

        return $"invalid:{RuntimeHelpers.GetHashCode(company)}";
    }

    private static string BuildDefaultDisplayName(CustomerSyncCompanyOptions company)
    {
        if (company.JeevesCompanyCode > 0)
            return $"Bolag {company.JeevesCompanyCode}";

        if (company.CompanyId != Guid.Empty)
            return $"Bolag {company.CompanyId.ToString("N")[..8]}";

        return "Okänt bolag";
    }

    internal sealed record CompanyEntry(string DisplayName, CustomerSyncCompanyOptions Company);
}
