using Microsoft.AspNetCore.Http;
using WebApp.Observability;
using WebApp.Repositories.TransAutoPrice;
using WebApp.Services.ExcelImport;
using WebApp.Services.SupplierPrice;

namespace WebApp.Services.TransAutoPrice;

// Configures the shared supplier price import engine for Trans Auto supplier workbooks.
public sealed class TransAutoPriceImportService : ITransAutoPriceImportService
{
    private const string ImportType = "transautoprice";
    private readonly SupplierPriceImportEngine _engine;

    private static readonly SupplierPriceImportDefinition Definition = new()
    {
        ImportType = ImportType,
        ResultHeaders = SupplierPriceImportColumns.ResultHeaders,
        MissingStagingTableMessage = "Trans Auto-stagingtabellen saknas. Initiera importtabeller innan du importerar.",
        ValidationStoppedBeforeStagingMessage = "Importen stoppades innan staging. Inga rader skrevs till stagingtabellen eftersom filen innehåller felrader.",
        UnsupportedWorkbookMessage = "Filen kunde inte läsas. Kontrollera att den är en giltig Excel-fil.",
        UnsupportedImportMessage = "Prisinläsning Trans Auto stödjer just nu .xlsx och .xlsm.",
        EmptyFileMessage = "Filen är tom.",
        NoMatchMessage = "Ingen känd Trans Auto-prislista kunde identifieras. Kontrollera att filen kommer från en stödd leverantör.",
        StagingFailureMessage = "Trans Auto-importen misslyckades när rader skulle skrivas till stagingtabellen. Kontakta support med felkod TRANS_AUTO_PRICE_STAGING_FAILED.",
        GenericFailureMessage = "Trans Auto-importen misslyckades. Kontakta support med felkod TRANS_AUTO_PRICE_IMPORT_FAILED.",
        ImportFailedErrorCode = PortalErrorCodes.TransAutoPriceImportFailed,
        WorkbookReadFailedErrorCode = PortalErrorCodes.TransAutoPriceWorkbookReadFailed,
        StagingFailedErrorCode = PortalErrorCodes.TransAutoPriceStagingFailed,
        Profiles = new[]
        {
            new SupplierPriceProfile(
                Supplier: "Cummins",
                RequiredHeaderSignals: new[] { "partnumber", "listprice", "description" },
                DefaultCurrency: "EUR",
                FirstDataRowOffset: 1,
                SupplierArticleNo: SupplierPriceFieldMapping.Header("partnumber"),
                CustomerArticleNo: SupplierPriceFieldMapping.None(),
                Description: SupplierPriceFieldMapping.Header("description"),
                CurrencyCode: SupplierPriceFieldMapping.None(),
                ListPrice: SupplierPriceFieldMapping.Header("listprice"),
                NetPrice: SupplierPriceFieldMapping.None(),
                DiscountPercent: SupplierPriceFieldMapping.None(),
                Uom: SupplierPriceFieldMapping.None(),
                MinimumOrderQuantity: SupplierPriceFieldMapping.None(),
                PackageQuantity: SupplierPriceFieldMapping.None(),
                WeightKg: SupplierPriceFieldMapping.Header("finalunitweightkg"),
                CountryOfOrigin: SupplierPriceFieldMapping.Header("countryoforigin"),
                TariffCode: SupplierPriceFieldMapping.Header("harmonizedcode"),
                ValidFrom: SupplierPriceFieldMapping.None(),
                ValidTo: SupplierPriceFieldMapping.None(),
                Category1: SupplierPriceFieldMapping.None(),
                Category2: SupplierPriceFieldMapping.None(),
                Category3: SupplierPriceFieldMapping.None(),
                Category4: SupplierPriceFieldMapping.None(),
                Category5: SupplierPriceFieldMapping.None()),
            new SupplierPriceProfile(
                Supplier: "Halyard",
                RequiredHeaderSignals: new[] { "partno", "unitpriceeuro", "description" },
                DefaultCurrency: "EUR",
                FirstDataRowOffset: 2,
                SupplierArticleNo: SupplierPriceFieldMapping.Header("partno"),
                CustomerArticleNo: SupplierPriceFieldMapping.Header("reference"),
                Description: SupplierPriceFieldMapping.Header("description"),
                CurrencyCode: SupplierPriceFieldMapping.None(),
                ListPrice: SupplierPriceFieldMapping.Header("unitpriceeuro"),
                NetPrice: SupplierPriceFieldMapping.None(),
                DiscountPercent: SupplierPriceFieldMapping.None(),
                Uom: SupplierPriceFieldMapping.Header("uom"),
                MinimumOrderQuantity: SupplierPriceFieldMapping.None(),
                PackageQuantity: SupplierPriceFieldMapping.None(),
                WeightKg: SupplierPriceFieldMapping.None(),
                CountryOfOrigin: SupplierPriceFieldMapping.None(),
                TariffCode: SupplierPriceFieldMapping.None(),
                ValidFrom: SupplierPriceFieldMapping.None(),
                ValidTo: SupplierPriceFieldMapping.None(),
                Category1: SupplierPriceFieldMapping.Header("cataloguepageno"),
                Category2: SupplierPriceFieldMapping.None(),
                Category3: SupplierPriceFieldMapping.None(),
                Category4: SupplierPriceFieldMapping.None(),
                Category5: SupplierPriceFieldMapping.None(),
                IgnoreStandaloneLabelRows: true),
            new SupplierPriceProfile(
                Supplier: "HamiltonJet",
                RequiredHeaderSignals: new[] { "itemdescription", "listpricenzd" },
                DefaultCurrency: "NZD",
                FirstDataRowOffset: 1,
                SupplierArticleNo: SupplierPriceFieldMapping.ByColumn(1),
                CustomerArticleNo: SupplierPriceFieldMapping.None(),
                Description: SupplierPriceFieldMapping.Header("itemdescription"),
                CurrencyCode: SupplierPriceFieldMapping.None(),
                ListPrice: SupplierPriceFieldMapping.Header("listpricenzd"),
                NetPrice: SupplierPriceFieldMapping.None(),
                DiscountPercent: SupplierPriceFieldMapping.None(),
                Uom: SupplierPriceFieldMapping.None(),
                MinimumOrderQuantity: SupplierPriceFieldMapping.None(),
                PackageQuantity: SupplierPriceFieldMapping.None(),
                WeightKg: SupplierPriceFieldMapping.None(),
                CountryOfOrigin: SupplierPriceFieldMapping.None(),
                TariffCode: SupplierPriceFieldMapping.None(),
                ValidFrom: SupplierPriceFieldMapping.None(),
                ValidTo: SupplierPriceFieldMapping.None(),
                Category1: SupplierPriceFieldMapping.Header("categorisationlevel1"),
                Category2: SupplierPriceFieldMapping.Header("categorisationlevel2"),
                Category3: SupplierPriceFieldMapping.Header("categorisationlevel3"),
                Category4: SupplierPriceFieldMapping.Header("categorisationlevel4"),
                Category5: SupplierPriceFieldMapping.Header("categorisationlevel5")),
            new SupplierPriceProfile(
                Supplier: "Twin Disc",
                RequiredHeaderSignals: new[] { "twindiscref", "prezzounitario" },
                DefaultCurrency: null,
                FirstDataRowOffset: 1,
                SupplierArticleNo: SupplierPriceFieldMapping.Header("twindiscref"),
                CustomerArticleNo: SupplierPriceFieldMapping.None(),
                Description: SupplierPriceFieldMapping.Header("descrizioneita"),
                CurrencyCode: SupplierPriceFieldMapping.Header("codval"),
                ListPrice: SupplierPriceFieldMapping.Header("prezzounitario"),
                NetPrice: SupplierPriceFieldMapping.Header("netpricetotransauto40"),
                DiscountPercent: SupplierPriceFieldMapping.Constant("40"),
                Uom: SupplierPriceFieldMapping.None(),
                MinimumOrderQuantity: SupplierPriceFieldMapping.None(),
                PackageQuantity: SupplierPriceFieldMapping.None(),
                WeightKg: SupplierPriceFieldMapping.None(),
                CountryOfOrigin: SupplierPriceFieldMapping.None(),
                TariffCode: SupplierPriceFieldMapping.None(),
                ValidFrom: SupplierPriceFieldMapping.None(),
                ValidTo: SupplierPriceFieldMapping.None(),
                Category1: SupplierPriceFieldMapping.None(),
                Category2: SupplierPriceFieldMapping.None(),
                Category3: SupplierPriceFieldMapping.None(),
                Category4: SupplierPriceFieldMapping.None(),
                Category5: SupplierPriceFieldMapping.None()),
            new SupplierPriceProfile(
                Supplier: "OH",
                RequiredHeaderSignals: new[] { "ohpartnumber", "standardprice", "currency" },
                DefaultCurrency: null,
                FirstDataRowOffset: 1,
                SupplierArticleNo: SupplierPriceFieldMapping.Header("ohpartnumber"),
                CustomerArticleNo: SupplierPriceFieldMapping.Header("customerpartnumber"),
                Description: SupplierPriceFieldMapping.Header("ohpartdescription"),
                CurrencyCode: SupplierPriceFieldMapping.Header("currency"),
                ListPrice: SupplierPriceFieldMapping.Header("standardprice"),
                NetPrice: SupplierPriceFieldMapping.Header("specialprice"),
                DiscountPercent: SupplierPriceFieldMapping.None(),
                Uom: SupplierPriceFieldMapping.Header("uom"),
                MinimumOrderQuantity: SupplierPriceFieldMapping.Header("minimumorderquantity"),
                PackageQuantity: SupplierPriceFieldMapping.Header("packagingquantity"),
                WeightKg: SupplierPriceFieldMapping.Header("unitweightgross"),
                CountryOfOrigin: SupplierPriceFieldMapping.Header("countryoforigin"),
                TariffCode: SupplierPriceFieldMapping.None(),
                ValidFrom: SupplierPriceFieldMapping.Header("priceeffectivefromdate"),
                ValidTo: SupplierPriceFieldMapping.Header("priceeffectivetodate"),
                Category1: SupplierPriceFieldMapping.Header("productline"),
                Category2: SupplierPriceFieldMapping.Header("publishedleadtimedays"),
                Category3: SupplierPriceFieldMapping.Header("unitcoredeposit"),
                Category4: SupplierPriceFieldMapping.Header("emergencyprice"),
                Category5: SupplierPriceFieldMapping.Header("partpricewaslastupdated"),
                IgnoreStandaloneLabelRows: true)
        }
    };

    public TransAutoPriceImportService(
        ITransAutoPriceStagingRepository repository,
        IExcelImportRowResultStore rowResultStore,
        IExcelImportContextService importContextService,
        IExcelImportResultFactory resultFactory,
        ILogger<TransAutoPriceImportService> logger)
    {
        _engine = new SupplierPriceImportEngine(repository, rowResultStore, importContextService, resultFactory, logger);
    }

    public Task<ExcelImportResult> ImportAsync(
        IFormFile file,
        string importedBy,
        CancellationToken cancellationToken = default)
        => _engine.ImportAsync(file, importedBy, Definition, cancellationToken);
}
