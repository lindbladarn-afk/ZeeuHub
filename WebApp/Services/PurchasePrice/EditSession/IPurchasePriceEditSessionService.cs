using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApp.Models.PurchasePrice;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PurchasePrice
{
    public interface IPurchasePriceEditSessionService
    {
        Task<ExcelImportResult> CreateEditSessionFromFileAsync(IFormFile file, string importedBy, int maxRows, CancellationToken cancellationToken = default);
        Task<ExcelImportResult> CreateEmptyEditSessionAsync(string importedBy, CancellationToken cancellationToken = default);
        Task<ExcelImportResult> ImportEditedRowsAsync(Guid editSessionId, IReadOnlyList<PurchasePriceEditRowDto> rows, string importedBy, CancellationToken cancellationToken = default);
    }
}
