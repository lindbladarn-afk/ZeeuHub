using WebApp.Models.Voucher;
using WebApp.Repositories.Vouchers;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Vouchers;

// Configures the shared fixed-template engine for voucher workbooks and voucher dates.
public sealed class VoucherImportService : IVoucherImportService
{
    private readonly IVoucherStagingRepository _repository;
    private readonly IVoucherStagingRowFactory _stagingRowFactory;
    private readonly IVoucherImportResultFactory _resultFactory;
    private readonly IExcelImportFixedTemplateEngine _engine;

    public VoucherImportService(
        IVoucherStagingRepository repository,
        IVoucherStagingRowFactory stagingRowFactory,
        IVoucherImportResultFactory resultFactory,
        IExcelImportFixedTemplateEngine engine)
    {
        _repository = repository;
        _stagingRowFactory = stagingRowFactory;
        _resultFactory = resultFactory;
        _engine = engine;
    }

    public async Task<VoucherImportResult> ImportAsync(
        IFormFile file,
        string importedBy,
        DateTime? postingDate = null,
        DateTime? reversalDate = null,
        CancellationToken cancellationToken = default)
    {
        var template = new ExcelImportFixedTemplate<PortalVoucherStagingRow>
        {
            ImportType = "voucher",
            WorkbookDefinition = VoucherValidation.WorkbookDefinition,
            ValidateRow = VoucherValidation.ValidateRowData,
            BuildRowSnapshot = VoucherValidation.BuildRowSnapshot,
            CreateStagingRow = context => _stagingRowFactory.Create(new VoucherStagingRowCreateRequest
            {
                ImportBatchId = context.ImportBatchId,
                RowNo = context.RowNo,
                RowData = context.RowData,
                RawJsonData = context.RowData,
                ImportedBy = context.ImportedBy,
                UserContext = context.UserContext,
                PostingDate = postingDate?.Date,
                ReversalDate = reversalDate?.Date
            })
        };

        var result = await _engine.ImportAsync(
            file,
            importedBy,
            template,
            (rows, token) => _repository.BulkInsertAsync(rows, token),
            cancellationToken);

        return _resultFactory.CreateImportResult(new VoucherImportResultCreateRequest
        {
            ImportBatchId = result.ImportBatchId,
            TotalRows = result.TotalRows,
            ValidRows = result.ValidRows,
            StagedRows = result.StagedRows,
            PostingDate = postingDate,
            ReversalDate = reversalDate,
            RowHeaders = result.RowHeaders,
            RowResults = result.RowResults,
            Errors = result.Errors
        });
    }
}
