namespace WebApp.Services.ExcelImport;

// Reads the transient Excel import list used on the import page.
public interface IExcelImportRuntimeStatusService
{
    IReadOnlyList<WebApp.Models.Application.SidebarRuntimeStatusItemViewModel> GetRecentItems(Guid? companyId, int take = 5);
    IReadOnlyList<WebApp.Models.Application.SidebarRuntimeStatusItemViewModel> GetRecentSummaries(Guid? companyId, int take = 5);
}

public sealed class ExcelImportRuntimeStatusService : IExcelImportRuntimeStatusService
{
    private readonly IExcelImportTransientStatusStore _store;

    public ExcelImportRuntimeStatusService(IExcelImportTransientStatusStore store)
    {
        _store = store;
    }

    public IReadOnlyList<WebApp.Models.Application.SidebarRuntimeStatusItemViewModel> GetRecentItems(Guid? companyId, int take = 5)
        => companyId is Guid value && value != Guid.Empty
            ? _store.ListRecent(value, take)
            : Array.Empty<WebApp.Models.Application.SidebarRuntimeStatusItemViewModel>();

    public IReadOnlyList<WebApp.Models.Application.SidebarRuntimeStatusItemViewModel> GetRecentSummaries(Guid? companyId, int take = 5)
        => companyId is Guid value && value != Guid.Empty
            ? _store.ListRecentSummaries(value, take)
            : Array.Empty<WebApp.Models.Application.SidebarRuntimeStatusItemViewModel>();
}
