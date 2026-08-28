using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Services.Application.AI.Billing;

/// <summary>
/// Contracts for AI invoice export logic.
/// Handles period-based invoice rows and Excel generation without coupling to UI controllers.
/// </summary>
public sealed class AiInvoiceExportSnapshot
{
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public IReadOnlyCollection<AiInvoiceExportRow> Rows { get; set; } = Array.Empty<AiInvoiceExportRow>();
}

public sealed class AiInvoiceExportRow
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = "-";
    public int UsedTokens { get; set; }
    public int ExtraTokens { get; set; }
    public decimal BaseCostSek { get; set; }
    public decimal SurchargeSek { get; set; }
    public decimal TotalBillableSek { get; set; }
}

public interface IAiInvoiceExportService
{
    Task<AiInvoiceExportSnapshot> GetSnapshotAsync(DateTime periodStartUtc, CancellationToken ct = default);
    Task<(byte[] Content, string FileName)> ExportAllAsync(DateTime periodStartUtc, CancellationToken ct = default);
    Task<(byte[] Content, string FileName)> ExportCompanyAsync(DateTime periodStartUtc, Guid companyId, CancellationToken ct = default);
    byte[] BuildWorkbook(IReadOnlyCollection<AiInvoiceExportRow> rows, bool includeTotalsRow);
}
