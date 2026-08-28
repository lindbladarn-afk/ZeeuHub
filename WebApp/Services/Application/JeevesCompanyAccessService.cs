using Entities.Application;
using Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebApp.Data;

namespace WebApp.Services.Application;

public sealed class JeevesCompanyAccessService : IJeevesCompanyAccessService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IApplicationUserContextService _applicationUserContextService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<JeevesCompanyAccessService> _logger;

    public JeevesCompanyAccessService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IApplicationUserContextService applicationUserContextService,
        IMemoryCache cache,
        ILogger<JeevesCompanyAccessService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _applicationUserContextService = applicationUserContextService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JeevesCompanyVM>> GetCompaniesAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
    {
        if (sessionUser is null)
            return Array.Empty<JeevesCompanyVM>();

        if (_cache.TryGetValue(BuildCacheKey(sessionUser), out IReadOnlyList<JeevesCompanyVM>? cached) && cached is not null)
            return cached;

        var companies = await ResolveCompaniesInternalAsync(sessionUser, cancellationToken);
        if (companies.Count > 0)
            _cache.Set(BuildCacheKey(sessionUser), companies, CacheDuration);

        return companies;
    }

    public async Task<bool> HasCompanyAccessAsync(UserSession? sessionUser, int companyCode, CancellationToken cancellationToken = default)
    {
        if (companyCode <= 0)
            return false;

        var companies = await GetCompaniesAsync(sessionUser, cancellationToken);
        return companies.Any(x => x.CompanyCode == companyCode);
    }

    public async Task<int?> ResolveCompanyCodeAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
    {
        if (sessionUser?.JeevesActiveCompany is int activeCompany and > 0)
            return activeCompany;

        var companies = await GetCompaniesAsync(sessionUser, cancellationToken);
        return companies.FirstOrDefault(x => x.IsDefault)?.CompanyCode
            ?? companies.FirstOrDefault()?.CompanyCode;
    }

    public async Task<string> ResolveCompanyNameAsync(UserSession? sessionUser, int? companyCode, CancellationToken cancellationToken = default)
    {
        var companies = await GetCompaniesAsync(sessionUser, cancellationToken);
        var matched = companyCode.HasValue
            ? companies.FirstOrDefault(x => x.CompanyCode == companyCode.Value)
            : null;

        return !string.IsNullOrWhiteSpace(matched?.Name)
            ? matched.Name
            : sessionUser?.CompanyName ?? string.Empty;
    }

    public void Store(UserSession? sessionUser, IReadOnlyList<JeevesCompanyVM> companies)
    {
        if (sessionUser is null || companies.Count == 0)
            return;

        _cache.Set(BuildCacheKey(sessionUser), companies, CacheDuration);
    }

    private async Task<IReadOnlyList<JeevesCompanyVM>> ResolveCompaniesInternalAsync(UserSession sessionUser, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var applicationUser = await ResolveApplicationUserAsync(context, sessionUser);
            var companyId = ResolveCompanyId(sessionUser, applicationUser);
            if (companyId is null || companyId == Guid.Empty)
                return Array.Empty<JeevesCompanyVM>();

            var configuredCompanies = await context.CompanyJeevesCompanies!
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId.Value && x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ThenByDescending(x => x.IsDefault)
                .ThenBy(x => x.CompanyCode)
                .Select(x => new JeevesCompanyVM
                {
                    CompanyCode = x.CompanyCode,
                    Name = x.DisplayName,
                    IsDefault = x.IsDefault
                })
                .ToListAsync(cancellationToken);

            var resolvedUserId = applicationUser?.Id ?? sessionUser.UserId;
            var allowedCodes = string.IsNullOrWhiteSpace(resolvedUserId)
                ? new List<int>()
                : await context.UserCompanyAccesses!
                    .AsNoTracking()
                    .Where(x => x.UserId == resolvedUserId)
                    .Select(x => x.CompanyCode)
                        .ToListAsync(cancellationToken);

            if (configuredCompanies.Count > 0)
                return ApplyAllowedCodeFilter(configuredCompanies, allowedCodes);

            // When no explicit company list is configured we stay inside portal-owned fallback data.
            // Runtime Jeeves resolution happens later, when a module actually needs tenant access.
            return ApplyAllowedCodeFilter(BuildFallbackCompanies(sessionUser).ToList(), allowedCodes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Jeeves companies for user {UserId}", sessionUser.UserId);
            return BuildFallbackCompanies(sessionUser);
        }
    }

    private async Task<Models.Identity.ApplicationUser?> ResolveApplicationUserAsync(ApplicationDbContext context, UserSession sessionUser)
    {
        if (!string.IsNullOrWhiteSpace(sessionUser.UserId))
        {
            var byId = await _applicationUserContextService.GetUserByIdAsync(context, sessionUser.UserId);
            if (byId is not null)
                return byId;
        }

        if (!string.IsNullOrWhiteSpace(sessionUser.Email))
            return await _applicationUserContextService.GetUserByEmailAsync(context, sessionUser.Email);

        return null;
    }

    private static Guid? ResolveCompanyId(UserSession sessionUser, Models.Identity.ApplicationUser? applicationUser)
    {
        if (applicationUser?.CompanyId is Guid applicationCompanyId && applicationCompanyId != Guid.Empty)
            return applicationCompanyId;

        if (sessionUser.CompanyId is Guid sessionCompanyId && sessionCompanyId != Guid.Empty)
            return sessionCompanyId;

        return null;
    }

    private static IReadOnlyList<JeevesCompanyVM> ApplyAllowedCodeFilter(
        List<JeevesCompanyVM> companies,
        List<int> allowedCodes)
    {
        if (allowedCodes.Count == 0)
            return companies;

        var allowedSet = allowedCodes.ToHashSet();
        return companies.Where(x => allowedSet.Contains(x.CompanyCode)).ToList();
    }

    private static IReadOnlyList<JeevesCompanyVM> BuildFallbackCompanies(UserSession sessionUser)
    {
        if (sessionUser.JeevesActiveCompany is not int companyCode || companyCode <= 0)
            return Array.Empty<JeevesCompanyVM>();

        return new List<JeevesCompanyVM>
        {
            new()
            {
                CompanyCode = companyCode,
                Name = sessionUser.CompanyName ?? $"Bolag {companyCode}",
                IsDefault = true
            }
        };
    }

    private static string BuildCacheKey(UserSession sessionUser)
        => $"JeevesCompanies:{sessionUser.CompanyId:N}:{sessionUser.UserId}";
}
