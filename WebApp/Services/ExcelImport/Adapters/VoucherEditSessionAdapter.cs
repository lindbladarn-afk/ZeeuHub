using WebApp.Models.Voucher;
using WebApp.Services.Vouchers;

namespace WebApp.Services.ExcelImport;

// Adapts voucher edit sessions to the generic Excel Import orchestration flow.
public sealed class VoucherEditSessionAdapter : ExcelImportEditSessionAdapterBase<VoucherEditRowDto>
{
    private readonly IVoucherEditSessionService _editSessionService;
    private readonly IVoucherImportService _importService;
    private readonly IVoucherImportResultFactory _resultFactory;

    public VoucherEditSessionAdapter(
        IVoucherEditSessionService editSessionService,
        IVoucherImportService importService,
        IVoucherImportResultFactory resultFactory)
    {
        _editSessionService = editSessionService;
        _importService = importService;
        _resultFactory = resultFactory;
    }

    public override string ImportType => "voucher";
    public override string EditSessionFileName => "Voucher (redigering)";
    public override int MaxEditableRows => 1000;

    public override async Task<ExcelImportResult> CreateEditSessionFromFileAsync(
        Microsoft.AspNetCore.Http.IFormFile file,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default)
    {
        var postingDate = RequirePostingDate(context);
        try
        {
            return await _editSessionService.CreateEditSessionFromFileAsync(
                file,
                importedBy,
                MaxEditableRows,
                postingDate,
                context.VoucherReversalDate,
                cancellationToken);
        }
        catch (EditableImportRowLimitExceededException)
        {
            ExcelImportFormFileResetter.RewindIfPossible(file);
            var direct = await _importService.ImportAsync(
                file,
                importedBy,
                postingDate,
                context.VoucherReversalDate,
                cancellationToken);
            return _resultFactory.ToExcelImportResult(direct, ImportType);
        }
    }

    public override Task<ExcelImportResult> CreateEmptyEditSessionAsync(
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default)
    {
        return _editSessionService.CreateEmptyEditSessionAsync(
            importedBy,
            context.VoucherPostingDate ?? DateTime.Today,
            context.VoucherReversalDate,
            cancellationToken);
    }

    protected override VoucherEditRowDto CreateRow(ExcelImportRowResult row)
    {
        return new VoucherEditRowDto
        {
            RowNo = row.RowNo,
            Data = row.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    protected override Task<ExcelImportResult> ImportRowsCoreAsync(
        Guid editSessionId,
        IReadOnlyList<VoucherEditRowDto> rows,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken)
    {
        return _editSessionService.ImportEditedRowsAsync(
            editSessionId,
            rows,
            importedBy,
            RequirePostingDate(context),
            context.VoucherReversalDate,
            cancellationToken);
    }

    private static DateTime RequirePostingDate(ExcelImportEditSessionContext context)
    {
        return context.VoucherPostingDate
            ?? throw new InvalidOperationException("Bokföringsdatum saknas eller är ogiltigt.");
    }
}
