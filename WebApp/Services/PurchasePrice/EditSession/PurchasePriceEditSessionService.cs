using System.Text.Encodings.Web;
using System.Text.Json;
using WebApp.Models.PurchasePrice;
using WebApp.Repositories.PurchasePrice;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PurchasePrice;

// Configures editable purchase-price rows for the shared edit-session engine.
public sealed class PurchasePriceEditSessionService : IPurchasePriceEditSessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IPurchasePriceStagingRepository _repository;
    private readonly IExcelImportEditSessionEngine _engine;
    private readonly ExcelImportEditTemplate<PortalPurchasePriceStagingRow> _template;

    public PurchasePriceEditSessionService(
        IPurchasePriceStagingRepository repository,
        IPurchasePriceStagingRowFactory stagingRowFactory,
        IExcelImportEditSessionEngine engine)
    {
        _repository = repository;
        _engine = engine;
        _template = new ExcelImportEditTemplate<PortalPurchasePriceStagingRow>
        {
            ImportType = "purchaseprice",
            WorkbookDefinition = PurchasePriceValidation.WorkbookDefinition,
            Headers = PurchasePriceValidation.ExpectedHeaders,
            ValidateRow = PurchasePriceValidation.ValidateRowData,
            BuildRowSnapshot = PurchasePriceValidation.BuildRowSnapshot,
            HasAnyValue = PurchasePriceValidation.HasAnyValue,
            NormalizeValue = (_, value) => value.Trim(),
            CreateStagingRow = context => stagingRowFactory.Create(new PurchasePriceStagingRowCreateRequest
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
        IReadOnlyList<PurchasePriceEditRowDto> rows,
        string importedBy,
        CancellationToken cancellationToken = default)
        => _engine.ImportEditedRowsAsync(
            editSessionId,
            rows.Select(ToEditableRow).ToList(),
            importedBy,
            _template,
            (stagingRows, token) => _repository.BulkInsertAsync(stagingRows, token),
            cancellationToken);

    private static ExcelImportEditableRow ToEditableRow(PurchasePriceEditRowDto row)
        => new()
        {
            RowNo = row.RowNo,
            Data = row.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
}
