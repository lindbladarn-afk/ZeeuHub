using ClosedXML.Excel;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Services.Application.AI.Quota;

namespace WebApp.Services.Application.AI.Billing;

/// <summary>
/// Builds period-based invoice data and Excel files for AI token billing exports.
/// </summary>
public sealed class AiInvoiceExportService : IAiInvoiceExportService
{
    private readonly IAiQuotaAdminService _aiQuotaAdminService;

    public AiInvoiceExportService(IAiQuotaAdminService aiQuotaAdminService)
    {
        _aiQuotaAdminService = aiQuotaAdminService;
    }

    public async Task<AiInvoiceExportSnapshot> GetSnapshotAsync(DateTime periodStartUtc, CancellationToken ct = default)
    {
        var quotaSnapshot = await _aiQuotaAdminService.GetSnapshotAsync(periodStartUtc, ct);
        return new AiInvoiceExportSnapshot
        {
            PeriodYear = quotaSnapshot.PeriodYear,
            PeriodMonth = quotaSnapshot.PeriodMonth,
            PeriodStartUtc = quotaSnapshot.PeriodStartUtc,
            Rows = quotaSnapshot.Companies
                .OrderBy(x => x.CompanyName)
                .Select(x => new AiInvoiceExportRow
                {
                    CompanyId = x.CompanyId,
                    CompanyName = x.CompanyName,
                    UsedTokens = x.UsedTokensCurrentPeriod,
                    ExtraTokens = x.PaidExtraTokensCurrentPeriod,
                    BaseCostSek = x.PaidExtraBaseCostSekCurrentPeriod,
                    SurchargeSek = x.PaidExtraRevenueSekCurrentPeriod,
                    TotalBillableSek = x.PaidExtraBillableSekCurrentPeriod
                })
                .ToList()
        };
    }

    public async Task<(byte[] Content, string FileName)> ExportAllAsync(DateTime periodStartUtc, CancellationToken ct = default)
    {
        var snapshot = await GetSnapshotAsync(periodStartUtc, ct);
        var bytes = BuildWorkbook(snapshot.Rows, includeTotalsRow: true);
        return (bytes, $"ai-fakturaunderlag-{snapshot.PeriodStartUtc:yyyy-MM}.xlsx");
    }

    public async Task<(byte[] Content, string FileName)> ExportCompanyAsync(DateTime periodStartUtc, Guid companyId, CancellationToken ct = default)
    {
        var snapshot = await GetSnapshotAsync(periodStartUtc, ct);
        var row = snapshot.Rows.FirstOrDefault(x => x.CompanyId == companyId);
        var rows = row is null ? Array.Empty<AiInvoiceExportRow>() : new[] { row };
        var safeCompanyName = row?.CompanyName ?? "okant-bolag";
        var fileSafeSlug = ToFileSlug(safeCompanyName);
        var bytes = BuildWorkbook(rows, includeTotalsRow: false);
        return (bytes, $"ai-fakturaunderlag-{fileSafeSlug}-{snapshot.PeriodStartUtc:yyyy-MM}.xlsx");
    }

    public byte[] BuildWorkbook(IReadOnlyCollection<AiInvoiceExportRow> rows, bool includeTotalsRow)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Fakturaunderlag");

        var headers = new[]
        {
            "Bolag",
            "Använt (antal)",
            "Extra tokens",
            "Bas-kostnad (kr)",
            "Intäkt påslag (kr)",
            "Total att fakturera (kr)"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var rowIndex = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.CompanyName;
            sheet.Cell(rowIndex, 2).Value = row.UsedTokens;
            sheet.Cell(rowIndex, 3).Value = row.ExtraTokens;
            sheet.Cell(rowIndex, 4).Value = row.BaseCostSek;
            sheet.Cell(rowIndex, 5).Value = row.SurchargeSek;
            sheet.Cell(rowIndex, 6).Value = row.TotalBillableSek;
            rowIndex++;
        }

        if (rowIndex == 2)
        {
            sheet.Cell(2, 1).Value = "Inga rader för vald period.";
        }
        else if (includeTotalsRow)
        {
            sheet.Cell(rowIndex, 1).Value = "Totalt";
            sheet.Cell(rowIndex, 1).Style.Font.Bold = true;
            sheet.Cell(rowIndex, 4).FormulaA1 = $"SUM(D2:D{rowIndex - 1})";
            sheet.Cell(rowIndex, 5).FormulaA1 = $"SUM(E2:E{rowIndex - 1})";
            sheet.Cell(rowIndex, 6).FormulaA1 = $"SUM(F2:F{rowIndex - 1})";
            sheet.Row(rowIndex).Style.Font.Bold = true;
        }

        sheet.Column(4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(5).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(6).Style.NumberFormat.Format = "#,##0.00";
        sheet.Columns(1, headers.Length).AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string ToFileSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "bolag";

        var normalized = input.Trim().ToLowerInvariant();
        var cleaned = string.Concat(normalized.Select(ch =>
            char.IsLetterOrDigit(ch) ? ch : '-'));
        while (cleaned.Contains("--", StringComparison.Ordinal))
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);

        cleaned = cleaned.Trim('-');
        return string.IsNullOrWhiteSpace(cleaned) ? "bolag" : cleaned;
    }
}
