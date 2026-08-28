using System.Globalization;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using WebApp.Models.SupplierPrice;
using WebApp.Services.ExcelImport;
using WebApp.Services.SupplierPrice;

namespace WebApp.Repositories.TransAutoPrice;

// Bulk inserts Trans Auto price import rows into the dedicated staging table.
public sealed class JeevesTransAutoPriceStagingRepository : ITransAutoPriceStagingRepository
{
    private readonly IExcelImportConnectionResolver _connectionResolver;
    private readonly ILogger<JeevesTransAutoPriceStagingRepository> _logger;

    public JeevesTransAutoPriceStagingRepository(IExcelImportConnectionResolver connectionResolver)
        : this(connectionResolver, Microsoft.Extensions.Logging.Abstractions.NullLogger<JeevesTransAutoPriceStagingRepository>.Instance)
    {
    }

    public JeevesTransAutoPriceStagingRepository(
        IExcelImportConnectionResolver connectionResolver,
        ILogger<JeevesTransAutoPriceStagingRepository> logger)
    {
        _connectionResolver = connectionResolver;
        _logger = logger;
    }

    public async Task<bool> TableExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionResolver.ResolveConnectionString());
        await conn.OpenAsync(cancellationToken);

        await using var command = conn.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID('dbo.q_zu_StagingTransAutoPrice', 'U') IS NULL THEN 0 ELSE 1 END";
        command.CommandTimeout = 30;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture) == 1;
    }

    public async Task BulkInsertAsync(
        IEnumerable<PortalSupplierPriceStagingRow> rows,
        CancellationToken cancellationToken = default)
    {
        var total = Stopwatch.StartNew();
        await using var conn = new SqlConnection(_connectionResolver.ResolveConnectionString());
        var open = Stopwatch.StartNew();
        await conn.OpenAsync(cancellationToken);
        _logger.LogInformation("Trans Auto staging open connection in {ElapsedMs} ms.", open.ElapsedMilliseconds);

        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var bulkWatch = Stopwatch.StartNew();
            using var reader = new SupplierPriceStagingDataReader(rows);
            using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.CheckConstraints, tx)
            {
                DestinationTableName = "dbo.q_zu_StagingTransAutoPrice",
                BatchSize = 1000,
                BulkCopyTimeout = 120
            };

            foreach (var column in SupplierPriceStagingDataReader.Columns)
            {
                bulk.ColumnMappings.Add(column.Name, column.Name);
            }

            await bulk.WriteToServerAsync(reader, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "Trans Auto staging bulk inserted {RowCount} rows in {ElapsedMs} ms.",
                reader.RecordsRead,
                bulkWatch.ElapsedMilliseconds);
            _logger.LogInformation(
                "Trans Auto staging total write completed in {ElapsedMs} ms.",
                total.ElapsedMilliseconds);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

}
