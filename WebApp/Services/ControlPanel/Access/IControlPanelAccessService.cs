using Entities.Application;

namespace WebApp.Services.ControlPanel;

// Defines access checks for the internal Control Panel module.
public interface IControlPanelAccessService
{
    bool IsAuthorizedTenant(UserSession? sessionUser);
}
