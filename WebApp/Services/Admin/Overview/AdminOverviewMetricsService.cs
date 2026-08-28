using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebApp.Helpers;
using WebApp.Data;
using WebApp.ViewModels.Admin;

namespace WebApp.Services.Admin;

// Computes the summary cards for the admin overview page.
public sealed class AdminOverviewMetricsService : IAdminOverviewMetricsService
{
    private const string CacheKey = "admin-overview:metrics";
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly Repository.Contracts.IAdminCompanyRepository _adminCompanyRepository;
    private readonly WebApp.Services.Telemetry.ITelemetryService _telemetryService;
    private readonly IMemoryCache _cache;

    public AdminOverviewMetricsService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        Repository.Contracts.IAdminCompanyRepository adminCompanyRepository,
        WebApp.Services.Telemetry.ITelemetryService telemetryService,
        IMemoryCache cache)
    {
        _dbContextFactory = dbContextFactory;
        _adminCompanyRepository = adminCompanyRepository;
        _telemetryService = telemetryService;
        _cache = cache;
    }

    public async Task<AdminOverviewViewModel> GetOverviewAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3);
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var companyCount = await _adminCompanyRepository.GetCompanyCountAsync();
            var userCount = await db.Users.CountAsync();
            var (excel, ai, _) = await _telemetryService.GetTotalsAsync(30);
            var totalMinutes = await _telemetryService.GetTotalSessionMinutesAsync();

            return new AdminOverviewViewModel
            {
                CompanyCount = companyCount,
                UserCount = userCount,
                ExcelImports = excel,
                AiQueries = ai,
                SessionMinutes = totalMinutes,
                SessionDurationText = DurationFormatter.ToFriendlyTime(totalMinutes)
            };
        }) ?? new AdminOverviewViewModel();
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
    }
}
