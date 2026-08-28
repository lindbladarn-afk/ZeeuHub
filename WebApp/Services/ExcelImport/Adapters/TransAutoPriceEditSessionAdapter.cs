using WebApp.Repositories.TransAutoPrice;
using WebApp.Services.TransAutoPrice;

namespace WebApp.Services.ExcelImport;

// Configures the shared supplier-price edit flow for Trans Auto imports.
public sealed class TransAutoPriceEditSessionAdapter : SupplierPriceEditSessionAdapterBase
{
    private static readonly SupplierPriceEditSessionDefinition Definition = new()
    {
        ImportType = "transautoprice",
        EditSessionFileName = "Prisinläsning Trans Auto (redigering)",
        MaxEditableRows = 50_000,
        MissingStagingTableMessage = "Trans Auto-stagingtabellen saknas. Initiera importtabeller innan du importerar.",
        ValidationStoppedBeforeStagingMessage = "Omimporten stoppades innan staging. Inga rader skrevs till stagingtabellen eftersom någon rad fortfarande innehåller fel."
    };

    public TransAutoPriceEditSessionAdapter(
        ITransAutoPriceImportService importService,
        ITransAutoPriceStagingRepository repository,
        IExcelImportContextService contextService,
        IExcelImportResultFactory resultFactory)
        : base(importService, repository, contextService, resultFactory, Definition)
    {
    }
}
