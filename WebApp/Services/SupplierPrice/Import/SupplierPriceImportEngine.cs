using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ExcelDataReader;
using WebApp.Models.SupplierPrice;
using WebApp.Observability;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.SupplierPrice;

// Reads supplier-specific price-list workbooks through profiles and stages normalized price rows.
public sealed class SupplierPriceImportEngine
{
    private const int HeaderScanRows = 40;
    private const int MaxRows = ExcelImportResourceLimits.MaxSupplierPriceRows;
    private const int MaxValidationErrors = 200;
    private const int RowResultBatchSize = 1000;
    private const int ResultPreviewRows = 50;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ISupplierPriceStagingRepository _repository;
    private readonly IExcelImportRowResultStore _rowResultStore;
    private readonly IExcelImportContextService _importContextService;
    private readonly IExcelImportResultFactory _resultFactory;
    private readonly ILogger _logger;

    static SupplierPriceImportEngine()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public SupplierPriceImportEngine(
        ISupplierPriceStagingRepository repository,
        IExcelImportRowResultStore rowResultStore,
        IExcelImportContextService importContextService,
        IExcelImportResultFactory resultFactory,
        ILogger logger)
    {
        _repository = repository;
        _rowResultStore = rowResultStore;
        _importContextService = importContextService;
        _resultFactory = resultFactory;
        _logger = logger;
    }

    public async Task<ExcelImportResult> ImportAsync(
        IFormFile file,
        string importedBy,
        SupplierPriceImportDefinition definition,
        CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid();
        var totalWatch = Stopwatch.StartNew();
        var extension = Path.GetExtension(file.FileName);

        try
        {
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Supplier price import rejected unsupported file extension. {ErrorCode} ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                    PortalErrorCodes.ExcelImportValidationFailed,
                    definition.ImportType,
                    extension,
                    file.Length);
                return CreateFailure(batchId, definition.UnsupportedImportMessage, definition);
            }

            if (file.Length == 0)
            {
                _logger.LogWarning(
                    "Supplier price import rejected empty file. {ErrorCode} ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                    PortalErrorCodes.ExcelImportValidationFailed,
                    definition.ImportType,
                    extension,
                    file.Length);
                return CreateFailure(batchId, definition.EmptyFileMessage, definition);
            }

            if (file.Length > ExcelImportResourceLimits.MaxUploadBytes)
            {
                _logger.LogWarning(
                    "Supplier price import rejected oversized file. {ErrorCode} ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                    PortalErrorCodes.ExcelImportValidationFailed,
                    definition.ImportType,
                    extension,
                    file.Length);
                return CreateFailure(
                    batchId,
                    ExcelImportResourceLimits.FileTooLargeMessage(ExcelImportResourceLimits.MaxUploadBytes),
                    definition);
            }

            var preflightWatch = Stopwatch.StartNew();
            if (!await _repository.TableExistsAsync(cancellationToken))
            {
                _logger.LogWarning(
                    "Supplier price import aborted because staging table is missing. {ErrorCode} ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                    definition.StagingFailedErrorCode,
                    definition.ImportType,
                    extension,
                    file.Length);
                return CreateFailure(batchId, definition.MissingStagingTableMessage, definition);
            }

            if (!await _rowResultStore.TableExistsAsync(cancellationToken))
            {
                _logger.LogWarning(
                    "Supplier price import aborted because row result table is missing. {ErrorCode} ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                    definition.StagingFailedErrorCode,
                    definition.ImportType,
                    extension,
                    file.Length);
                return CreateFailure(batchId, "Excelimportens radresultattabell saknas. Initiera importtabeller innan du importerar.", definition);
            }

            _logger.LogInformation(
                "Supplier price preflight completed in {ElapsedMs} ms. ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                preflightWatch.ElapsedMilliseconds,
                definition.ImportType,
                extension,
                file.Length);

            await using var stream = file.OpenReadStream();
            if (!stream.CanSeek)
            {
                _logger.LogWarning(
                    "Supplier price import rejected a non-seekable workbook stream. {ErrorCode} ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                    definition.WorkbookReadFailedErrorCode,
                    definition.ImportType,
                    extension,
                    file.Length);
                return CreateFailure(batchId, definition.UnsupportedWorkbookMessage, definition);
            }

