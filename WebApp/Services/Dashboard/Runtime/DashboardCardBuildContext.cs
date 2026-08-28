// Carries one request's user, company, data cache, and rendering mode to card providers.
using Entities.Application;
using Microsoft.AspNetCore.Routing;
using WebApp.Services.Application;

namespace WebApp.Services.Dashboard;

public sealed class DashboardCardBuildContext
{
    private readonly LinkGenerator _linkGenerator;

    public DashboardCardBuildContext(
        UserSession? user,
        JeevesRuntimeContext? runtimeContext,
        bool useDemoData,
        bool isSingleCardRequest,
        HttpContext? httpContext,
        DashboardCardDataContext data,
        LinkGenerator linkGenerator)
    {
        User = user;
        RuntimeContext = runtimeContext;
        UseDemoData = useDemoData;
        IsSingleCardRequest = isSingleCardRequest;
        HttpContext = httpContext;
        Data = data;
        _linkGenerator = linkGenerator;
    }

    public UserSession? User { get; }
    public JeevesRuntimeContext? RuntimeContext { get; }
    public bool UseDemoData { get; }
    public bool IsSingleCardRequest { get; }
    public HttpContext? HttpContext { get; }
    public DashboardCardDataContext Data { get; }

    public string? GetRefreshUrl(string cardId)
        => HttpContext is null
            ? null
            : _linkGenerator.GetPathByAction(
                HttpContext,
                action: "DashboardCard",
                controller: "Member",
                values: new { cardId });
}
