using WebApp.Models.PriceUpdate;
using WebApp.Services.PriceUpdate;

namespace WebApp.Services.ExcelImport;

// Adapts price update edit sessions to the generic Excel Import orchestration flow.
public sealed class PriceUpdateEditSessionAdapter : ExcelImportEditSessionAdapterBase<PriceUpdateEditRowDto>
{
    private readonly IPriceUpdateEditSessionService _editSessionService;
    private readonly IPriceUpdateImportService _importService;

    public PriceUpdateEditSessionAdapter(
        IPriceUpdateEditSessionService editSessionService,
        IPriceUpdateImportService importService)
    {
        _editSessionService = editSessionService;
        _importService = importService;
    }

    public override string ImportType => "priceupdate";
    public override string EditSessionFileName => "PriceUpdate (redigering)";
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

    protected override PriceUpdateEditRowDto CreateRow(ExcelImportRowResult row)
    {
        return new PriceUpdateEditRowDto
        {
            RowNo = row.RowNo,
            Data = row.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    protected override Task<ExcelImportResult> ImportRowsCoreAsync(
        Guid editSessionId,
        IReadOnlyList<PriceUpdateEditRowDto> rows,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken)
    {
        return _editSessionService.ImportEditedRowsAsync(editSessionId, rows, importedBy, cancellationToken);
    }
}
