using Entities.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using Repository.Contracts;

namespace WebApp.Services.Application;

public sealed class ApplicationMenuService : IApplicationMenuService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly IApplicationMenuRepository _applicationMenuRepository;
    private readonly IMemoryCache _cache;
    private readonly IUserPermissionAccessService _userPermissionAccessService;

    public ApplicationMenuService(
        IApplicationMenuRepository applicationMenuRepository,
        IMemoryCache cache,
        IUserPermissionAccessService userPermissionAccessService)
    {
        _applicationMenuRepository = applicationMenuRepository;
        _cache = cache;
        _userPermissionAccessService = userPermissionAccessService;
    }

    public async Task<SideMenuViewModel> GetMenuAsync(Guid companyId, string? userId = null, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return new SideMenuViewModel();

        var cacheKey = BuildCacheKey(companyId);
        if (_cache.TryGetValue(cacheKey, out SideMenuViewModel? cached) && cached is not null)
            return await _userPermissionAccessService.ApplyToMenuAsync(cached, companyId, userId, cancellationToken);

        var menu = await _applicationMenuRepository.GetMenuAsync(companyId);
        _cache.Set(cacheKey, menu, CacheDuration);
        return await _userPermissionAccessService.ApplyToMenuAsync(menu, companyId, userId, cancellationToken);
    }

    public void Invalidate(Guid companyId)
    {
        if (companyId == Guid.Empty)
            return;

        _cache.Remove(BuildCacheKey(companyId));
    }

    private static string BuildCacheKey(Guid companyId)
        => $"PortalMenu:{companyId:N}";
}
