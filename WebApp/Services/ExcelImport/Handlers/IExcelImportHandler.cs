using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebApp.Services.ExcelImport;

public interface IExcelImportHandler
{
    string ImportType { get; }
    string DisplayName { get; }
    bool CanHandle(string? importType);
    Task<ExcelImportResult> ImportAsync(IFormFile file, string importedBy, CancellationToken cancellationToken = default);
}
