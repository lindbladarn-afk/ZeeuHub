using Entities.Application;
using WebApp.Services;

namespace WebApp.Services.Purchase.Demo;

// Stores the purchase demo switch in the authenticated user's session.
public sealed class PurchaseDemoModeService : IPurchaseDemoModeService
{
    private const string SessionKeyPrefix = "Purchase.DemoMode";
    private const string AdminRoleName = "Administrator";

    private readonly IHttpContextAccessor _contextAccessor;

    public PurchaseDemoModeService(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public bool IsEnabled()
    {
        if (!IsAdministrator())
            return false;

        var companyId = ResolveCompanyId();
        if (companyId is null)
            return false;

        return _contextAccessor.HttpContext?.Session.GetString(BuildSessionKey(companyId.Value)) == "1";
    }

    public void SetEnabled(bool enabled)
    {
        if (!IsAdministrator())
            return;

        var companyId = ResolveCompanyId();
        if (companyId is null)
            return;

        _contextAccessor.HttpContext?.Session.SetString(BuildSessionKey(companyId.Value), enabled ? "1" : "0");
    }

    private Guid? ResolveCompanyId()
    {
        var sessionUser = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
        if (sessionUser?.CompanyId is not Guid companyId || companyId == Guid.Empty)
            return null;

        return companyId;
    }

    private static string BuildSessionKey(Guid companyId)
        => $"{SessionKeyPrefix}.{companyId:N}";

    private bool IsAdministrator()
        => _contextAccessor.HttpContext?.User?.IsInRole(AdminRoleName) == true;
}
