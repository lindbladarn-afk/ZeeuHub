using Dapper;
using Repository.Execution;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.SupplierInvoices;

// Keeps Jonas' supplier-payment invoice selection in hub-owned SQL instead of a Jeeves stored procedure.
public sealed class BankReconciliationSupplierInvoiceRepository : IBankReconciliationSupplierInvoiceRepository
{
    private const string GetSupplierInvoiceCandidatesSql = @"
SELECT
    *
INTO #SupplierInvoiceCandidates
FROM (
    SELECT
        CAST(lrfb.ForetagKod AS int) AS CompanyCode,
        CAST(lrfb.UtbServJournNr AS int) AS PaymentJournalNumber,
        CAST(lrfb.BestRkgNr AS nvarchar(50)) AS InvoiceNumber,
        CAST(lrfb.BetalasTillNr AS nvarchar(50)) AS PayeeId,
        CAST(COALESCE(fr.FtgNamn, N'') AS nvarchar(255)) AS PayeeName,
        CAST(COALESCE(lrfb.VbFbsBelopp, 0) AS decimal(18, 2)) AS PaymentAmount,
        CAST(COALESCE(lrfb.ValKod, N'SEK') AS nvarchar(10)) AS PaymentCurrencyCode,
        CAST(lr.LevFaktDat AS datetime) AS InvoiceDate,
        CAST(lrfb.LrFaktFFDat AS datetime) AS PreferredPaymentDate,
        CAST(COALESCE(lr.vb_faktsum, lrfb.VbFbsBelopp, 0) AS decimal(18, 2)) AS InvoiceAmount,
        CAST(COALESCE(lr.ValKod, lrfb.ValKod, N'SEK') AS nvarchar(10)) AS InvoiceCurrencyCode
    FROM dbo.lrfb lrfb WITH (READUNCOMMITTED)
    INNER JOIN dbo.lr lr WITH (READUNCOMMITTED)
        ON lr.ForetagKod = lrfb.ForetagKod
       AND lr.FtgNr = lrfb.FtgNr
       AND lr.BestRkgNr = lrfb.BestRkgNr
    INNER JOIN dbo.fr fr WITH (READUNCOMMITTED)
        ON fr.ForetagKod = lrfb.ForetagKod
       AND fr.FtgNr = lrfb.FtgNr
    INNER JOIN dbo.jfbs jfbs WITH (READUNCOMMITTED)
        ON jfbs.ForetagKod = lrfb.ForetagKod
       AND jfbs.UtbServJournNr = lrfb.UtbServJournNr
    WHERE (@CompanyCode IS NULL OR lrfb.ForetagKod = @CompanyCode)
      AND (@PaymentJournalNumber IS NULL OR lrfb.UtbServJournNr = @PaymentJournalNumber)
      AND (@InvoiceNumber IS NULL OR lrfb.BestRkgNr = @InvoiceNumber)
      AND (@SourceTimestamp IS NULL OR lrfb.StrDateTime = @SourceTimestamp)
      AND COALESCE(lrfb.makulerad, 0) <> 1
      AND jfbs.utbfilecreated = 1
      AND jfbs.banksanddat IS NOT NULL
      AND lr.levfaktstatkod < 8
) candidates;

SELECT COUNT(1) AS TotalCount
FROM #SupplierInvoiceCandidates;

SELECT *
FROM #SupplierInvoiceCandidates
ORDER BY PreferredPaymentDate ASC, InvoiceNumber ASC
OFFSET @OffsetRows ROWS FETCH NEXT @PageSize ROWS ONLY;

DROP TABLE #SupplierInvoiceCandidates;";

    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

    public BankReconciliationSupplierInvoiceRepository(IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public async Task<BankReconciliationSupplierInvoiceResult> GetPaymentCandidatesAsync(
        string connectionString,
        BankReconciliationSupplierInvoiceQuery query,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(query.Page.GetValueOrDefault(1), 1);
        var safePageSize = Math.Clamp(query.PageSize.GetValueOrDefault(500), 1, 10_000);
        var offsetRows = (safePage - 1) * safePageSize;

        return await _jeevesSqlExecutor.WithConnectionAsync(
            connectionString,
            async connection =>
            {
                var command = new CommandDefinition(
                    GetSupplierInvoiceCandidatesSql,
                    new
                    {
                        query.CompanyCode,
                        query.PaymentJournalNumber,
                        query.InvoiceNumber,
                        query.SourceTimestamp,
                        OffsetRows = offsetRows,
                        PageSize = safePageSize
                    },
                    commandTimeout: 30,
                    cancellationToken: cancellationToken);

                using var multi = await connection.QueryMultipleAsync(command);
                var totalCount = await multi.ReadFirstOrDefaultAsync<int>();
                var invoices = (await multi.ReadAsync<BankReconciliationSupplierInvoiceRow>()).ToList();

                return new BankReconciliationSupplierInvoiceResult
                {
                    Invoices = invoices,
                    TotalCount = totalCount
                };
            },
            operationName: "BankReconciliationSupplierInvoiceRepository.GetPaymentCandidates");
    }
}
