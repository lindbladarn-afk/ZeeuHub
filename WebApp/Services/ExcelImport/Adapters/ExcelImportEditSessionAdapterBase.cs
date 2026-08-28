using System.Text.Json;

namespace WebApp.Services.ExcelImport;

// Provides shared edit-row deserialization and auto-import guards for Excel import adapters.
public abstract class ExcelImportEditSessionAdapterBase<TEditRow> : IExcelImportEditSessionAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public abstract string ImportType { get; }
    public abstract string EditSessionFileName { get; }
    public abstract int MaxEditableRows { get; }

    public abstract Task<ExcelImportResult> CreateEditSessionFromFileAsync(
        Microsoft.AspNetCore.Http.IFormFile file,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default);

    public abstract Task<ExcelImportResult> CreateEmptyEditSessionAsync(
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default);

    public async Task<ExcelImportResult> ImportEditedRowsAsync(
        Guid editSessionId,
        string rowsJson,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default)
    {
        var rows = DeserializeRows(rowsJson);
        if (rows.Count == 0)
            throw new InvalidExcelImportRowsException("Minst en rad måste innehålla data innan import.");
        if (rows.Count > MaxEditableRows)
            throw new EditableImportRowLimitExceededException(rows.Count, MaxEditableRows);

        return await ImportRowsCoreAsync(editSessionId, rows, importedBy, context, cancellationToken);
    }

    public async Task<ExcelImportResult> TryAutoImportEditedRowsAsync(
        ExcelImportResult result,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!CanAutoImport(result))
            return result;

        var rows = result.RowResults
            .Where(row => row.Data != null)
            .Select(CreateRow)
            .ToList();

        return await ImportRowsCoreAsync(result.EditSessionId!.Value, rows, importedBy, context, cancellationToken);
    }

    protected abstract TEditRow CreateRow(ExcelImportRowResult row);

    protected abstract Task<ExcelImportResult> ImportRowsCoreAsync(
        Guid editSessionId,
        IReadOnlyList<TEditRow> rows,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken);

    private static List<TEditRow> DeserializeRows(string rowsJson)
    {
        if (string.IsNullOrWhiteSpace(rowsJson))
            throw new InvalidExcelImportRowsException("Raddata saknas i importen.");

        try
        {
            return JsonSerializer.Deserialize<List<TEditRow>>(rowsJson, JsonOptions)
                   ?? new List<TEditRow>();
        }
        catch (JsonException ex)
        {
            throw new InvalidExcelImportRowsException("Raddata kunde inte tolkas.", ex);
        }
    }

    private static bool CanAutoImport(ExcelImportResult result)
    {
        if (!result.EditSessionId.HasValue)
            return false;
        if (result.InvalidRows > 0 || (result.Errors?.Any() ?? false))
            return false;

        return result.RowResults is { Count: > 0 };
    }
}
