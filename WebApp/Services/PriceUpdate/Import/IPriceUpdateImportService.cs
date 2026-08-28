using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PriceUpdate
{
    public interface IPriceUpdateImportService
    {
        Task<ExcelImportResult> ImportAsync(IFormFile file, string importedBy, CancellationToken cancellationToken = default);
    }
}
