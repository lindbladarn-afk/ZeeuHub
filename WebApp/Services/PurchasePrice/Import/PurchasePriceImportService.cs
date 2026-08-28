using System.Text.Encodings.Web;
using System.Text.Json;
using WebApp.Models.PurchasePrice;
using WebApp.Repositories.PurchasePrice;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PurchasePrice;

// Configures the shared fixed-template engine for purchase-price workbooks.
public sealed class PurchasePriceImportService : IPurchasePriceImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IPurchasePriceStagingRepository _repository;
    private readonly IExcelImportFixedTemplateEngine _engine;
    private readonly ExcelImportFixedTemplate<PortalPurchasePriceStagingRow> _template;

    public PurchasePriceImportService(
        IPurchasePriceStagingRepository repository,
        IPurchasePriceStagingRowFactory stagingRowFactory,
        IExcelImportFixedTemplateEngine engine)
    {
        _repository = repository;
        _engine = engine;
        _template = new ExcelImportFixedTemplate<PortalPurchasePriceStagingRow>
        {
            ImportType = "purchaseprice",
            WorkbookDefinition = PurchasePriceValidation.WorkbookDefinition,
            ValidateRow = PurchasePriceValidation.ValidateRowData,
            BuildRowSnapshot = PurchasePriceValidation.BuildRowSnapshot,
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
