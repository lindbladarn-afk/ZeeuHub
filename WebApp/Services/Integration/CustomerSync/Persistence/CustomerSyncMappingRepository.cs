using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Integration.CustomerSync;

namespace WebApp.Services.Integration.CustomerSync.Persistence;

// Stores stable customer mappings used by both sync directions.
public sealed class CustomerSyncMappingRepository : ICustomerSyncMappingRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public CustomerSyncMappingRepository(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<CustomerSyncMappingRecord?> FindByJeevesCustomerAsync(
        Guid companyId,
        int jeevesCompanyCode,
        string jeevesCustomerNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jeevesCustomerNumber))
            return null;

        var normalized = jeevesCustomerNumber.Trim();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CustomerSyncMappings!
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.CompanyId == companyId
                    && item.JeevesCompanyCode == jeevesCompanyCode
                    && item.JeevesCustomerNumber == normalized,
                cancellationToken);
    }

    public async Task<CustomerSyncMappingRecord?> FindByHubSpotCompanyAsync(
        Guid companyId,
        string hubSpotCompanyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hubSpotCompanyId))
            return null;

        var normalized = hubSpotCompanyId.Trim();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CustomerSyncMappings!
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.CompanyId == companyId && item.HubSpotCompanyId == normalized,
                cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerSyncMappingRecord>> FindByOrganizationNumberAsync(
        Guid companyId,
        string organizationNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationNumber))
            return Array.Empty<CustomerSyncMappingRecord>();

        var normalized = organizationNumber.Trim();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CustomerSyncMappings!
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.OrganizationNumber == normalized)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountHubSpotMappingsAsync(
        IReadOnlyCollection<Guid> companyIds,
        CancellationToken cancellationToken)
    {
        if (companyIds.Count == 0)
            return 0;

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CustomerSyncMappings!
            .AsNoTracking()
            .Where(item => companyIds.Contains(item.CompanyId) && item.HubSpotCompanyId != null)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerSyncMappingRecord>> ListHubSpotMappingsAsync(
        IReadOnlyCollection<Guid> companyIds,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (companyIds.Count == 0)
            return Array.Empty<CustomerSyncMappingRecord>();

        var safeSkip = Math.Max(0, skip);
        var safeTake = Math.Clamp(take, 1, 100);
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CustomerSyncMappings!
            .AsNoTracking()
            .Where(item => companyIds.Contains(item.CompanyId) && item.HubSpotCompanyId != null)
            .OrderByDescending(item => item.LastSyncedFromHubSpotAtUtc ?? item.UpdatedAtUtc)
            .ThenBy(item => item.NormalizedName)
            .Skip(safeSkip)
            .Take(safeTake)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerSyncMappingRecord> UpsertAsync(
        CustomerSyncMappingRecord mapping,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindTrackedMappingAsync(db, mapping, cancellationToken);

        if (entity is null)
        {
            entity = new CustomerSyncMappingRecord
            {
                Id = mapping.Id == Guid.Empty ? Guid.NewGuid() : mapping.Id,
                CompanyId = mapping.CompanyId,
                JeevesCompanyCode = mapping.JeevesCompanyCode,
                CreatedAtUtc = utcNow
            };
            db.CustomerSyncMappings!.Add(entity);
        }

        entity.JeevesCustomerNumber = Normalize(mapping.JeevesCustomerNumber, 64);
        entity.HubSpotCompanyId = Normalize(mapping.HubSpotCompanyId, 64);
        entity.HubSpotContactId = Normalize(mapping.HubSpotContactId, 64);
        entity.OrganizationNumber = Normalize(mapping.OrganizationNumber, 64);
        entity.NormalizedName = Normalize(mapping.NormalizedName, 256);
        entity.Domain = Normalize(mapping.Domain, 256);
        entity.Email = Normalize(mapping.Email, 256);
        entity.Phone = Normalize(mapping.Phone, 64);
        entity.HubSpotUpdatedAtUtc = mapping.HubSpotUpdatedAtUtc;
        entity.LastSyncedFromJeevesAtUtc = mapping.LastSyncedFromJeevesAtUtc ?? entity.LastSyncedFromJeevesAtUtc;
        entity.LastSyncedFromHubSpotAtUtc = mapping.LastSyncedFromHubSpotAtUtc ?? entity.LastSyncedFromHubSpotAtUtc;
        entity.UpdatedAtUtc = utcNow;

        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private static async Task<CustomerSyncMappingRecord?> FindTrackedMappingAsync(
        ApplicationDbContext db,
        CustomerSyncMappingRecord mapping,
        CancellationToken cancellationToken)
    {
        if (mapping.Id != Guid.Empty)
        {
            var byId = await db.CustomerSyncMappings!.FirstOrDefaultAsync(item => item.Id == mapping.Id, cancellationToken);
            if (byId is not null)
                return byId;
        }

        if (!string.IsNullOrWhiteSpace(mapping.JeevesCustomerNumber))
        {
            var jeevesCustomerNumber = mapping.JeevesCustomerNumber.Trim();
            var byJeeves = await db.CustomerSyncMappings!.FirstOrDefaultAsync(
                item => item.CompanyId == mapping.CompanyId
                    && item.JeevesCompanyCode == mapping.JeevesCompanyCode
                    && item.JeevesCustomerNumber == jeevesCustomerNumber,
                cancellationToken);

            if (byJeeves is not null)
                return byJeeves;
        }

        if (!string.IsNullOrWhiteSpace(mapping.HubSpotCompanyId))
        {
            var hubSpotCompanyId = mapping.HubSpotCompanyId.Trim();
            return await db.CustomerSyncMappings!.FirstOrDefaultAsync(
                item => item.CompanyId == mapping.CompanyId && item.HubSpotCompanyId == hubSpotCompanyId,
                cancellationToken);
        }

        return null;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
