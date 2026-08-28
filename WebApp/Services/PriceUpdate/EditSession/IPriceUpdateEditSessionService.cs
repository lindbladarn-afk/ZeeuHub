using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApp.Models.PriceUpdate;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PriceUpdate
{
    public interface IPriceUpdateEditSessionService
    {
        Task<ExcelImportResult> CreateEditSessionFromFileAsync(IFormFile file, string importedBy, int maxRows, CancellationToken cancellationToken = default);
        Task<ExcelImportResult> CreateEmptyEditSessionAsync(string importedBy, CancellationToken cancellationToken = default);
        Task<ExcelImportResult> ImportEditedRowsAsync(Guid editSessionId, IReadOnlyList<PriceUpdateEditRowDto> rows, string importedBy, CancellationToken cancellationToken = default);
    }
}
