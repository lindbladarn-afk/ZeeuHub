using WebApp.Repositories.PressKogyoPrice;
using WebApp.Services.PressKogyoPrice;

namespace WebApp.Services.ExcelImport;

// Configures the shared supplier-price edit flow for Press Kogyo imports.
public sealed class PressKogyoPriceEditSessionAdapter : SupplierPriceEditSessionAdapterBase
{
    private static readonly SupplierPriceEditSessionDefinition Definition = new()
    {
        ImportType = "presskogyoprice",
        EditSessionFileName = "Prisinläsning Press Kogyo (redigering)",
        MaxEditableRows = 50_000,
        MissingStagingTableMessage = "Press Kogyo-stagingtabellen saknas. Initiera importtabeller innan du importerar.",
        ValidationStoppedBeforeStagingMessage = "Omimporten stoppades innan staging. Inga rader skrevs till stagingtabellen eftersom någon rad fortfarande innehåller fel."
    };

    public PressKogyoPriceEditSessionAdapter(
        IPressKogyoPriceImportService importService,
        IPressKogyoPriceStagingRepository repository,
        IExcelImportContextService contextService,
        IExcelImportResultFactory resultFactory)
        : base(importService, repository, contextService, resultFactory, Definition)
    {
    }
}
