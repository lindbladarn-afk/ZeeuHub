using Entities.ViewModels;

namespace WebApp.Services.Application;

public interface IApplicationMenuService
{
    Task<SideMenuViewModel> GetMenuAsync(Guid companyId, string? userId = null, CancellationToken cancellationToken = default);
    void Invalidate(Guid companyId);
}
