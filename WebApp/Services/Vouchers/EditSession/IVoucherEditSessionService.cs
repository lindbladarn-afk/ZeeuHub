using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApp.Models.Voucher;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Vouchers
{
    // Manages editable voucher import sessions before rows are committed to staging.
    public interface IVoucherEditSessionService
    {
        Task<ExcelImportResult> CreateEditSessionFromFileAsync(IFormFile file, string importedBy, int maxRows, DateTime postingDate, DateTime? reversalDate = null, CancellationToken cancellationToken = default);
        Task<ExcelImportResult> CreateEmptyEditSessionAsync(string importedBy, DateTime postingDate, DateTime? reversalDate = null, CancellationToken cancellationToken = default);
        Task<ExcelImportResult> ImportEditedRowsAsync(Guid editSessionId, IReadOnlyList<VoucherEditRowDto> rows, string importedBy, DateTime postingDate, DateTime? reversalDate = null, CancellationToken cancellationToken = default);
    }
}
