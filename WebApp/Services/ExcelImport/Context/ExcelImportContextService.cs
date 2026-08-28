using WebApp.Services;

namespace WebApp.Services.ExcelImport;

// Reads the current portal session context for Excel import staging.
public sealed class ExcelImportContextService : IExcelImportContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExcelImportContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ExcelImportUserContext GetCurrent()
    {
        var sessionUser = _httpContextAccessor.HttpContext?.Session.Get<Entities.Application.UserSession>("UserObject");
        return ExcelImportUserContext.FromSession(sessionUser);
    }
}
