namespace WebApp.Services.Integration.CustomerSync.Mapping;

public interface ICustomerSyncNormalizer
{
    string? NormalizeOrganizationNumber(string? value);
    string? NormalizeName(string? value);
    string? NormalizeEmail(string? value);
    string? NormalizePhone(string? value);
}
