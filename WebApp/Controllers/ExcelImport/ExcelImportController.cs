using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.Application;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.ExcelImport;
using WebApp.Services.Application;
using WebApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebApp.ViewModels.ExcelImport;
using Microsoft.Extensions.Localization;
using WebApp.Observability;
using WebApp.Services.Integration;

namespace WebApp.Controllers
{
    // Handles Excel import uploads, edit sessions, and background-job dispatch for the portal.
    [Authorize(Roles = "Administrator,User,SuperUser")]
    public class ExcelImportController : Controller
    {
        private const int MaxFilesPerUpload = 20;
        private static readonly string[] AllowedExtensions = new[] { ".xls", ".xlsx", ".xlsm", ".csv" };
        private readonly IExcelImportService _excelImportService;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly ITenantGuard _tenantGuard;
        private readonly IFeatureAccessService _featureAccessService;
        private readonly ICompanyPermissionGuard _companyPermissionGuard;
        private readonly IExcelImportTableInitializationService _excelImportTableInitializationService;
        private readonly IExcelImportRuntimeStatusService _excelImportRuntimeStatusService;
        private readonly IExcelImportTransientStatusStore _excelImportTransientStatusStore;
        private readonly IExcelImportRowResultStore _excelImportRowResultStore;
        private readonly IStringLocalizer<SharedResources> _sharedLocalizer;
        private readonly ILogger<ExcelImportController> _logger;
        private static readonly Guid SubModuleExcelImportNewId = Guid.Parse("a2dfeb49-7a52-4e0b-9c8c-0b7a3a3fa41f");
        private const string ImportRowsFailureMessage = "Importen kunde inte slutföras. Kontakta support med felkod EXCEL_IMPORT_VALIDATION_FAILED.";
        private const string UploadFailureMessage = "Importen kunde inte slutföras. Kontakta support med felkod EXCEL_IMPORT_PROCESSING_FAILED.";
        private const string StorageConfigurationFailureMessage = "Excelimporten kan inte startas eftersom säker fillagring saknas. Kontakta support med felkod EXCEL_IMPORT_PROCESSING_FAILED.";

        [ActivatorUtilitiesConstructor]
        public ExcelImportController(
            IExcelImportService excelImportService,
            IHttpContextAccessor contextAccessor,
            ITenantGuard tenantGuard,
            IFeatureAccessService featureAccessService,
            ICompanyPermissionGuard companyPermissionGuard,
            IExcelImportTableInitializationService excelImportTableInitializationService,
            IExcelImportRuntimeStatusService excelImportRuntimeStatusService,
            IExcelImportTransientStatusStore excelImportTransientStatusStore,
            IExcelImportRowResultStore excelImportRowResultStore,
            IStringLocalizer<SharedResources> sharedLocalizer,
            ILogger<ExcelImportController> logger)
        {
            _excelImportService = excelImportService;
            _contextAccessor = contextAccessor;
            _tenantGuard = tenantGuard;
            _featureAccessService = featureAccessService;
            _companyPermissionGuard = companyPermissionGuard;
            _excelImportTableInitializationService = excelImportTableInitializationService;
            _excelImportRuntimeStatusService = excelImportRuntimeStatusService;
            _excelImportTransientStatusStore = excelImportTransientStatusStore;
            _excelImportRowResultStore = excelImportRowResultStore;
            _sharedLocalizer = sharedLocalizer;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? importType = null)
        {
            if (!IsFeatureAllowed())
                return Forbid();
            if (!await HasCompanyPermissionAsync())
                return Forbid();

            var selectedImportType = ExcelImportTypeDefinitions.Normalize(importType);
            if (string.IsNullOrWhiteSpace(selectedImportType) || !_excelImportService.IsSupportedImportType(selectedImportType))
            {
                selectedImportType = "voucher";
            }

            return View(new ExcelImportPageVm
            {
                ImportType = selectedImportType,
                VoucherPostingDate = DateTime.Today.ToString("yyyy-MM-dd"),
                RuntimeStatusItems = GetRuntimeStatusItems()
            });
        }

        [HttpGet]
        public async Task<IActionResult> RuntimeStatusPanel()
        {
            if (!IsFeatureAllowed())
                return Forbid();
            if (!await HasCompanyPermissionAsync())
                return Forbid();

            return PartialView("~/Views/ExcelImport/Partials/_ExcelImportRuntimeStatusPanel.cshtml", GetRuntimeStatusItems());
        }

