using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using WebApp.Models.SupplierPrice;
using WebApp.Services.SupplierPrice;

namespace WebApp.Services.ExcelImport;

// Implements validation and staging for normalized supplier-price edit sessions.
public abstract class SupplierPriceEditSessionAdapterBase
    : ExcelImportEditSessionAdapterBase<SupplierPriceEditRowDto>
{
    private const int MaxValidationErrors = 200;
    private readonly ISupplierPriceImportService _importService;
    private readonly ISupplierPriceStagingRepository _repository;
    private readonly IExcelImportContextService _contextService;
    private readonly IExcelImportResultFactory _resultFactory;
    private readonly SupplierPriceEditSessionDefinition _definition;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string[] DateHeaders = ["ValidFrom", "ValidTo"];

    protected SupplierPriceEditSessionAdapterBase(
        ISupplierPriceImportService importService,
        ISupplierPriceStagingRepository repository,
        IExcelImportContextService contextService,
        IExcelImportResultFactory resultFactory,
        SupplierPriceEditSessionDefinition definition)
    {
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _contextService = contextService ?? throw new ArgumentNullException(nameof(contextService));
        _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public override string ImportType => _definition.ImportType;
    public override string EditSessionFileName => _definition.EditSessionFileName;
    public override int MaxEditableRows => _definition.MaxEditableRows;

    public override Task<ExcelImportResult> CreateEditSessionFromFileAsync(
        IFormFile file,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default)
        => _importService.ImportAsync(file, importedBy, cancellationToken);

    public override async Task<ExcelImportResult> CreateEmptyEditSessionAsync(
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await _repository.TableExistsAsync(cancellationToken))
            return CreateFailureResult();

        var row = new ExcelImportRowResult
        {
            RowNo = 1,
            IsValid = false,
            Data = SupplierPriceImportColumns.ResultHeaders.ToDictionary(
                header => header,
                _ => string.Empty,
                StringComparer.OrdinalIgnoreCase)
        };

        return _resultFactory.Create(new ExcelImportResultCreateRequest
        {
            ImportType = ImportType,
            ImportBatchId = Guid.NewGuid(),
            EditSessionId = Guid.NewGuid(),
            TotalRows = 0,
            ValidRows = 0,
            InvalidRows = 0,
            RowHeaders = SupplierPriceImportColumns.ResultHeaders,
            RowResults = new[] { row }
        });
    }

    protected override SupplierPriceEditRowDto CreateRow(ExcelImportRowResult row)
        => new()
        {
            RowNo = row.RowNo,
            Data = row.Data is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(row.Data, StringComparer.OrdinalIgnoreCase)
        };

    protected override async Task<ExcelImportResult> ImportRowsCoreAsync(
        Guid editSessionId,
        IReadOnlyList<SupplierPriceEditRowDto> rows,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken)
    {
        if (!await _repository.TableExistsAsync(cancellationToken))
            return CreateFailureResult();

        var importBatchId = Guid.NewGuid();
        var importContext = ExcelImportContextGuard.GetRequiredCurrent(_contextService);
        var stagingRows = new List<PortalSupplierPriceStagingRow>(rows.Count);
        var rowResults = new List<ExcelImportRowResult>(rows.Count);
        var errors = new List<string>();
        var typeDefinition = ExcelImportTypeDefinitions.Get(ImportType);

        foreach (var row in rows.OrderBy(row => row.RowNo))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var data = NormalizeData(row.Data);
            var rowErrors = Validate(row.RowNo, data, typeDefinition).ToList();
            rowResults.Add(new ExcelImportRowResult
            {
                RowNo = row.RowNo,
                IsValid = rowErrors.Count == 0,
                ErrorMessage = rowErrors.Count == 0 ? null : string.Join(" ", rowErrors),
                Data = BuildResultData(data)
            });

            if (rowErrors.Count > 0)
            {
                AddValidationErrors(errors, rowErrors);
                continue;
            }

            stagingRows.Add(MapStagingRow(importBatchId, row.RowNo, data, importedBy, importContext));
        }

        if (errors.Count == 0 && stagingRows.Count > 0)
            await _repository.BulkInsertAsync(stagingRows, cancellationToken);

        var resultErrors = errors.Count > 0
            ? errors.Prepend(_definition.ValidationStoppedBeforeStagingMessage).ToList()
            : errors;

        return _resultFactory.Create(new ExcelImportResultCreateRequest
        {
            ImportType = ImportType,
            ImportBatchId = importBatchId,
            EditSessionId = errors.Count > 0 ? editSessionId : null,
            TotalRows = rowResults.Count,
            ValidRows = rowResults.Count(result => result.IsValid),
            InvalidRows = rowResults.Count(result => !result.IsValid),
            StagedRows = errors.Count == 0 ? stagingRows.Count : 0,
            Errors = resultErrors,
            RowHeaders = SupplierPriceImportColumns.ResultHeaders,
            RowResults = rowResults
        });
    }

    private PortalSupplierPriceStagingRow MapStagingRow(
        Guid importBatchId,
        int rowNo,
        Dictionary<string, string> data,
        string importedBy,
        ExcelImportUserContext? importContext)
        => new()
        {
            ImportBatchId = importBatchId,
            RowNo = rowNo,
            Supplier = data.GetValueOrDefault("Supplier"),
            SupplierArticleNo = data.GetValueOrDefault("SupplierArticleNo"),
            CustomerArticleNo = data.GetValueOrDefault("CustomerArticleNo"),
            Description = data.GetValueOrDefault("Description"),
            CurrencyCode = data.GetValueOrDefault("CurrencyCode"),
            ListPrice = ParseDecimal(data.GetValueOrDefault("ListPrice")),
            NetPrice = ParseDecimal(data.GetValueOrDefault("NetPrice")),
            DiscountPercent = ParseDecimal(data.GetValueOrDefault("DiscountPercent")),
            Uom = data.GetValueOrDefault("Uom"),
            MinimumOrderQuantity = ParseDecimal(data.GetValueOrDefault("MinimumOrderQuantity")),
            PackageQuantity = ParseDecimal(data.GetValueOrDefault("PackageQuantity")),
            WeightKg = ParseDecimal(data.GetValueOrDefault("WeightKg")),
            CountryOfOrigin = data.GetValueOrDefault("CountryOfOrigin"),
            TariffCode = data.GetValueOrDefault("TariffCode"),
            ValidFrom = ParseDate(data.GetValueOrDefault("ValidFrom")),
            ValidTo = ParseDate(data.GetValueOrDefault("ValidTo")),
            Category1 = data.GetValueOrDefault("Category1"),
            Category2 = data.GetValueOrDefault("Category2"),
            Category3 = data.GetValueOrDefault("Category3"),
            Category4 = data.GetValueOrDefault("Category4"),
            Category5 = data.GetValueOrDefault("Category5"),
            SourceSheetName = data.GetValueOrDefault("SourceSheetName"),
            SourceRowNo = ParseInt(data.GetValueOrDefault("SourceRowNo")),
            RawJson = JsonSerializer.Serialize(data, JsonOptions),
            ImportedAt = DateTime.UtcNow,
            ImportedBy = importedBy,
            CompanyId = importContext?.CompanyId,
            ForetagKod = importContext?.ForetagKod,
            UserId = importContext?.UserId
        };

    private ExcelImportResult CreateFailureResult()
        => _resultFactory.Create(new ExcelImportResultCreateRequest
        {
            ImportType = ImportType,
            ImportBatchId = Guid.NewGuid(),
            TotalRows = 0,
            ValidRows = 0,
            InvalidRows = 0,
            Errors = new[] { _definition.MissingStagingTableMessage },
            RowHeaders = SupplierPriceImportColumns.ResultHeaders,
            RowResults = Array.Empty<ExcelImportRowResult>()
        });

    private static Dictionary<string, string> NormalizeData(Dictionary<string, string>? data)
        => SupplierPriceImportColumns.ResultHeaders.ToDictionary(
            header => header,
            header => data is not null && data.TryGetValue(header, out var value)
                ? value?.Trim() ?? string.Empty
                : string.Empty,
            StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> BuildResultData(Dictionary<string, string> data)
        => SupplierPriceImportColumns.ResultHeaders.ToDictionary(
            header => header,
            header => data.GetValueOrDefault(header) ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Validate(
        int rowNo,
        Dictionary<string, string> data,
        ExcelImportTypeDefinition typeDefinition)
    {
        if (data.Values.Any(value => value.Length > ExcelImportResourceLimits.MaxCellLength))
            yield return ExcelImportResourceLimits.CellTooLongMessage(rowNo, ExcelImportResourceLimits.MaxCellLength);

        foreach (var header in typeDefinition.RequiredHeaders)
        {
            if (string.IsNullOrWhiteSpace(data.GetValueOrDefault(header)))
                yield return $"Rad {rowNo}: {header} saknas.";
        }

        foreach (var header in typeDefinition.ThreeLetterCodeHeaders)
        {
            var value = data.GetValueOrDefault(header);
            if (!string.IsNullOrWhiteSpace(value) && value.Length != 3)
                yield return $"Rad {rowNo}: {header} måste vara tre tecken.";
        }

        foreach (var header in typeDefinition.NumericHeaders)
        {
            var value = data.GetValueOrDefault(header);
            if (!string.IsNullOrWhiteSpace(value) && ParseDecimal(value) is null)
                yield return $"Rad {rowNo}: {header} kunde inte tolkas som tal.";
        }

        var listPrice = ParseDecimal(data.GetValueOrDefault("ListPrice"));
        if (listPrice < 0)
            yield return $"Rad {rowNo}: ListPrice får inte vara negativt.";

        var netPrice = ParseDecimal(data.GetValueOrDefault("NetPrice"));
        if (netPrice < 0)
            yield return $"Rad {rowNo}: NetPrice får inte vara negativt.";

        var sourceRowNo = data.GetValueOrDefault("SourceRowNo");
        if (!string.IsNullOrWhiteSpace(sourceRowNo) && ParseInt(sourceRowNo) is null)
            yield return $"Rad {rowNo}: SourceRowNo kunde inte tolkas som heltal.";

        foreach (var header in DateHeaders)
        {
            var value = data.GetValueOrDefault(header);
            if (!string.IsNullOrWhiteSpace(value) && ParseDate(value) is null)
                yield return $"Rad {rowNo}: {header} kunde inte tolkas som datum.";
        }
    }

    private static void AddValidationErrors(List<string> target, IEnumerable<string> rowErrors)
    {
        foreach (var error in rowErrors)
        {
            if (target.Count >= MaxValidationErrors)
                return;

            target.Add(error);
        }
    }

    private static decimal? ParseDecimal(string? value)
    {
        var normalized = (value ?? string.Empty)
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty);
        if (normalized.Length == 0)
            return null;

        return SupplierPriceImportText.TryParseDecimal(normalized, out var parsed)
            ? parsed
            : null;
    }

    private static int? ParseInt(string? value)
        => int.TryParse(
            (value ?? string.Empty).Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static DateTime? ParseDate(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return null;

        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            || DateTime.TryParse(trimmed, CultureInfo.GetCultureInfo("sv-SE"), DateTimeStyles.AssumeLocal, out parsed)
            ? parsed.Date
            : null;
    }
}
