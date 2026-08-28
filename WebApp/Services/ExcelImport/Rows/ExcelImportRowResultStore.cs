using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

namespace WebApp.Services.ExcelImport;

// Persists imported row data separately from live status so large imports can be viewed page by page.
public interface IExcelImportRowResultStore
{
    Task<bool> TableExistsAsync(CancellationToken cancellationToken = default);
    Task BulkInsertAsync(IEnumerable<ExcelImportStoredRowResult> rows, CancellationToken cancellationToken = default);
    Task CleanupOldRowsAsync(int retentionDays = JeevesExcelImportRowResultStore.DefaultRetentionDays, CancellationToken cancellationToken = default);
    Task<ExcelImportStoredRowPage> GetPageAsync(
        Guid companyId,
        string importType,
        Guid importBatchId,
        int page,
        int pageSize,
        bool showOnlyInvalidRows,
        bool showAllRows = false,
        CancellationToken cancellationToken = default);
}

public sealed class ExcelImportStoredRowResult
{
    public required string ImportType { get; init; }
    public Guid ImportBatchId { get; init; }
    public int RowNo { get; init; }
    public bool IsValid { get; init; }
    public Dictionary<string, string> Data { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ErrorMessage { get; init; }
    public DateTime ImportedAt { get; init; }
    public Guid? CompanyId { get; init; }
    public string? UserId { get; init; }
}

public sealed class ExcelImportStoredRowPage
{
    public List<ExcelImportRowResult> Rows { get; init; } = new();
    public int TotalCount { get; init; }
    public int FilteredCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public bool ShowOnlyInvalidRows { get; init; }
    public bool ShowAllRows { get; init; }
}

public sealed class JeevesExcelImportRowResultStore : IExcelImportRowResultStore
{
    public const int DefaultRetentionDays = 7;
    private const string TableName = "dbo.q_zu_StagingExcelImportRowResult";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IExcelImportConnectionResolver _connectionResolver;
    private int _cleanupCompleted;

    public JeevesExcelImportRowResultStore(IExcelImportConnectionResolver connectionResolver)
    {
        _connectionResolver = connectionResolver;
    }

    public async Task<bool> TableExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionResolver.ResolveConnectionString());
        await conn.OpenAsync(cancellationToken);

