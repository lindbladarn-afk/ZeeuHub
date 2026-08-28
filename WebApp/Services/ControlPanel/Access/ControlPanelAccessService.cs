using Entities.Application;
using Microsoft.Extensions.Options;
using WebApp.Models.ControlPanel;

namespace WebApp.Services.ControlPanel;

// Keeps Control Panel tenant access rules out of MVC controllers.
public sealed class ControlPanelAccessService : IControlPanelAccessService
{
    private readonly ControlPanelOptions _options;

    public ControlPanelAccessService(IOptions<ControlPanelOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAuthorizedTenant(UserSession? sessionUser)
    {
        return sessionUser is not null
            && string.Equals(
                sessionUser.CompanyName?.Trim(),
                _options.AllowedCompanyName.Trim(),
                StringComparison.OrdinalIgnoreCase);
    }
}