            if (!ExcelImportOpenXmlPackageValidator.TryValidate(stream, out var packageError))
            {
                _logger.LogWarning(
                    "Supplier price import rejected unsafe workbook package. {ErrorCode} ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                    definition.WorkbookReadFailedErrorCode,
                    definition.ImportType,
                    extension,
                    file.Length);
                return CreateFailure(batchId, packageError, definition);
            }

            IExcelDataReader reader;
            try
            {
                reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration
                {
                    LeaveOpen = false
                });
            }
            catch (Exception ex) when (IsTransientProcessingFailure(ex))
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Supplier price workbook reader could not be created. {ErrorCode} ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                    definition.WorkbookReadFailedErrorCode,
                    definition.ImportType,
                    extension,
                    file.Length);
                return CreateFailure(batchId, definition.UnsupportedWorkbookMessage, definition);
            }

            using (reader)
            {
                return await ReadAndStageAsync(reader, file, importedBy, batchId, totalWatch, definition, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsTransientProcessingFailure(ex))
        {
            _logger.LogWarning(
                ex,
                "Supplier price import encountered a transient infrastructure failure. {ErrorCode} ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                definition.ImportFailedErrorCode,
                definition.ImportType,
                extension,
                file.Length);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Supplier price import failed. {ErrorCode} ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                definition.ImportFailedErrorCode,
                definition.ImportType,
                extension,
                file.Length);
            return CreateFailure(batchId, definition.GenericFailureMessage, definition);
        }
    }

    private async Task<ExcelImportResult> ReadAndStageAsync(
        IExcelDataReader reader,
        IFormFile file,
        string importedBy,
        Guid batchId,
        Stopwatch totalWatch,
        SupplierPriceImportDefinition definition,
        CancellationToken cancellationToken)
    {
        var importContext = ExcelImportContextGuard.GetRequiredCurrent(_importContextService);
        using var contextScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["ImportType"] = definition.ImportType,
            ["ImportBatchId"] = batchId,
            ["FileExtension"] = Path.GetExtension(file.FileName),
            ["FileSizeBytes"] = file.Length,
            ["CompanyId"] = importContext?.CompanyId,
            ["ForetagKod"] = importContext?.ForetagKod,
            ["UserId"] = importContext?.UserId
        });

        var rowResultBatch = new List<ExcelImportStoredRowResult>(RowResultBatchSize);
        var rowResultPreview = new List<ExcelImportRowResult>(ResultPreviewRows);
        var validationErrors = new List<string>();
        var rowNo = 0;
        var validRows = 0;
        var invalidRows = 0;
        WorksheetProfileMatch? worksheetMatch = null;
        var sheetCells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var profileWatch = Stopwatch.StartNew();
        var rowScanWatch = Stopwatch.StartNew();
        var matched = false;

        do
        {
            var sheetName = reader.Name ?? string.Empty;
            sheetCells.Clear();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceRowNo = reader.Depth + 1;

                if (reader.FieldCount > ExcelImportResourceLimits.MaxColumns)
                {
                    return CreateFailure(
                        batchId,
                        ExcelImportResourceLimits.TooManyColumnsMessage(ExcelImportResourceLimits.MaxColumns),
                        definition,
                        rowResultPreview);
                }

                if (!matched)
                {
                    if (sourceRowNo > HeaderScanRows)
                        break;

                    var headerCells = ReadCurrentRow(reader, reader.FieldCount);
                    if (headerCells.Any(cell => cell.Length > ExcelImportResourceLimits.MaxCellLength))
                    {
                        return CreateFailure(
                            batchId,
                            ExcelImportResourceLimits.CellTooLongMessage(sourceRowNo, ExcelImportResourceLimits.MaxCellLength),
                            definition,
                            rowResultPreview);
                    }
                    StoreScannedCells(sheetCells, sourceRowNo, headerCells);
                    var lastColumn = LastNonEmptyColumn(headerCells);
                    if (lastColumn == 0)
                        continue;

                    var headerMap = BuildHeaderMap(headerCells, lastColumn);
                    var profile = FindWorksheetProfile(definition.Profiles, headerMap);
                    if (profile is null)
                        continue;

                    worksheetMatch = new WorksheetProfileMatch(
                        sheetName,
                        profile,
                        sourceRowNo,
                        lastColumn,
                        headerMap,
                        new Dictionary<string, string>(sheetCells, StringComparer.OrdinalIgnoreCase));
                    matched = true;
                    _logger.LogInformation(
                        "Supplier price import matched profile {Supplier}. ImportType={ImportType} SheetName={SheetName} HeaderRow={HeaderRow} ElapsedMs={ElapsedMs}.",
                        worksheetMatch.Profile.Supplier,
                        definition.ImportType,
                        worksheetMatch.SheetName,
                        worksheetMatch.HeaderRowNo,
                        profileWatch.ElapsedMilliseconds);
                    continue;
                }

                var currentMatch = worksheetMatch ?? throw new InvalidOperationException("Importprofil saknas trots att importen redan har identifierat en mall.");
                if (!string.Equals(sheetName, currentMatch.SheetName, StringComparison.OrdinalIgnoreCase))
                    break;

                if (sourceRowNo <= currentMatch.HeaderRowNo + currentMatch.Profile.FirstDataRowOffset - 1)
                    continue;

                if (sourceRowNo - currentMatch.HeaderRowNo > MaxRows)
                {
                    validationErrors.Add($"Filen innehåller fler än {MaxRows} datarader. Dela upp importen i mindre filer.");
                    break;
                }

                var raw = ReadRawRow(reader, currentMatch.LastColumn);
                if (raw.Values.Any(value => value.Length > ExcelImportResourceLimits.MaxCellLength))
                {
                    return CreateFailure(
                        batchId,
                        ExcelImportResourceLimits.CellTooLongMessage(sourceRowNo, ExcelImportResourceLimits.MaxCellLength),
                        definition,
                        rowResultPreview);
                }
                if (!raw.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
                    continue;
                if (IsStandaloneLabelRow(currentMatch, raw))
                    continue;

                rowNo++;
                var normalized = BuildNormalizedRow(currentMatch, raw, file.FileName, sourceRowNo);
                var rowValidationErrors = Validate(normalized, sourceRowNo).ToList();
                var rowErrorMessage = rowValidationErrors.Count == 0 ? null : string.Join(" ", rowValidationErrors);
                var resultData = BuildResultData(normalized);
                var rowResult = new ExcelImportRowResult
                {
                    RowNo = sourceRowNo,
                    IsValid = rowValidationErrors.Count == 0,
                    ErrorMessage = rowErrorMessage,
                    Data = resultData
                };

                if (rowResultPreview.Count < ResultPreviewRows)
                    rowResultPreview.Add(rowResult);

                rowResultBatch.Add(new ExcelImportStoredRowResult
                {
                    ImportType = definition.ImportType,
                    ImportBatchId = batchId,
                    RowNo = sourceRowNo,
                    IsValid = rowValidationErrors.Count == 0,
                    Data = resultData,
                    ErrorMessage = rowErrorMessage,
                    ImportedAt = DateTime.UtcNow,
                    CompanyId = importContext?.CompanyId,
                    UserId = importContext?.UserId
                });

                if (rowResultBatch.Count >= RowResultBatchSize)
                {
                    await _rowResultStore.BulkInsertAsync(rowResultBatch, cancellationToken);
                    rowResultBatch.Clear();
                }

                if (rowValidationErrors.Count > 0)
                {
                    invalidRows++;
                    AddValidationErrors(validationErrors, rowValidationErrors);
                    continue;
                }

                validRows++;
            }

            if (matched)
                break;
        }
        while (reader.NextResult());

        if (rowResultBatch.Count > 0)
            await _rowResultStore.BulkInsertAsync(rowResultBatch, cancellationToken);

        if (worksheetMatch is null)
        {
            _logger.LogWarning(
                "Supplier price workbook did not match any known profile. {ErrorCode} ImportType={ImportType} FileExtension={FileExtension} FileSizeBytes={FileSizeBytes}.",
                PortalErrorCodes.ExcelImportValidationFailed,
                definition.ImportType,
                Path.GetExtension(file.FileName),
                file.Length);
            return CreateFailure(batchId, definition.NoMatchMessage, definition);
        }

        if (rowNo == 0)
            validationErrors.Add("Filen innehåller inga datarader att importera efter identifierad rubrikrad.");

        _logger.LogInformation(
            "Supplier price row scan completed in {ElapsedMs} ms. ImportType={ImportType} Supplier={Supplier} SheetName={SheetName} CandidateRows={CandidateRows} ValidRows={ValidRows} InvalidRows={InvalidRows}.",
            rowScanWatch.ElapsedMilliseconds,
            definition.ImportType,
            worksheetMatch.Profile.Supplier,
            worksheetMatch.SheetName,
            rowNo,
            validRows,
            invalidRows);

        if (validationErrors.Count > 0)
        {
            var errors = validationErrors
                .Prepend(definition.ValidationStoppedBeforeStagingMessage)
                .ToList();

            _logger.LogWarning(
                "Supplier price import stopped before staging. {ErrorCode} ImportType={ImportType} Supplier={Supplier} CandidateRows={CandidateRows} ValidRows={ValidRows} InvalidRows={InvalidRows}.",
                PortalErrorCodes.ExcelImportValidationFailed,
                definition.ImportType,
                worksheetMatch.Profile.Supplier,
                rowNo,
                validRows,
                invalidRows);
            return _resultFactory.Create(new ExcelImportResultCreateRequest
            {
                ImportType = definition.ImportType,
                ImportBatchId = batchId,
                TotalRows = rowNo,
                ValidRows = validRows,
                InvalidRows = invalidRows,
                Errors = errors,
                RowHeaders = definition.ResultHeaders,
                RowResults = rowResultPreview
            });
        }

        if (validRows == 0)
        {
            return _resultFactory.Create(new ExcelImportResultCreateRequest
            {
                ImportType = definition.ImportType,
                ImportBatchId = batchId,
                TotalRows = rowNo,
                ValidRows = 0,
                InvalidRows = invalidRows,
                Errors = validationErrors,
                RowHeaders = definition.ResultHeaders,
                RowResults = rowResultPreview
            });
        }

        try
        {
            var stagingRows = StreamValidatedRows(
                file,
                worksheetMatch,
                importedBy,
                batchId,
                importContext,
                cancellationToken);
            await _repository.BulkInsertAsync(stagingRows, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsTransientProcessingFailure(ex))
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Supplier price staging insert failed. {ErrorCode} ImportType={ImportType} Supplier={Supplier} SheetName={SheetName} RowCount={RowCount} ValidRows={ValidRows} InvalidRows={InvalidRows}.",
                definition.StagingFailedErrorCode,
                definition.ImportType,
                worksheetMatch.Profile.Supplier,
                worksheetMatch.SheetName,
                validRows,
                validRows,
                invalidRows);
            return CreateFailure(batchId, definition.StagingFailureMessage, definition, rowResultPreview);
        }

        _logger.LogInformation(
            "Supplier price import completed in {ElapsedMs} ms. ImportType={ImportType} Supplier={Supplier} SheetName={SheetName} CandidateRows={CandidateRows} ValidRows={ValidRows} InvalidRows={InvalidRows}.",
            totalWatch.ElapsedMilliseconds,
            definition.ImportType,
            worksheetMatch.Profile.Supplier,
            worksheetMatch.SheetName,
            rowNo,
            validRows,
            invalidRows);

        return _resultFactory.Create(new ExcelImportResultCreateRequest
        {
            ImportType = definition.ImportType,
            ImportBatchId = batchId,
            TotalRows = rowNo,
            ValidRows = validRows,
            InvalidRows = invalidRows,
            StagedRows = validRows,
            Errors = Array.Empty<string>(),
            RowHeaders = definition.ResultHeaders,
            RowResults = rowResultPreview
        });
    }

    private static IEnumerable<PortalSupplierPriceStagingRow> StreamValidatedRows(
        IFormFile file,
        WorksheetProfileMatch match,
        string importedBy,
        Guid batchId,
        ExcelImportUserContext? importContext,
        CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();
        using var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration
        {
            LeaveOpen = false
        });

        do
        {
            if (!string.Equals(reader.Name, match.SheetName, StringComparison.OrdinalIgnoreCase))
                continue;

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceRowNo = reader.Depth + 1;
                if (sourceRowNo <= match.HeaderRowNo + match.Profile.FirstDataRowOffset - 1)
                    continue;
                if (sourceRowNo - match.HeaderRowNo > MaxRows)
                    yield break;

                var raw = ReadRawRow(reader, match.LastColumn);
                if (!raw.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
                    continue;
                if (IsStandaloneLabelRow(match, raw))
                    continue;

                var normalized = BuildNormalizedRow(match, raw, file.FileName, sourceRowNo);
                yield return new PortalSupplierPriceStagingRow
                {
                    ImportBatchId = batchId,
                    RowNo = sourceRowNo,
                    Supplier = normalized.Supplier,
                    SupplierArticleNo = normalized.SupplierArticleNo,
                    CustomerArticleNo = normalized.CustomerArticleNo,
                    Description = normalized.Description,
                    CurrencyCode = normalized.CurrencyCode,
                    ListPrice = normalized.ListPrice,
                    NetPrice = normalized.NetPrice,
                    DiscountPercent = normalized.DiscountPercent,
                    Uom = normalized.Uom,
                    MinimumOrderQuantity = normalized.MinimumOrderQuantity,
                    PackageQuantity = normalized.PackageQuantity,
                    WeightKg = normalized.WeightKg,
                    CountryOfOrigin = normalized.CountryOfOrigin,
                    TariffCode = normalized.TariffCode,
                    ValidFrom = normalized.ValidFrom,
                    ValidTo = normalized.ValidTo,
                    Category1 = normalized.Category1,
                    Category2 = normalized.Category2,
                    Category3 = normalized.Category3,
                    Category4 = normalized.Category4,
                    Category5 = normalized.Category5,
                    SourceFileName = normalized.SourceFileName,
                    SourceSheetName = normalized.SourceSheetName,
                    SourceRowNo = normalized.SourceRowNo,
                    RawJson = JsonSerializer.Serialize(raw, JsonOptions),
                    ImportedAt = DateTime.UtcNow,
                    ImportedBy = importedBy,
                    CompanyId = importContext?.CompanyId,
                    ForetagKod = importContext?.ForetagKod,
                    UserId = importContext?.UserId
                };
            }

            yield break;
        }
        while (reader.NextResult());
    }

    private ExcelImportResult CreateFailure(
        Guid batchId,
        string error,
        SupplierPriceImportDefinition definition,
        IReadOnlyList<ExcelImportRowResult>? rowResults = null)
        => _resultFactory.Create(new ExcelImportResultCreateRequest
        {
            ImportType = definition.ImportType,
            ImportBatchId = batchId,
            TotalRows = 0,
            ValidRows = 0,
            InvalidRows = 0,
            Errors = new[] { error },
            RowHeaders = definition.ResultHeaders,
            RowResults = rowResults ?? Array.Empty<ExcelImportRowResult>()
        });

    private static void AddValidationErrors(List<string> target, IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            if (target.Count >= MaxValidationErrors)
                return;

            target.Add(error);
        }
    }

    private static bool IsTransientProcessingFailure(Exception exception)
        => exception is IOException or TimeoutException or Microsoft.Data.SqlClient.SqlException;

    private static SupplierPriceProfile? FindWorksheetProfile(
        IReadOnlyList<SupplierPriceProfile> profiles,
        IReadOnlyDictionary<string, int> headerMap)
        => profiles.FirstOrDefault(profile => profile.RequiredHeaderSignals.All(signal => FindHeaderColumn(headerMap, signal) > 0));

    private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> headerCells, int lastColumn)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= lastColumn; column++)
        {
            var normalized = SupplierPriceImportText.NormalizeHeader(headerCells[column - 1]);
            if (string.IsNullOrWhiteSpace(normalized) || headers.ContainsKey(normalized))
                continue;

            headers[normalized] = column;
        }

        return headers;
    }

    private static Dictionary<string, string> ReadRawRow(IExcelDataReader reader, int lastColumn)
    {
        var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= lastColumn; column++)
        {
            raw[$"Column{column}"] = SupplierPriceImportText.ToSafeCellText(reader.GetValue(column - 1));
        }

        return raw;
    }

    private static List<string> ReadCurrentRow(IExcelDataReader reader, int minimumColumns)
    {
        var columnCount = Math.Max(reader.FieldCount, minimumColumns);
        var cells = new List<string>(columnCount);
        for (var column = 0; column < columnCount; column++)
        {
            cells.Add(SupplierPriceImportText.ToSafeCellText(reader.GetValue(column)));
        }

        return cells;
    }

    private static int LastNonEmptyColumn(IReadOnlyList<string> cells)
    {
        for (var index = cells.Count - 1; index >= 0; index--)
        {
            if (!string.IsNullOrWhiteSpace(cells[index]))
                return index + 1;
        }

        return 0;
    }

    private static SupplierPriceNormalizedRow BuildNormalizedRow(
        WorksheetProfileMatch match,
        IReadOnlyDictionary<string, string> raw,
        string fileName,
        int sourceRowNo)
    {
        string? Text(SupplierPriceFieldMapping mapping) => ReadText(match, raw, mapping);
        decimal? Decimal(SupplierPriceFieldMapping mapping) => SupplierPriceImportText.TryParseDecimal(Text(mapping), out var value) ? value : null;
        DateTime? Date(SupplierPriceFieldMapping mapping) => SupplierPriceImportText.TryParseDate(Text(mapping), out var value) ? value : null;

        var currency = Text(match.Profile.CurrencyCode);
        if (string.IsNullOrWhiteSpace(currency))
            currency = match.Profile.DefaultCurrency;

        var netPrice = Decimal(match.Profile.NetPrice);
        if (netPrice == 0m)
            netPrice = null;

        return new SupplierPriceNormalizedRow
        {
            Supplier = match.Profile.Supplier,
            SupplierArticleNo = Text(match.Profile.SupplierArticleNo),
            CustomerArticleNo = Text(match.Profile.CustomerArticleNo),
            Description = Text(match.Profile.Description),
            CurrencyCode = SupplierPriceImportText.NormalizeCurrency(currency),
            ListPrice = Decimal(match.Profile.ListPrice),
            NetPrice = netPrice,
            DiscountPercent = Decimal(match.Profile.DiscountPercent),
            Uom = Text(match.Profile.Uom),
            MinimumOrderQuantity = Decimal(match.Profile.MinimumOrderQuantity),
            PackageQuantity = Decimal(match.Profile.PackageQuantity),
            WeightKg = Decimal(match.Profile.WeightKg),
            CountryOfOrigin = Text(match.Profile.CountryOfOrigin),
            TariffCode = Text(match.Profile.TariffCode),
            ValidFrom = Date(match.Profile.ValidFrom),
            ValidTo = Date(match.Profile.ValidTo),
            Category1 = Text(match.Profile.Category1),
            Category2 = Text(match.Profile.Category2),
            Category3 = Text(match.Profile.Category3),
            Category4 = Text(match.Profile.Category4),
            Category5 = Text(match.Profile.Category5),
            SourceFileName = fileName,
            SourceSheetName = match.SheetName,
            SourceRowNo = sourceRowNo
        };
    }

    private static string? ReadText(WorksheetProfileMatch match, IReadOnlyDictionary<string, string> raw, SupplierPriceFieldMapping mapping)
    {
        if (mapping.Kind == SupplierPriceFieldMappingKind.None)
            return null;
        if (mapping.Kind == SupplierPriceFieldMappingKind.Constant)
            return mapping.Value;
        if (mapping.Kind == SupplierPriceFieldMappingKind.Cell)
            return match.SheetCells.TryGetValue(CellKey(mapping.Row, mapping.Column), out var cellValue)
                ? cellValue.Trim()
                : null;

        var column = mapping.Kind == SupplierPriceFieldMappingKind.Column
            ? mapping.Column
            : FindHeaderColumn(match.HeaderMap, mapping.Value);

        return column > 0 && raw.TryGetValue($"Column{column}", out var value)
            ? value.Trim()
            : null;
    }

    private static bool IsStandaloneLabelRow(
        WorksheetProfileMatch match,
        IReadOnlyDictionary<string, string> raw)
    {
        if (!match.Profile.IgnoreStandaloneLabelRows)
            return false;

        var articleText = ReadText(match, raw, match.Profile.SupplierArticleNo);
        if (string.IsNullOrWhiteSpace(articleText)
            || articleText.Length < 12
            || !articleText.Any(char.IsWhiteSpace))
        {
            return false;
        }

        SupplierPriceFieldMapping[] productValueMappings =
        [
            match.Profile.CustomerArticleNo,
            match.Profile.Description,
            match.Profile.ListPrice,
            match.Profile.NetPrice,
            match.Profile.DiscountPercent,
            match.Profile.Uom,
            match.Profile.MinimumOrderQuantity,
            match.Profile.PackageQuantity,
            match.Profile.WeightKg,
            match.Profile.CountryOfOrigin,
            match.Profile.TariffCode,
            match.Profile.ValidFrom,
            match.Profile.ValidTo,
            match.Profile.Category1,
            match.Profile.Category2,
            match.Profile.Category3,
            match.Profile.Category4,
            match.Profile.Category5
        ];

        return productValueMappings.All(mapping => string.IsNullOrWhiteSpace(ReadText(match, raw, mapping)));
    }

    private static int FindHeaderColumn(IReadOnlyDictionary<string, int> headerMap, string? signal)
    {
        if (string.IsNullOrWhiteSpace(signal))
            return 0;

        if (headerMap.TryGetValue(signal, out var exactColumn))
            return exactColumn;

        var match = headerMap.FirstOrDefault(header => header.Key.Contains(signal, StringComparison.OrdinalIgnoreCase));
        return match.Value;
    }

    private static void StoreScannedCells(Dictionary<string, string> sheetCells, int rowNo, IReadOnlyList<string> cells)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            var value = cells[index];
            if (!string.IsNullOrWhiteSpace(value))
                sheetCells[CellKey(rowNo, index + 1)] = value;
        }
    }

    private static string CellKey(int row, int column) => $"R{row}C{column}";

    private static IEnumerable<string> Validate(SupplierPriceNormalizedRow row, int sourceRowNo)
    {
        if (string.IsNullOrWhiteSpace(row.SupplierArticleNo))
            yield return $"Rad {sourceRowNo}: artikelnummer saknas.";

        if (row.ListPrice is null)
            yield return $"Rad {sourceRowNo}: listpris saknas eller kunde inte tolkas.";
        else if (row.ListPrice < 0)
            yield return $"Rad {sourceRowNo}: listpris får inte vara negativt.";

        if (row.NetPrice < 0)
            yield return $"Rad {sourceRowNo}: nettopris får inte vara negativt.";

        if (string.IsNullOrWhiteSpace(row.CurrencyCode))
            yield return $"Rad {sourceRowNo}: valuta saknas och kunde inte härledas från leverantörsprofilen.";
        else if (row.CurrencyCode.Length != 3)
            yield return $"Rad {sourceRowNo}: valuta '{row.CurrencyCode}' måste vara tre bokstäver.";
    }

    private static Dictionary<string, string> BuildResultData(SupplierPriceNormalizedRow row)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Supplier"] = row.Supplier ?? string.Empty,
            ["SupplierArticleNo"] = row.SupplierArticleNo ?? string.Empty,
            ["CustomerArticleNo"] = row.CustomerArticleNo ?? string.Empty,
            ["Description"] = row.Description ?? string.Empty,
            ["CurrencyCode"] = row.CurrencyCode ?? string.Empty,
            ["ListPrice"] = SupplierPriceImportText.Format(row.ListPrice),
            ["NetPrice"] = SupplierPriceImportText.Format(row.NetPrice),
            ["DiscountPercent"] = SupplierPriceImportText.Format(row.DiscountPercent),
            ["Uom"] = row.Uom ?? string.Empty,
            ["MinimumOrderQuantity"] = SupplierPriceImportText.Format(row.MinimumOrderQuantity),
            ["PackageQuantity"] = SupplierPriceImportText.Format(row.PackageQuantity),
            ["WeightKg"] = SupplierPriceImportText.Format(row.WeightKg),
            ["CountryOfOrigin"] = row.CountryOfOrigin ?? string.Empty,
            ["TariffCode"] = row.TariffCode ?? string.Empty,
            ["ValidFrom"] = row.ValidFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            ["ValidTo"] = row.ValidTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            ["Category1"] = row.Category1 ?? string.Empty,
            ["Category2"] = row.Category2 ?? string.Empty,
            ["Category3"] = row.Category3 ?? string.Empty,
            ["Category4"] = row.Category4 ?? string.Empty,
            ["Category5"] = row.Category5 ?? string.Empty,
            ["SourceSheetName"] = row.SourceSheetName ?? string.Empty,
            ["SourceRowNo"] = row.SourceRowNo?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
        };

    private sealed record WorksheetProfileMatch(
        string SheetName,
        SupplierPriceProfile Profile,
        int HeaderRowNo,
        int LastColumn,
        IReadOnlyDictionary<string, int> HeaderMap,
        IReadOnlyDictionary<string, string> SheetCells);
}