        var result = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT CASE WHEN OBJECT_ID('{TableName}', 'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: cancellationToken,
            commandTimeout: 30));

        return result == 1;
    }

    public async Task BulkInsertAsync(IEnumerable<ExcelImportStoredRowResult> rows, CancellationToken cancellationToken = default)
    {
        var rowList = rows as IList<ExcelImportStoredRowResult> ?? rows.ToList();
        if (rowList.Count == 0)
            return;

        await using var conn = new SqlConnection(_connectionResolver.ResolveConnectionString());
        await conn.OpenAsync(cancellationToken);

        if (Interlocked.Exchange(ref _cleanupCompleted, 1) == 0)
            await CleanupOldRowsAsync(conn, DefaultRetentionDays, cancellationToken);

        using var table = BuildDataTable(rowList);
        using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.CheckConstraints, null)
        {
            DestinationTableName = TableName,
            BatchSize = 1000,
            BulkCopyTimeout = 120
        };

        foreach (DataColumn column in table.Columns)
        {
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulk.WriteToServerAsync(table, cancellationToken);
    }

    public async Task CleanupOldRowsAsync(int retentionDays = DefaultRetentionDays, CancellationToken cancellationToken = default)
    {
        var safeRetentionDays = Math.Clamp(retentionDays, 1, 365);
        await using var conn = new SqlConnection(_connectionResolver.ResolveConnectionString());
        await conn.OpenAsync(cancellationToken);
        await CleanupOldRowsAsync(conn, safeRetentionDays, cancellationToken);
    }

    public async Task<ExcelImportStoredRowPage> GetPageAsync(
        Guid companyId,
        string importType,
        Guid importBatchId,
        int page,
        int pageSize,
        bool showOnlyInvalidRows,
        bool showAllRows = false,
        CancellationToken cancellationToken = default)
    {
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var totalCount = 0;
        var filteredCount = 0;

        await using var conn = new SqlConnection(_connectionResolver.ResolveConnectionString());
        await conn.OpenAsync(cancellationToken);

        var parameters = new
        {
            CompanyId = companyId,
            ImportType = ExcelImportTypeDefinitions.Normalize(importType),
            ImportBatchId = importBatchId,
            ShowOnlyInvalidRows = showOnlyInvalidRows,
            Offset = 0,
            PageSize = safePageSize
        };

        totalCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1)
              FROM dbo.q_zu_StagingExcelImportRowResult
              WHERE CompanyId = @CompanyId
                AND ImportType = @ImportType
                AND ImportBatchId = @ImportBatchId",
            parameters,
            cancellationToken: cancellationToken,
            commandTimeout: 60));

        filteredCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1)
              FROM dbo.q_zu_StagingExcelImportRowResult
              WHERE CompanyId = @CompanyId
                AND ImportType = @ImportType
                AND ImportBatchId = @ImportBatchId
                AND (@ShowOnlyInvalidRows = 0 OR IsValid = 0)",
            parameters,
            cancellationToken: cancellationToken,
            commandTimeout: 60));

        var totalPages = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)safePageSize));
        var safePage = Math.Clamp(page, 1, totalPages);
        var offset = (safePage - 1) * safePageSize;

        var rows = await conn.QueryAsync<StoredRowRecord>(new CommandDefinition(
            @"SELECT RowNo, IsValid, DataJson, ErrorMessage
              FROM dbo.q_zu_StagingExcelImportRowResult
              WHERE CompanyId = @CompanyId
                AND ImportType = @ImportType
                AND ImportBatchId = @ImportBatchId
                AND (@ShowOnlyInvalidRows = 0 OR IsValid = 0)
              ORDER BY RowNo
              OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            new
            {
                CompanyId = companyId,
                ImportType = ExcelImportTypeDefinitions.Normalize(importType),
                ImportBatchId = importBatchId,
                ShowOnlyInvalidRows = showOnlyInvalidRows,
                Offset = offset,
                PageSize = safePageSize
            },
            cancellationToken: cancellationToken,
            commandTimeout: 60));

        return new ExcelImportStoredRowPage
        {
            Rows = rows.Select(MapRow).ToList(),
            TotalCount = totalCount,
            FilteredCount = filteredCount,
            Page = safePage,
            PageSize = safePageSize,
            TotalPages = totalPages,
            ShowOnlyInvalidRows = showOnlyInvalidRows,
            ShowAllRows = showAllRows
        };
    }

    private static DataTable BuildDataTable(IEnumerable<ExcelImportStoredRowResult> rows)
    {
        var table = new DataTable();
        table.Columns.Add("ImportType", typeof(string));
        table.Columns.Add("ImportBatchId", typeof(Guid));
        table.Columns.Add("RowNo", typeof(int));
        table.Columns.Add("IsValid", typeof(bool));
        table.Columns.Add("DataJson", typeof(string));
        table.Columns.Add("ErrorMessage", typeof(string));
        table.Columns.Add("ImportedAt", typeof(DateTime));
        table.Columns.Add("CompanyId", typeof(Guid));
        table.Columns.Add("UserId", typeof(string));

        foreach (var row in rows)
        {
            var dataRow = table.NewRow();
            dataRow["ImportType"] = ExcelImportTypeDefinitions.Normalize(row.ImportType);
            dataRow["ImportBatchId"] = row.ImportBatchId;
            dataRow["RowNo"] = row.RowNo;
            dataRow["IsValid"] = row.IsValid;
            dataRow["DataJson"] = JsonSerializer.Serialize(row.Data, JsonOptions);
            dataRow["ErrorMessage"] = string.IsNullOrWhiteSpace(row.ErrorMessage) ? DBNull.Value : row.ErrorMessage;
            dataRow["ImportedAt"] = row.ImportedAt;
            dataRow["CompanyId"] = row.CompanyId.HasValue ? row.CompanyId.Value : DBNull.Value;
            dataRow["UserId"] = string.IsNullOrWhiteSpace(row.UserId) ? DBNull.Value : row.UserId;
            table.Rows.Add(dataRow);
        }

        return table;
    }

    private static Task CleanupOldRowsAsync(SqlConnection conn, int retentionDays, CancellationToken cancellationToken)
        => conn.ExecuteAsync(new CommandDefinition(
            @"DELETE FROM dbo.q_zu_StagingExcelImportRowResult
              WHERE ImportedAt < DATEADD(day, -@RetentionDays, SYSUTCDATETIME())",
            new { RetentionDays = retentionDays },
            cancellationToken: cancellationToken,
            commandTimeout: 60));

    private static ExcelImportRowResult MapRow(StoredRowRecord row)
        => new()
        {
            RowNo = row.RowNo,
            IsValid = row.IsValid,
            ErrorMessage = row.ErrorMessage,
            Data = DeserializeData(row.DataJson)
        };

    private static Dictionary<string, string> DeserializeData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed class StoredRowRecord
    {
        public int RowNo { get; set; }
        public bool IsValid { get; set; }
        public string? DataJson { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
