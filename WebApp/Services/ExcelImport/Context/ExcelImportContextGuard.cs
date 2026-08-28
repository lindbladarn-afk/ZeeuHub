namespace WebApp.Services.ExcelImport;

// Rejects staging operations that lack the tenant and user identity needed for safe ownership.
public static class ExcelImportContextGuard
{
    public static ExcelImportUserContext GetRequiredCurrent(IExcelImportContextService contextService)
    {
        ArgumentNullException.ThrowIfNull(contextService);

        var context = contextService.GetCurrent();
        if (context.CompanyId is not Guid companyId || companyId == Guid.Empty)
            throw new InvalidOperationException("Aktivt bolag saknas för Excelimporten.");
        if (!context.ForetagKod.HasValue)
            throw new InvalidOperationException("Aktiv företagskod saknas för Excelimporten.");
        if (string.IsNullOrWhiteSpace(context.UserId))
            throw new InvalidOperationException("Användarkontext saknas för Excelimporten.");

        return context;
    }
}