public interface ISupplierPriceStagingRepository
{
    Task<bool> TableExistsAsync(CancellationToken cancellationToken = default);

    Task BulkInsertAsync(IEnumerable<PortalSupplierPriceStagingRow> rows, CancellationToken cancellationToken = default);
}

public sealed class SupplierPriceImportDefinition
{
    public required string ImportType { get; init; }
    public required IReadOnlyList<SupplierPriceProfile> Profiles { get; init; }
    public required IReadOnlyList<string> ResultHeaders { get; init; }
    public required string MissingStagingTableMessage { get; init; }
    public required string ValidationStoppedBeforeStagingMessage { get; init; }
    public required string UnsupportedWorkbookMessage { get; init; }
    public required string UnsupportedImportMessage { get; init; }
    public required string EmptyFileMessage { get; init; }
    public required string NoMatchMessage { get; init; }
    public required string StagingFailureMessage { get; init; }
    public required string GenericFailureMessage { get; init; }
    public required string ImportFailedErrorCode { get; init; }
    public required string WorkbookReadFailedErrorCode { get; init; }
    public required string StagingFailedErrorCode { get; init; }
}

public sealed record SupplierPriceProfile(
    string Supplier,
    IReadOnlyList<string> RequiredHeaderSignals,
    string? DefaultCurrency,
    int FirstDataRowOffset,
    SupplierPriceFieldMapping SupplierArticleNo,
    SupplierPriceFieldMapping CustomerArticleNo,
    SupplierPriceFieldMapping Description,
    SupplierPriceFieldMapping CurrencyCode,
    SupplierPriceFieldMapping ListPrice,
    SupplierPriceFieldMapping NetPrice,
    SupplierPriceFieldMapping DiscountPercent,
    SupplierPriceFieldMapping Uom,
    SupplierPriceFieldMapping MinimumOrderQuantity,
    SupplierPriceFieldMapping PackageQuantity,
    SupplierPriceFieldMapping WeightKg,
    SupplierPriceFieldMapping CountryOfOrigin,
    SupplierPriceFieldMapping TariffCode,
    SupplierPriceFieldMapping ValidFrom,
    SupplierPriceFieldMapping ValidTo,
    SupplierPriceFieldMapping Category1,
    SupplierPriceFieldMapping Category2,
    SupplierPriceFieldMapping Category3,
    SupplierPriceFieldMapping Category4,
    SupplierPriceFieldMapping Category5,
    bool IgnoreStandaloneLabelRows = false);

