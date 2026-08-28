using WebApp.Models.PurchasePrice;
using WebApp.Services.PurchasePrice;

namespace WebApp.Services.ExcelImport;

// Adapts purchase price edit sessions to the generic Excel Import orchestration flow.
public sealed class PurchasePriceEditSessionAdapter : ExcelImportEditSessionAdapterBase<PurchasePriceEditRowDto>
{
    private readonly IPurchasePriceEditSessionService _editSessionService;
    private readonly IPurchasePriceImportService _importService;

    public PurchasePriceEditSessionAdapter(
        IPurchasePriceEditSessionService editSessionService,
        IPurchasePriceImportService importService)
    {
        _editSessionService = editSessionService;
        _importService = importService;
    }

    public override string ImportType => "purchaseprice";
    public override string EditSessionFileName => "Inköpspriser (redigering)";
    public override int MaxEditableRows => 1000;

    public override async Task<ExcelImportResult> CreateEditSessionFromFileAsync(
        Microsoft.AspNetCore.Http.IFormFile file,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _editSessionService.CreateEditSessionFromFileAsync(file, importedBy, MaxEditableRows, cancellationToken);
        }
        catch (EditableImportRowLimitExceededException)
        {
            ExcelImportFormFileResetter.RewindIfPossible(file);
            return await _importService.ImportAsync(file, importedBy, cancellationToken);
        }
    }

    public override Task<ExcelImportResult> CreateEmptyEditSessionAsync(
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default)
    {
        return _editSessionService.CreateEmptyEditSessionAsync(importedBy, cancellationToken);
    }

    protected override PurchasePriceEditRowDto CreateRow(ExcelImportRowResult row)
    {
        return new PurchasePriceEditRowDto
        {
            RowNo = row.RowNo,
            Data = row.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    protected override Task<ExcelImportResult> ImportRowsCoreAsync(
        Guid editSessionId,
        IReadOnlyList<PurchasePriceEditRowDto> rows,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken)
    {
        return _editSessionService.ImportEditedRowsAsync(editSessionId, rows, importedBy, cancellationToken);
    }
}
