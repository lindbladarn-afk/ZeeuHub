using Microsoft.AspNetCore.Authorization;

namespace WebApp.Services.ControlPanel;

// Marker requirement for Control Panel tenant access.
public sealed class ControlPanelAccessRequirement : IAuthorizationRequirement
{
}
