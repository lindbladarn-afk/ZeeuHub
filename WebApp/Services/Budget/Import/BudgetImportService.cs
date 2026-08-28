using WebApp.Models.Budget;
using WebApp.Repositories.Budget;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Budget;

// Configures the shared fixed-template engine for budget workbooks.
public sealed class BudgetImportService : IBudgetImportService
{
    private readonly IBudgetStagingRepository _repository;
    private readonly IExcelImportFixedTemplateEngine _engine;
    private readonly ExcelImportFixedTemplate<PortalBudgetStagingRow> _template;

    public BudgetImportService(
        IBudgetStagingRepository repository,
        IBudgetStagingRowFactory stagingRowFactory,
        IExcelImportFixedTemplateEngine engine)
    {
        _repository = repository;
        _engine = engine;
        _template = new ExcelImportFixedTemplate<PortalBudgetStagingRow>
        {
            ImportType = "budget",
            WorkbookDefinition = BudgetValidation.WorkbookDefinition,
            ValidateRow = BudgetValidation.ValidateRowData,
            BuildRowSnapshot = BudgetValidation.BuildRowSnapshot,
            CreateStagingRow = context => stagingRowFactory.Create(new BudgetStagingRowCreateRequest
            {
                ImportBatchId = context.ImportBatchId,
                RowNo = context.RowNo,
                RawJsonData = context.NonEmptyRowData,
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
