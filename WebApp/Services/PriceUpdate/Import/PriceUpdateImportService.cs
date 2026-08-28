using System.Text.Encodings.Web;
using System.Text.Json;
using WebApp.Models.PriceUpdate;
using WebApp.Repositories.PriceUpdate;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PriceUpdate;

// Configures the shared fixed-template engine for price-update workbooks.
public sealed class PriceUpdateImportService : IPriceUpdateImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IPriceUpdateStagingRepository _repository;
    private readonly IExcelImportFixedTemplateEngine _engine;
    private readonly ExcelImportFixedTemplate<PortalPriceUpdateStagingRow> _template;

    public PriceUpdateImportService(
        IPriceUpdateStagingRepository repository,
        IPriceUpdateStagingRowFactory stagingRowFactory,
        IExcelImportFixedTemplateEngine engine)
    {
        _repository = repository;
        _engine = engine;
        _template = new ExcelImportFixedTemplate<PortalPriceUpdateStagingRow>
        {
            ImportType = "priceupdate",
            WorkbookDefinition = PriceUpdateValidation.WorkbookDefinition,
            ValidateRow = PriceUpdateValidation.ValidateRowData,
            BuildRowSnapshot = PriceUpdateValidation.BuildRowSnapshot,
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

    public Task<ExcelImportResult> ImportAsync(
        IFormFile file,
        string importedBy,
        CancellationToken cancellationToken = default)
        => _engine.ImportAsync(
            file,
            importedBy,
            _template,
            (rows, token) => _repository.BulkInsertAsync(rows, token),
            cancellationToken);
}