        [HttpGet]
        public async Task<IActionResult> RuntimeRowsPanel(string aggregateKey, int page = 1, int pageSize = ExcelImportRowPaging.DefaultPageSize, bool showOnlyInvalidRows = false, bool showAllRows = false)
        {
            if (!IsFeatureAllowed())
                return Forbid();
            if (!await HasCompanyPermissionAsync())
                return Forbid();

            if (string.IsNullOrWhiteSpace(aggregateKey))
                return BadRequest();

            var item = GetRuntimeStatusItems(20, includeRows: true)
                .FirstOrDefault(candidate => string.Equals(candidate.AggregateKey, aggregateKey, StringComparison.OrdinalIgnoreCase));
            if (item is null)
                return NotFound();

            var importType = ResolveImportTypeKey(item);
            var headers = ResolveRowHeaders(item);
            var editUrl = item.LinkUrl;
            var sessionUser = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (sessionUser?.CompanyId is not Guid companyId || companyId == Guid.Empty)
                return Forbid();

            ExcelImportRuntimeRowsViewModel vm;
            if (UsesStoredRowResults(importType) && item.ImportBatchId.HasValue)
            {
                var storedRows = await _excelImportRowResultStore.GetPageAsync(
                    companyId,
                    importType,
                    item.ImportBatchId.Value,
                    page,
                    pageSize,
                    showOnlyInvalidRows,
                    showAllRows,
                    HttpContext.RequestAborted);

                vm = new ExcelImportRuntimeRowsViewModel
                {
                    AggregateKey = aggregateKey,
                    ImportType = importType,
                    Headers = headers,
                    Rows = storedRows.Rows,
                    Page = storedRows.Page,
                    PageSize = storedRows.PageSize,
                    TotalCount = storedRows.TotalCount,
                    FilteredCount = storedRows.FilteredCount,
                    TotalPages = storedRows.TotalPages,
                    ShowOnlyInvalidRows = storedRows.ShowOnlyInvalidRows,
                    ShowAllRows = storedRows.ShowAllRows,
                    EditUrl = editUrl
                };
            }
            else
            {
                var rowResults = ResolveRowResults(item);
                var pagedRows = ExcelImportRowPaging.Build(rowResults, page, pageSize, showOnlyInvalidRows, showAllRows);
                vm = new ExcelImportRuntimeRowsViewModel
                {
                    AggregateKey = aggregateKey,
                    ImportType = importType,
                    Headers = headers,
                    Rows = pagedRows.PageRows,
                    Page = pagedRows.Page,
                    PageSize = pagedRows.PageSize,
                    TotalCount = pagedRows.TotalCount,
                    FilteredCount = pagedRows.FilteredCount,
                    TotalPages = pagedRows.TotalPages,
                    ShowOnlyInvalidRows = pagedRows.ShowOnlyInvalidRows,
                    ShowAllRows = pagedRows.ShowAllRows,
                    EditUrl = editUrl
                };
            }

            return PartialView("~/Views/ExcelImport/Partials/_ExcelImportRuntimeRowsPanel.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> EditRecentImport(string aggregateKey, int page = 1, int pageSize = ExcelImportRowPaging.DefaultPageSize, bool showOnlyInvalidRows = false)
        {
            if (!IsFeatureAllowed())
                return Forbid();
            if (!await HasCompanyPermissionAsync())
                return Forbid();

            if (string.IsNullOrWhiteSpace(aggregateKey))
            {
                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", "Redigeringsläget kunde inte öppnas."],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }

            var recentItems = GetRuntimeStatusItems(20, includeRows: true);
            var item = recentItems.FirstOrDefault(candidate => string.Equals(candidate.AggregateKey, aggregateKey, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", "Den senaste importen kunde inte hittas."],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }

            var importType = ResolveImportTypeKey(item);
            if (string.IsNullOrWhiteSpace(importType))
            {
                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", "Importtypen kunde inte identifieras."],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }

            var rowHeaders = ResolveRowHeaders(item);
            var rowResults = ResolveRowResults(item);
            ExcelImportStoredRowPage? storedRows = null;
            var sessionUser = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (sessionUser?.CompanyId is not Guid companyId || companyId == Guid.Empty)
                return Forbid();

            if (UsesStoredRowResults(importType) && item.ImportBatchId.HasValue)
            {
                storedRows = await _excelImportRowResultStore.GetPageAsync(
                    companyId,
                    importType,
                    item.ImportBatchId.Value,
                    page,
                    pageSize,
                    showOnlyInvalidRows,
                    cancellationToken: HttpContext.RequestAborted);
                rowResults = storedRows.Rows;
            }
            if (rowHeaders.Count == 0 || (rowResults.Count == 0 && storedRows is null))
            {
                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", "Det finns inga importerade rader att redigera."],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }

            return View("Index", WithRuntimeStatus(BuildImportViewModel(
                result: new ExcelImportResult
                {
                    ImportType = importType,
                    ImportBatchId = item.ImportBatchId ?? Guid.NewGuid(),
                    EditSessionId = Guid.NewGuid(),
                    TotalRows = storedRows?.TotalCount ?? rowResults.Count,
                    ValidRows = item.ValidRows ?? rowResults.Count(row => row.IsValid),
                    InvalidRows = item.InvalidRows ?? rowResults.Count(row => !row.IsValid),
                    RowHeaders = rowHeaders,
                    RowResults = rowResults
                },
                file: new FormFile(Stream.Null, 0, 0, "file", $"{ExcelImportTypeDefinitions.GetDisplayName(importType)} (redigeringsläge)"),
                importType: importType,
                page: storedRows?.Page ?? page,
                pageSize: storedRows?.PageSize ?? pageSize,
                showOnlyInvalidRows: storedRows?.ShowOnlyInvalidRows ?? showOnlyInvalidRows,
                note: "Ändra raderna och importera igen.",
                cancelEditUrl: GetIndexUrl(),
                fileSizeKbOverride: "—",
                storedRowPage: storedRows)));
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnsureImportTables()
        {
            var result = await _excelImportTableInitializationService.EnsureImportTablesAsync(HttpContext.RequestAborted);
            var supportId = GetOrCreateSupportId();

            TempData["ExcelImportAdminMessageType"] = result.Success ? "success" : "danger";
            TempData["ExcelImportAdminMessage"] = result.Success
                ? _sharedLocalizer["ExcelImport_TableInitCompleted", result.Items.Count].Value
                : $"{_sharedLocalizer["ExcelImport_TableInitCompletedWithErrors"].Value} Referens: {supportId}.";

            TempData["ExcelImportAdminDetails"] = string.Join(Environment.NewLine,
                result.Items.Select(x =>
                    x.Success
                        ? $"{x.TableName}: {_sharedLocalizer["Ok"].Value}"
                        : $"{x.TableName}: {IntegrationLogSanitizer.Diagnostic(x.Message)} Referens: {supportId}"));

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload([FromForm(Name = "file")] List<IFormFile>? files, string importType = "voucher", string? voucherPostingDate = null, string? voucherReversalDate = null)
        {
            if (!IsFeatureAllowed())
                return Forbid();
            if (!await HasCompanyPermissionAsync())
                return Forbid();

            var selectedFiles = files?
                .Where(candidate => candidate is not null && candidate.Length > 0)
                .ToList() ?? new List<IFormFile>();

            if (selectedFiles.Count == 0)
            {
                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_NoFileSelected"],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }

            if (selectedFiles.Count > MaxFilesPerUpload)
            {
                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", $"Du kan importera max {MaxFilesPerUpload} filer åt gången."],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }

            var invalidExtensionFile = selectedFiles.FirstOrDefault(candidate =>
            {
                var extension = Path.GetExtension(candidate.FileName);
                return string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
            });
            if (invalidExtensionFile is not null)
            {
                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", $"{invalidExtensionFile.FileName}: {_sharedLocalizer["ExcelImport_OnlyExcelSupported"].Value}"],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }

            var tooLargeFile = selectedFiles.FirstOrDefault(candidate => candidate.Length > ExcelImportResourceLimits.MaxUploadBytes);
            if (tooLargeFile is not null)
            {
                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", $"{tooLargeFile.FileName}: Filen är för stor. Maximal filstorlek är 50 MB."],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }

            var type = (importType ?? "voucher").Trim().ToLowerInvariant();
            if (!_excelImportService.IsSupportedImportType(type))
            {
                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_TypeNotSupportedInUi", importType ?? string.Empty],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }

            if (string.Equals(type, "voucher", StringComparison.OrdinalIgnoreCase))
            {
                if (!ExcelImportDateParser.TryParsePostingDate(voucherPostingDate, out _))
                {
                    return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                    {
                        ImportType = type,
                        VoucherPostingDate = voucherPostingDate,
                        ImportMessage = _sharedLocalizer["ExcelImport_VoucherPostingDateRequired"],
                        ImportMessageType = "error",
                        ImportAlertClass = "alert-danger"
                    }));
                }

                if (!ExcelImportDateParser.TryParseOptionalDate(voucherReversalDate, out _))
                {
                    return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                    {
                        ImportType = type,
                        VoucherPostingDate = voucherPostingDate,
                        VoucherReversalDate = voucherReversalDate,
                        ImportMessage = "Återbokningsdatum måste vara ett giltigt datum.",
                        ImportMessageType = "error",
                        ImportAlertClass = "alert-danger"
                    }));
                }
            }

            var sessionUser = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (sessionUser?.CompanyId is not Guid companyId || companyId == Guid.Empty)
            {
                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", "Aktivt bolag saknas."],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }

            try
            {
                var jobs = new List<BackgroundJobSnapshot>(selectedFiles.Count);
                foreach (var selectedFile in selectedFiles)
                {
                    var job = await _excelImportService.QueueUploadAsync(
                        selectedFile,
                        new ExcelImportUploadRequest
                        {
                            ImportType = type,
                            ImportedBy = User?.Identity?.Name ?? sessionUser.Email ?? "unknown",
                            CreatedByUserId = sessionUser.UserId,
                            CreatedByEmail = sessionUser.Email,
                            CompanyId = companyId,
                            JeevesActiveCompany = sessionUser.JeevesActiveCompany,
                            VoucherPostingDate = voucherPostingDate,
                            VoucherReversalDate = voucherReversalDate
                        },
                        HttpContext.RequestAborted);
                    jobs.Add(job);
                }

                if (jobs.Count > 1)
                {
                    TempData["ExcelImportSuccessMessage"] = $"{jobs.Count} filer lades på kö.";
                    TempData["ExcelImportSuccessDetails"] = "Varje fil körs som ett separat importjobb.";
                }

                var focusRuntimeKey = jobs.Count == 1 ? $"excel-import:{type}:{jobs[0].Id:N}" : null;
                return RedirectToAction(nameof(Index), "ExcelImport", new
                {
                    focusRuntimeKey,
                    scrollTarget = "excel-runtime-status-slot"
                });
            }
            catch (ExcelImportStorageConfigurationException ex)
            {
                var supportId = GetOrCreateSupportId();
                _logger.LogError(
                    ex,
                    "Excel import storage is not configured. {ErrorCode} SupportId={SupportId} ImportType={ImportType} FileCount={FileCount} CompanyId={CompanyId}.",
                    PortalErrorCodes.ExcelImportProcessingFailed,
                    supportId,
                    type,
                    selectedFiles.Count,
                    companyId);

                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", $"{StorageConfigurationFailureMessage} Referens: {supportId}."],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }
            catch (Exception ex)
            {
                var supportId = GetOrCreateSupportId();
                _logger.LogError(
                    ex,
                    "Excel import upload failed. {ErrorCode} SupportId={SupportId} ImportType={ImportType} FileCount={FileCount} CompanyId={CompanyId}.",
                    PortalErrorCodes.ExcelImportProcessingFailed,
                    supportId,
                    type,
                    selectedFiles.Count,
                    companyId);

                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", $"{UploadFailureMessage} Referens: {supportId}."],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportEditedRows(string importType, Guid editSessionId, string rowsJson, string? voucherPostingDate = null, string? voucherReversalDate = null)
        {
            var type = ExcelImportTypeDefinitions.Normalize(importType);
            var contextResult = BuildEditSessionContext(type, voucherPostingDate, voucherReversalDate, editSessionId);
            if (contextResult.ErrorResult is not null)
            {
                return contextResult.ErrorResult;
            }

            return await ImportEditedRowsWithAdapterAsync(type, editSessionId, rowsJson, contextResult.Context);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEditSession(string importType, string? voucherPostingDate = null, string? voucherReversalDate = null)
        {
            var type = ExcelImportTypeDefinitions.Normalize(importType);
            var contextResult = BuildEditSessionContext(type, voucherPostingDate, voucherReversalDate, null, defaultVoucherPostingDate: true);
            if (contextResult.ErrorResult is not null)
            {
                return contextResult.ErrorResult;
            }

            return await CreateEmptyEditSessionWithAdapterAsync(type, contextResult.Context);
        }

        private (ExcelImportEditSessionContext Context, IActionResult? ErrorResult) BuildEditSessionContext(
            string importType,
            string? voucherPostingDate,
            string? voucherReversalDate,
            Guid? editSessionId,
            bool defaultVoucherPostingDate = false)
        {
            if (!RequiresVoucherContext(importType))
                return (new ExcelImportEditSessionContext(), null);

            return BuildVoucherEditSessionContext(importType, voucherPostingDate, voucherReversalDate, editSessionId, defaultVoucherPostingDate);
        }

        private (ExcelImportEditSessionContext Context, IActionResult? ErrorResult) BuildVoucherEditSessionContext(
            string importType,
            string? voucherPostingDate,
            string? voucherReversalDate,
            Guid? editSessionId,
            bool defaultVoucherPostingDate)
        {

            if (!ExcelImportDateParser.TryParsePostingDate(voucherPostingDate, out var postingDate))
            {
                if (defaultVoucherPostingDate)
                {
                    postingDate = DateTime.Today;
                }
                else
                {
                    return (new ExcelImportEditSessionContext(), View("Index", WithRuntimeStatus(new ExcelImportPageVm
                    {
                        ImportType = importType,
                        EditSessionId = editSessionId,
                        VoucherPostingDate = voucherPostingDate,
                        ImportMessage = _sharedLocalizer["ExcelImport_EnterVoucherPostingDateBeforeImport"],
                        ImportMessageType = "error",
                        ImportAlertClass = "alert-danger"
                    })));
                }
            }

            if (!ExcelImportDateParser.TryParseOptionalDate(voucherReversalDate, out var reversalDate))
            {
                return (new ExcelImportEditSessionContext(), View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportType = importType,
                    EditSessionId = editSessionId,
                    VoucherPostingDate = voucherPostingDate,
                    VoucherReversalDate = voucherReversalDate,
                    ImportMessage = "Återbokningsdatum måste vara ett giltigt datum.",
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                })));
            }

            return (new ExcelImportEditSessionContext
            {
                VoucherPostingDate = postingDate.Date,
                VoucherReversalDate = reversalDate?.Date
            }, null);
        }

        private static bool RequiresVoucherContext(string importType)
            => string.Equals(importType, "voucher", StringComparison.OrdinalIgnoreCase);

        private static int[] ExtractInvalidRowNumbers(System.Collections.Generic.List<string>? errors)
        {
            if (errors == null || errors.Count == 0)
                return System.Array.Empty<int>();

            var regex = new System.Text.RegularExpressions.Regex(@"^Rad\s+(\d+):", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            return errors.Select(e =>
                    {
                        var match = regex.Match(e);
                        return match.Success && int.TryParse(match.Groups[1].Value, out var rowNo) ? rowNo : (int?)null;
                    })
                .Where(r => r.HasValue)
                .Select(r => r!.Value)
                .Distinct()
                .OrderBy(r => r)
                .ToArray();
        }

        private ExcelImportPageVm BuildImportViewModel(
            ExcelImportResult result,
            IFormFile file,
            string importType,
            int page = 1,
            int pageSize = ExcelImportRowPaging.DefaultPageSize,
            bool showOnlyInvalidRows = false,
            string? note = null,
            string? cancelEditUrl = null,
            string? fileSizeKbOverride = null,
            ExcelImportStoredRowPage? storedRowPage = null)
        {
            var sizeKb = Math.Round(file.Length / 1024m, 1);
            var invalidRows = result.InvalidRows > 0 || (result.Errors?.Any() ?? false);
            var invalidRowNos = ExtractInvalidRowNumbers(result.Errors);
            var rowResults = result.RowResults ?? new System.Collections.Generic.List<ExcelImportRowResult>();
            var pagedRows = ExcelImportRowPaging.Build(rowResults, page, pageSize, showOnlyInvalidRows);
            var hasRowResults = rowResults.Count > 0;
            var isEmptyTemplateSession = result.EditSessionId.HasValue
                                       && result.TotalRows == 0
                                       && rowResults.Count == 1
                                       && !rowResults[0].IsValid
                                       && (rowResults[0].Data?.Values.All(v => string.IsNullOrWhiteSpace(v)) ?? false);
            var displayValidRows = storedRowPage is not null
                ? result.ValidRows
                : hasRowResults
                    ? rowResults.Count(r => r.IsValid)
                    : result.ValidRows;
            var displayInvalidRowsFromRows = storedRowPage is not null
                ? result.InvalidRows
                : hasRowResults
                    ? rowResults.Count(r => !r.IsValid)
                    : result.InvalidRows;
            if (isEmptyTemplateSession)
            {
                displayValidRows = 0;
                displayInvalidRowsFromRows = 0;
            }

            var displayInvalidRows = displayInvalidRowsFromRows == 0 && (result.Errors?.Any() ?? false)
                ? 1
                : displayInvalidRowsFromRows;

            var hasFatalValidationError = (result.Errors?.Any() ?? false) && result.TotalRows == 0;
            var typeDefinition = ExcelImportTypeDefinitions.Get(importType);
            var hasEditSession = result.EditSessionId.HasValue && !hasFatalValidationError && _excelImportService.IsEditSessionSupported(importType);
            var importTypeLabel = ExcelImportTypeDefinitions.GetDisplayName(importType);
            var validationHint = BuildValidationHint(importType, result.Errors);
            var importMessage = invalidRows
                ? _sharedLocalizer["ExcelImport_ImportCancelled", importTypeLabel, file.FileName, sizeKb]
                : _sharedLocalizer["ExcelImport_ImportCompleted", importTypeLabel, file.FileName, sizeKb, result.TotalRows, result.ImportBatchId];

            if (hasFatalValidationError)
            {
                importMessage = _sharedLocalizer["ExcelImport_FileCouldNotBeValidated", importTypeLabel.ToLowerInvariant()];
            }

            if (hasEditSession)
            {
                if (isEmptyTemplateSession)
                {
                    importMessage = _sharedLocalizer["ExcelImport_EmptyTemplateCreated", importTypeLabel];
                }
                else
                {
                    importMessage = invalidRows
                        ? _sharedLocalizer["ExcelImport_EditSessionCreatedWithErrors", importTypeLabel, file.FileName, sizeKb]
                        : _sharedLocalizer["ExcelImport_EditSessionCreatedReady", importTypeLabel, file.FileName, sizeKb];
                }
            }

            return new ExcelImportPageVm
            {
                ImportMessage = importMessage,
                ImportMessageType = invalidRows ? "error" : "success",
                ImportAlertClass = invalidRows ? "alert-danger" : "alert-success",
                ImportDetails = BuildDetails(
                    result,
                    displayValidRows,
                    displayInvalidRowsFromRows,
                    hasEditSession
                        ? _sharedLocalizer["ExcelImport_TemporaryEditSession"]
                        : hasFatalValidationError
                            ? validationHint
                            : null),
                ImportErrors = result.Errors ?? new System.Collections.Generic.List<string>(),
                IsFatalValidationError = hasFatalValidationError,
                ValidationHint = validationHint,
                ShowValidation = true,
                FileName = file.FileName,
                FileSizeKb = fileSizeKbOverride ?? sizeKb.ToString("0.0"),
                TotalRows = result.TotalRows,
                ValidRows = displayValidRows,
                InvalidRows = displayInvalidRows,
                RowPage = storedRowPage?.Page ?? pagedRows.Page,
                RowPageSize = storedRowPage?.PageSize ?? pagedRows.PageSize,
                RowTotalCount = storedRowPage?.TotalCount ?? pagedRows.TotalCount,
                RowFilteredCount = storedRowPage?.FilteredCount ?? pagedRows.FilteredCount,
                RowTotalPages = storedRowPage?.TotalPages ?? pagedRows.TotalPages,
                ShowOnlyInvalidRows = storedRowPage?.ShowOnlyInvalidRows ?? pagedRows.ShowOnlyInvalidRows,
                IsServerPagedRows = storedRowPage is not null,
                InvalidRowNos = invalidRowNos.ToList(),
                RowHeaders = result.RowHeaders ?? new System.Collections.Generic.List<string>(),
                RowResults = rowResults,
                VisibleRowResults = storedRowPage?.Rows ?? pagedRows.PageRows,
                ImportType = importType,
                ImportTypeDefinition = typeDefinition,
                CanEditSession = hasEditSession,
                EditSessionId = hasEditSession ? result.EditSessionId : null,
                VoucherPostingDate = result.VoucherPostingDate,
                VoucherReversalDate = result.VoucherReversalDate,
                CancelEditUrl = hasEditSession ? (cancelEditUrl ?? GetIndexUrl()) : null,
                RuntimeStatusItems = GetRuntimeStatusItems()
            };
        }

        private string BuildValidationHint(string? importType, System.Collections.Generic.List<string>? errors)
        {
            if (errors == null || errors.Count == 0)
                return _sharedLocalizer["ExcelImport_CheckFileStructure"];

            var importTypeLabel = ExcelImportTypeDefinitions.GetDisplayName(importType);
            var hasHeaderError = errors.Any(error => error.Contains("Fel rubrik i kolumn", StringComparison.OrdinalIgnoreCase));
            if (hasHeaderError)
            {
                return _sharedLocalizer["ExcelImport_ColumnsDoNotMatch", importTypeLabel];
            }

            return _sharedLocalizer["ExcelImport_ValidationStoppedImport"];
        }

        private string BuildDetails(ExcelImportResult result, int validRows, int invalidRows, string? note)
        {
            var details = _sharedLocalizer["ExcelImport_Details", result.TotalRows, validRows, invalidRows].Value;
            if (!string.IsNullOrWhiteSpace(note))
            {
                details = $"{details}. {note}";
            }

            return details;
        }

        private static string ResolveImportTypeKey(SidebarRuntimeStatusItemViewModel item)
        {
            return ExcelImportTypeDefinitions.ResolveRuntimeImportType(item);
        }

        private static bool UsesStoredRowResults(string? importType)
            => ExcelImportTypeDefinitions.IsKnown(importType);

        private static List<string> ResolveRowHeaders(SidebarRuntimeStatusItemViewModel item)
        {
            if (item.ColumnHeaders is { Count: > 0 })
                return item.ColumnHeaders.ToList();

            return item.ImportedRows
                .SelectMany(row => (row.Cells ?? new Dictionary<string, string>()).Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<ExcelImportRowResult> ResolveRowResults(SidebarRuntimeStatusItemViewModel item)
        {
            return (item.ImportedRows ?? new List<ExcelImportRuntimeRowViewModel>())
                .OrderBy(row => row.RowNo)
                .Select(row => new ExcelImportRowResult
                {
                    RowNo = row.RowNo,
                    IsValid = row.IsValid,
                    ErrorMessage = row.ErrorMessage,
                    Data = row.Cells is null
                        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(row.Cells, StringComparer.OrdinalIgnoreCase)
                })
                .ToList();
        }

        private async Task<IActionResult> ImportEditedRowsWithAdapterAsync(
            string importType,
            Guid editSessionId,
            string rowsJson,
            ExcelImportEditSessionContext context)
        {
            if (!IsFeatureAllowed())
                return Forbid();
            if (!await HasCompanyPermissionAsync())
                return Forbid();

            var user = User?.Identity?.Name ?? "unknown";
            ExcelImportResult result;
            try
            {
                result = await _excelImportService.ImportEditedRowsAsync(
                    new ExcelImportEditedRowsRequest
                    {
                        ImportType = importType,
                        EditSessionId = editSessionId,
                        RowsJson = rowsJson,
                        ImportedBy = user,
                        Context = context
                    },
                    HttpContext.RequestAborted);
            }
            catch (EditableImportRowLimitExceededException)
            {
                var adapterInfo = _excelImportService.GetEditSessionInfo(importType);
                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_EditSessionTooLarge", adapterInfo.MaxEditableRows],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }
            catch (InvalidExcelImportRowsException ex)
            {
                var supportId = GetOrCreateSupportId();
                _logger.LogWarning(
                    ex,
                    "Excel import edited rows were rejected. {ErrorCode} SupportId={SupportId} ImportType={ImportType} EditSessionId={EditSessionId}.",
                    PortalErrorCodes.ExcelImportValidationFailed,
                    supportId,
                    importType,
                    editSessionId);

                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", $"{ImportRowsFailureMessage} Referens: {supportId}."],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }
            catch (JsonException ex)
            {
                var supportId = GetOrCreateSupportId();
                _logger.LogWarning(
                    ex,
                    "Excel import edited rows JSON could not be parsed. {ErrorCode} SupportId={SupportId} ImportType={ImportType} EditSessionId={EditSessionId}.",
                    PortalErrorCodes.ExcelImportValidationFailed,
                    supportId,
                    importType,
                    editSessionId);

                return View("Index", WithRuntimeStatus(new ExcelImportPageVm
                {
                    ImportMessage = _sharedLocalizer["ExcelImport_Failed", $"{ImportRowsFailureMessage} Referens: {supportId}."],
                    ImportMessageType = "error",
                    ImportAlertClass = "alert-danger"
                }));
            }

            var adapter = _excelImportService.GetEditSessionInfo(importType);
            if (result.InvalidRows == 0 && !(result.Errors?.Any() ?? false))
            {
                RecordEditedImportSuccess(importType, result);
                if (TempData is not null)
                {
                    TempData["ExcelImportSuccessMessage"] = $"Importen för {ExcelImportTypeDefinitions.GetDisplayName(importType).ToLowerInvariant()} gick igenom.";
                    TempData["ExcelImportSuccessDetails"] = _sharedLocalizer["ExcelImport_Details", result.TotalRows, result.ValidRows, result.InvalidRows].Value;
                }
                var focusRuntimeKey = $"excel-import:{importType}:{result.ImportBatchId:N}";
                return RedirectToAction(nameof(Index), "ExcelImport", new
                {
                    focusRuntimeKey,
                    scrollTarget = "excel-runtime-status-slot"
                });
            }

            var fakeFile = new FormFile(Stream.Null, 0, 0, "file", adapter.EditSessionFileName);
            return View("Index", BuildImportViewModel(result, fakeFile, adapter.ImportType));
        }

        private async Task<IActionResult> CreateEmptyEditSessionWithAdapterAsync(
            string importType,
            ExcelImportEditSessionContext context)
        {
            if (!IsFeatureAllowed())
                return Forbid();
            if (!await HasCompanyPermissionAsync())
                return Forbid();

            var user = User?.Identity?.Name ?? "unknown";
            var result = await _excelImportService.CreateEmptyEditSessionAsync(
                new ExcelImportEditSessionRequest
                {
                    ImportType = importType,
                    ImportedBy = user,
                    Context = context
                },
                HttpContext.RequestAborted);
            var adapter = _excelImportService.GetEditSessionInfo(importType);
            var fakeFile = new FormFile(Stream.Null, 0, 0, "file", adapter.EditSessionFileName);
            return View("Index", BuildImportViewModel(result, fakeFile, adapter.ImportType));
        }

        private ExcelImportPageVm WithRuntimeStatus(ExcelImportPageVm vm)
        {
            vm.RuntimeStatusItems = GetRuntimeStatusItems();
            return vm;
        }

        private void RecordEditedImportSuccess(string importType, ExcelImportResult result)
        {
            var companyId = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject")?.CompanyId;
            if (companyId is not Guid value || value == Guid.Empty)
                return;

            var aggregateKey = $"excel-import:{importType}:{result.ImportBatchId:N}";
            var record = new SidebarRuntimeEventRecord
            {
                CompanyId = value,
                AggregateKey = aggregateKey,
                ImportBatchId = result.ImportBatchId,
                TotalRows = result.TotalRows,
                ValidRows = result.ValidRows,
                InvalidRows = result.InvalidRows,
                StagedRows = result.StagedRows,
                Source = "ExcelImport",
                Title = $"{ExcelImportTypeDefinitions.GetDisplayName(importType)} klar",
                Summary = $"{ExcelImportTypeDefinitions.GetDisplayName(importType)} importerades. Rader: {result.TotalRows}, giltiga: {result.ValidRows}.",
                LinkUrl = $"/ExcelImport/EditRecentImport?aggregateKey={Uri.EscapeDataString(aggregateKey)}&scrollTarget=excel-edit-table",
                StatusLabel = "Completed",
                StatusTone = "success",
                IconClass = "fas fa-file-excel",
                OccurredAtUtc = DateTimeOffset.UtcNow,
                ColumnHeaders = result.RowHeaders ?? new List<string>(),
                VoucherPostingDate = result.VoucherPostingDate,
                VoucherReversalDate = result.VoucherReversalDate,
                ImportedRows = (result.RowResults ?? new List<ExcelImportRowResult>())
                    .OrderBy(row => row.RowNo)
                    .Select(row => new ExcelImportRuntimeRowViewModel
                    {
                        RowNo = row.RowNo,
                        IsValid = row.IsValid,
                        ErrorMessage = row.ErrorMessage,
                        Cells = row.Data is null
                            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            : new Dictionary<string, string>(row.Data, StringComparer.OrdinalIgnoreCase)
                    })
                    .ToList()
            };

            _excelImportTransientStatusStore.Record(record);
        }

        private string GetOrCreateSupportId()
        {
            var supportId = _contextAccessor.HttpContext?.Items[PortalObservability.SupportIdItemKey]?.ToString();
            if (!string.IsNullOrWhiteSpace(supportId))
                return supportId;

            supportId = Guid.NewGuid().ToString("N")[..8];
            if (_contextAccessor.HttpContext is not null)
                _contextAccessor.HttpContext.Items[PortalObservability.SupportIdItemKey] = supportId;

            return supportId;
        }

        private string GetIndexUrl()
            => Url?.Action(nameof(Index), "ExcelImport") ?? "/ExcelImport";

        private IReadOnlyList<SidebarRuntimeStatusItemViewModel> GetRuntimeStatusItems(int take = 5, bool includeRows = false)
        {
            var sessionUser = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            return includeRows
                ? _excelImportRuntimeStatusService.GetRecentItems(sessionUser?.CompanyId, take)
                : _excelImportRuntimeStatusService.GetRecentSummaries(sessionUser?.CompanyId, take);
        }

        private bool IsFeatureAllowed()
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var validation = _tenantGuard.Validate(user);
            if (!validation.Success || user?.JeevesActiveCompany is null)
                return false;

            return _featureAccessService.IsEnabled(_contextAccessor.HttpContext!.Session, user.JeevesActiveCompany.Value, FeatureFlag.ExcelImport);
        }

        private async Task<bool> HasCompanyPermissionAsync()
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is null) return false;
            return await _companyPermissionGuard.HasAccessAsync(user.CompanyId.Value, SubModuleExcelImportNewId);
        }
}
}
