using System.Text.Encodings.Web;
using System.Text.Json;
using WebApp.Models.Budget;
using WebApp.Repositories.Budget;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Budget;

// Configures editable budget rows for the shared edit-session engine.
public sealed class BudgetEditSessionService : IBudgetEditSessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IBudgetStagingRepository _repository;
    private readonly IExcelImportEditSessionEngine _engine;
    private readonly ExcelImportEditTemplate<PortalBudgetStagingRow> _template;

    public BudgetEditSessionService(
        IBudgetStagingRepository repository,
        IBudgetStagingRowFactory stagingRowFactory,
        IExcelImportEditSessionEngine engine)
    {
        _repository = repository;
        _engine = engine;
        _template = new ExcelImportEditTemplate<PortalBudgetStagingRow>
        {
            ImportType = "budget",
            WorkbookDefinition = BudgetValidation.WorkbookDefinition,
            Headers = BudgetValidation.ExpectedHeaders,
            ValidateRow = BudgetValidation.ValidateRowData,
            BuildRowSnapshot = BudgetValidation.BuildRowSnapshot,
            HasAnyValue = BudgetValidation.HasAnyValue,
            NormalizeValue = (_, value) => value.Trim(),
            CreateStagingRow = context => stagingRowFactory.Create(new BudgetStagingRowCreateRequest
            {
                ImportBatchId = context.ImportBatchId,
                RowNo = context.RowNo,
                RawJsonData = context.NonEmptyRowData,
                JsonOptions = JsonOptions,
                ImportedBy = context.ImportedBy,
                UserContext = context.UserContext
            })
        };
    }

    public Task<ExcelImportResult> CreateEditSessionFromFileAsync(
        IFormFile file,
        string importedBy,
        int maxRows,
        CancellationToken cancellationToken = default)
        => _engine.CreateFromFileAsync(file, _template, maxRows, cancellationToken);

    public Task<ExcelImportResult> CreateEmptyEditSessionAsync(
        string importedBy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_engine.CreateEmpty(_template));
    }

    public Task<ExcelImportResult> ImportEditedRowsAsync(
        Guid editSessionId,
        IReadOnlyList<BudgetEditRowDto> rows,
        string importedBy,
        CancellationToken cancellationToken = default)
        => _engine.ImportEditedRowsAsync(
            editSessionId,
            rows.Select(ToEditableRow).ToList(),
            importedBy,
            _template,
            (stagingRows, token) => _repository.BulkInsertAsync(stagingRows, token),
            cancellationToken);

    private static ExcelImportEditableRow ToEditableRow(BudgetEditRowDto row)
        => new()
        {
            RowNo = row.RowNo,
            Data = row.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
}
