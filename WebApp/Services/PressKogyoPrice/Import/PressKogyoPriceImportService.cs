using Microsoft.AspNetCore.Http;
using WebApp.Observability;
using WebApp.Repositories.PressKogyoPrice;
using WebApp.Services.ExcelImport;
using WebApp.Services.SupplierPrice;

namespace WebApp.Services.PressKogyoPrice;

// Configures the shared supplier price import engine for Press Kogyo price-list workbooks.
public sealed class PressKogyoPriceImportService : IPressKogyoPriceImportService
{
    private const string ImportType = "presskogyoprice";
    private readonly SupplierPriceImportEngine _engine;

    private static readonly SupplierPriceImportDefinition Definition = new()
    {
        ImportType = ImportType,
        ResultHeaders = SupplierPriceImportColumns.ResultHeaders,
        MissingStagingTableMessage = "Press Kogyo-stagingtabellen saknas. Initiera importtabeller innan du importerar.",
        ValidationStoppedBeforeStagingMessage = "Importen stoppades innan staging. Inga rader skrevs till stagingtabellen eftersom filen innehåller felrader.",
        UnsupportedWorkbookMessage = "Filen kunde inte läsas. Kontrollera att den är en giltig Excel-fil.",
        UnsupportedImportMessage = "Prisinläsning Press Kogyo stödjer just nu .xlsx och .xlsm.",
        EmptyFileMessage = "Filen är tom.",
        NoMatchMessage = "Ingen känd Press Kogyo-prislista kunde identifieras. Kontrollera att filen kommer från rätt mall.",
        StagingFailureMessage = "Press Kogyo-importen misslyckades när rader skulle skrivas till stagingtabellen. Kontakta support med felkod PRESS_KOGYO_PRICE_STAGING_FAILED.",
        GenericFailureMessage = "Press Kogyo-importen misslyckades. Kontakta support med felkod PRESS_KOGYO_PRICE_IMPORT_FAILED.",
        ImportFailedErrorCode = PortalErrorCodes.PressKogyoPriceImportFailed,
        WorkbookReadFailedErrorCode = PortalErrorCodes.PressKogyoPriceWorkbookReadFailed,
        StagingFailedErrorCode = PortalErrorCodes.PressKogyoPriceStagingFailed,
        Profiles = new[]
        {
            new SupplierPriceProfile(
                Supplier: "Press Kogyo",
                RequiredHeaderSignals: new[]
                {
                    "artnrpks",
                    "artnrålö",
                    "antalstaffladprislista",
                    "nyttprisjanuarijuni2026"
                },
                DefaultCurrency: "SEK",
                FirstDataRowOffset: 1,
                SupplierArticleNo: SupplierPriceFieldMapping.Header("artnrpks"),
                CustomerArticleNo: SupplierPriceFieldMapping.Header("artnrålö"),
                Description: SupplierPriceFieldMapping.Header("artikelbeskrivning"),
                CurrencyCode: SupplierPriceFieldMapping.None(),
                ListPrice: SupplierPriceFieldMapping.Header("nyttprisjanuarijuni2026"),
                NetPrice: SupplierPriceFieldMapping.None(),
                DiscountPercent: SupplierPriceFieldMapping.Header("prisjusteringför20012508"),
                Uom: SupplierPriceFieldMapping.None(),
                MinimumOrderQuantity: SupplierPriceFieldMapping.Header("antalstaffladprislista"),
                PackageQuantity: SupplierPriceFieldMapping.None(),
                WeightKg: SupplierPriceFieldMapping.Header("nettovikt"),
                CountryOfOrigin: SupplierPriceFieldMapping.None(),
                TariffCode: SupplierPriceFieldMapping.None(),
                ValidFrom: SupplierPriceFieldMapping.ByCell(3, 3),
                ValidTo: SupplierPriceFieldMapping.None(),
                Category1: SupplierPriceFieldMapping.Header("bruttovikt"),
                Category2: SupplierPriceFieldMapping.Header("skrot"),
                Category3: SupplierPriceFieldMapping.Header("prisjusteringför20012508sek"),
                Category4: SupplierPriceFieldMapping.Header("återståendeprishöjning"),
                Category5: SupplierPriceFieldMapping.None())
        }
    };

    public PressKogyoPriceImportService(
        IPressKogyoPriceStagingRepository repository,
        IExcelImportRowResultStore rowResultStore,
        IExcelImportContextService importContextService,
        IExcelImportResultFactory resultFactory,
        ILogger<PressKogyoPriceImportService> logger)
    {
        _engine = new SupplierPriceImportEngine(repository, rowResultStore, importContextService, resultFactory, logger);
    }

    public Task<ExcelImportResult> ImportAsync(
        IFormFile file,
        string importedBy,
        CancellationToken cancellationToken = default)
        => _engine.ImportAsync(file, importedBy, Definition, cancellationToken);
}
