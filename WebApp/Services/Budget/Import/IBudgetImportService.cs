using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Budget
{
    public interface IBudgetImportService
    {
        Task<ExcelImportResult> ImportAsync(IFormFile file, string importedBy, CancellationToken cancellationToken = default);
    }
}
