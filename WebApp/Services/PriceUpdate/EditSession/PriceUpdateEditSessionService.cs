using System.Text.Encodings.Web;
using System.Text.Json;
using WebApp.Models.PriceUpdate;
using WebApp.Repositories.PriceUpdate;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PriceUpdate;

// Configures editable price-update rows for the shared edit-session engine.
public sealed class PriceUpdateEditSessionService : IPriceUpdateEditSessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IPriceUpdateStagingRepository _repository;
    private readonly IExcelImportEditSessionEngine _engine;
    private readonly ExcelImportEditTemplate<PortalPriceUpdateStagingRow> _template;

    public PriceUpdateEditSessionService(
        IPriceUpdateStagingRepository repository,
        IPriceUpdateStagingRowFactory stagingRowFactory,
        IExcelImportEditSessionEngine engine)
    {
        _repository = repository;
        _engine = engine;
        _template = new ExcelImportEditTemplate<PortalPriceUpdateStagingRow>
        {
            ImportType = "priceupdate",
            WorkbookDefinition = PriceUpdateValidation.WorkbookDefinition,
            Headers = PriceUpdateValidation.ExpectedHeaders,
            ValidateRow = PriceUpdateValidation.ValidateRowData,
            BuildRowSnapshot = PriceUpdateValidation.BuildRowSnapshot,
            HasAnyValue = PriceUpdateValidation.HasAnyValue,
            NormalizeValue = (_, value) => value.Trim(),
            CreateStagingRow = context => stagingRowFactory.Create(new PriceUpdateStagingRowCreateRequest
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
        IReadOnlyList<PriceUpdateEditRowDto> rows,
        string importedBy,
        CancellationToken cancellationToken = default)
        => _engine.ImportEditedRowsAsync(
            editSessionId,
            rows.Select(ToEditableRow).ToList(),
            importedBy,
            _template,
            (stagingRows, token) => _repository.BulkInsertAsync(stagingRows, token),
            cancellationToken);

    private static ExcelImportEditableRow ToEditableRow(PriceUpdateEditRowDto row)
        => new()
        {
            RowNo = row.RowNo,
            Data = row.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
}