public sealed record SupplierPriceFieldMapping(SupplierPriceFieldMappingKind Kind, string? Value, int Column, int Row)
{
    public static SupplierPriceFieldMapping None() => new(SupplierPriceFieldMappingKind.None, null, 0, 0);
    public static SupplierPriceFieldMapping Header(string normalizedHeader) => new(SupplierPriceFieldMappingKind.Header, normalizedHeader, 0, 0);
    public static SupplierPriceFieldMapping ByColumn(int column) => new(SupplierPriceFieldMappingKind.Column, null, column, 0);
    public static SupplierPriceFieldMapping ByCell(int row, int column) => new(SupplierPriceFieldMappingKind.Cell, null, column, row);
    public static SupplierPriceFieldMapping Constant(string value) => new(SupplierPriceFieldMappingKind.Constant, value, 0, 0);
}

public enum SupplierPriceFieldMappingKind
{
    None,
    Header,
    Column,
    Cell,
    Constant
}

public sealed class SupplierPriceNormalizedRow
{
    public string? Supplier { get; init; }
    public string? SupplierArticleNo { get; init; }
    public string? CustomerArticleNo { get; init; }
    public string? Description { get; init; }
    public string? CurrencyCode { get; init; }
    public decimal? ListPrice { get; init; }
    public decimal? NetPrice { get; init; }
    public decimal? DiscountPercent { get; init; }
    public string? Uom { get; init; }
    public decimal? MinimumOrderQuantity { get; init; }
    public decimal? PackageQuantity { get; init; }
    public decimal? WeightKg { get; init; }
    public string? CountryOfOrigin { get; init; }
    public string? TariffCode { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }
    public string? Category1 { get; init; }
    public string? Category2 { get; init; }
    public string? Category3 { get; init; }
    public string? Category4 { get; init; }
    public string? Category5 { get; init; }
    public string? SourceFileName { get; init; }
    public string? SourceSheetName { get; init; }
    public int? SourceRowNo { get; init; }
}

public static class SupplierPriceImportText
{
    public static string Format(decimal? value) =>
        value?.ToString("0.######", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string NormalizeHeader(string value)
    {
        return new string((value ?? string.Empty)
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    public static string? NormalizeCurrency(string? value)
    {
        var currency = (value ?? string.Empty).Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(currency) ? null : currency;
    }

    public static string ToSafeCellText(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }

    public static bool TryParseDecimal(string? raw, out decimal value)
    {
        var text = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "TBD", StringComparison.OrdinalIgnoreCase))
        {
            value = default;
            return false;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("sv-SE"), out value)
               || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryParseDate(string? raw, out DateTime value)
    {
        var text = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            value = default;
            return false;
        }

        return DateTime.TryParse(text, CultureInfo.GetCultureInfo("sv-SE"), DateTimeStyles.None, out value)
               || DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out value)
               || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }
}
