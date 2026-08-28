using System.Text.Encodings.Web;
using System.Text.Json;
using WebApp.Models.Voucher;
using WebApp.Repositories.Vouchers;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Vouchers;

// Configures editable voucher rows and dates for the shared edit-session engine.
public sealed class VoucherEditSessionService : IVoucherEditSessionService
{
    private const string DateFormat = "yyyy-MM-dd";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IVoucherStagingRepository _repository;
    private readonly IVoucherStagingRowFactory _stagingRowFactory;
    private readonly IExcelImportEditSessionEngine _engine;

    public VoucherEditSessionService(
        IVoucherStagingRepository repository,
        IVoucherStagingRowFactory stagingRowFactory,
        IExcelImportEditSessionEngine engine)
    {
        _repository = repository;
        _stagingRowFactory = stagingRowFactory;
        _engine = engine;
    }

    public async Task<ExcelImportResult> CreateEditSessionFromFileAsync(
        IFormFile file,
        string importedBy,
        int maxRows,
        DateTime postingDate,
        DateTime? reversalDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _engine.CreateFromFileAsync(
            file,
            CreateTemplate(postingDate, reversalDate),
            maxRows,
            cancellationToken);
        return AddDates(result, postingDate, reversalDate);
    }

    public Task<ExcelImportResult> CreateEmptyEditSessionAsync(
        string importedBy,
        DateTime postingDate,
        DateTime? reversalDate = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = _engine.CreateEmpty(CreateTemplate(postingDate, reversalDate));
        return Task.FromResult(AddDates(result, postingDate, reversalDate));
    }

    public async Task<ExcelImportResult> ImportEditedRowsAsync(
        Guid editSessionId,
        IReadOnlyList<VoucherEditRowDto> rows,
        string importedBy,
        DateTime postingDate,
        DateTime? reversalDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _engine.ImportEditedRowsAsync(
            editSessionId,
            rows.Select(ToEditableRow).ToList(),
            importedBy,
            CreateTemplate(postingDate, reversalDate),
            (stagingRows, token) => _repository.BulkInsertAsync(stagingRows, token),
            cancellationToken);
        return AddDates(result, postingDate, reversalDate);
    }

    private ExcelImportEditTemplate<PortalVoucherStagingRow> CreateTemplate(
        DateTime postingDate,
        DateTime? reversalDate)
        => new()
        {
            ImportType = "voucher",
            WorkbookDefinition = VoucherValidation.WorkbookDefinition,
            Headers = VoucherValidation.ExpectedHeaders,
            ValidateRow = VoucherValidation.ValidateRowData,
            BuildRowSnapshot = VoucherValidation.BuildRowSnapshot,
            HasAnyValue = VoucherValidation.HasAnyValue,
            NormalizeValue = VoucherValidation.Normalize,
            CreateStagingRow = context => _stagingRowFactory.Create(new VoucherStagingRowCreateRequest
            {
                ImportBatchId = context.ImportBatchId,
                RowNo = context.RowNo,
                RowData = context.RowData,
                RawJsonData = context.NonEmptyRowData,
                JsonOptions = JsonOptions,
                ImportedBy = context.ImportedBy,
                UserContext = context.UserContext,
                PostingDate = postingDate.Date,
                ReversalDate = reversalDate?.Date
            })
        };

    private static ExcelImportEditableRow ToEditableRow(VoucherEditRowDto row)
        => new()
        {
            RowNo = row.RowNo,
            Data = row.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

    private static ExcelImportResult AddDates(
        ExcelImportResult result,
        DateTime postingDate,
        DateTime? reversalDate)
    {
        result.VoucherPostingDate = postingDate.ToString(DateFormat);
        result.VoucherReversalDate = reversalDate?.ToString(DateFormat);
        return result;
    }
}
