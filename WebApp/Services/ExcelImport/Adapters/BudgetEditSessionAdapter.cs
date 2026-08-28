using WebApp.Models.Budget;
using WebApp.Services.Budget;

namespace WebApp.Services.ExcelImport;

// Adapts budget edit sessions to the generic Excel Import orchestration flow.
public sealed class BudgetEditSessionAdapter : ExcelImportEditSessionAdapterBase<BudgetEditRowDto>
{
    private readonly IBudgetEditSessionService _editSessionService;
    private readonly IBudgetImportService _importService;

    public BudgetEditSessionAdapter(
        IBudgetEditSessionService editSessionService,
        IBudgetImportService importService)
    {
        _editSessionService = editSessionService;
        _importService = importService;
    }

    public override string ImportType => "budget";
    public override string EditSessionFileName => "Budget (redigering)";
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

    protected override BudgetEditRowDto CreateRow(ExcelImportRowResult row)
    {
        return new BudgetEditRowDto
        {
            RowNo = row.RowNo,
            Data = row.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    protected override Task<ExcelImportResult> ImportRowsCoreAsync(
        Guid editSessionId,
        IReadOnlyList<BudgetEditRowDto> rows,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken)
    {
        return _editSessionService.ImportEditedRowsAsync(editSessionId, rows, importedBy, cancellationToken);
    }
}
