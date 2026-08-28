using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApp.Models.Budget;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Budget
{
    public interface IBudgetEditSessionService
    {
        Task<ExcelImportResult> CreateEditSessionFromFileAsync(IFormFile file, string importedBy, int maxRows, CancellationToken cancellationToken = default);
        Task<ExcelImportResult> CreateEmptyEditSessionAsync(string importedBy, CancellationToken cancellationToken = default);
        Task<ExcelImportResult> ImportEditedRowsAsync(Guid editSessionId, IReadOnlyList<BudgetEditRowDto> rows, string importedBy, CancellationToken cancellationToken = default);
    }
}
