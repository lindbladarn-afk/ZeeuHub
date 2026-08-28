using Entities.Application;

namespace WebApp.Services.ExcelImport;

// Provides the portal user and company context needed when Excel import rows are staged.
public interface IExcelImportContextService
{
    ExcelImportUserContext GetCurrent();
}

public sealed class ExcelImportUserContext
{
    public Guid? CompanyId { get; init; }
    public int? ForetagKod { get; init; }
    public string? UserId { get; init; }

    public static ExcelImportUserContext FromSession(UserSession? sessionUser)
    {
        return new ExcelImportUserContext
        {
            CompanyId = sessionUser?.CompanyId,
            ForetagKod = sessionUser?.JeevesActiveCompany,
            UserId = sessionUser?.UserId
        };
    }
}
