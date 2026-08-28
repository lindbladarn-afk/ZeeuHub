using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using WebApp.Helpers;
using WebApp.Controllers;
using WebApp.Models.Application;
using WebApp.Models.BackgroundJobs;
using WebApp.Models.ControlPanel;
using WebApp.Observability;
using WebApp.Services.Application;
using WebApp.Services;
using WebApp.Services.ExcelImport;
using WebApp.ViewModels.ExcelImport;

namespace WebApp.Tests;

// Verifies completed Excel imports can re-open in edit mode without any persistent draft storage.
public sealed class ExcelImportControllerEditRecentImportTests
{
    [Fact]
    public async Task EditRecentImport_Returns_Edit_View_For_Completed_Runtime_Item()
    {
        var fixtures = CreateController();

        var result = await fixtures.Controller.EditRecentImport(fixtures.AggregateKey);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", viewResult.ViewName);

        var model = Assert.IsType<ExcelImportPageVm>(viewResult.Model);
        Assert.Equal("budget", model.ImportType);
        Assert.NotNull(model.EditSessionId);
        Assert.True(model.ShowValidation);
        Assert.EndsWith("(redigeringsläge)", model.FileName);
        Assert.Contains("ExcelImport_EditSessionCreatedReady", model.ImportMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ExcelImport_TemporaryEditSession", model.ImportDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/ExcelImport", model.CancelEditUrl);
        Assert.Single(model.RowResults);
        Assert.Equal(2, model.RowHeaders.Count);
        Assert.Equal("A1", model.RowResults[0].Data["KolumnA"]);
    }

    [Fact]
    public async Task ImportEditedRows_Redirects_And_Records_Success_Event()
    {
        var fixtures = CreateController(new SuccessfulBudgetImportService());

        var result = await fixtures.Controller.ImportEditedRows("budget", Guid.NewGuid(), "[]");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ExcelImportController.Index), redirect.ActionName);
        Assert.Equal("ExcelImport", redirect.ControllerName);
        Assert.Equal("excel-runtime-status-slot", redirect.RouteValues?["scrollTarget"]);
        Assert.StartsWith("excel-import:budget:", Assert.IsType<string>(redirect.RouteValues?["focusRuntimeKey"]));
        Assert.NotNull(fixtures.TransientStore.LastRecord);
        Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), fixtures.TransientStore.LastRecord!.ImportBatchId);
        Assert.Equal("Completed", fixtures.TransientStore.LastRecord!.StatusLabel);
        Assert.Contains("Budget", fixtures.TransientStore.LastRecord.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Budget", fixtures.TransientStore.LastRecord.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("/ExcelImport/EditRecentImport?aggregateKey=", fixtures.TransientStore.LastRecord.LinkUrl);
    }

    [Fact]
    public async Task ImportEditedRows_Forwards_Modified_RowPayload_To_Service()
    {
        var service = new CapturingBudgetImportService();
        var fixtures = CreateController(service);
        var rowsJson = """
            [
              {
                "rowNo": 1,
                "data": {
                  "KolumnA": "Ändrad",
                  "KolumnB": "B1"
                }
              }
            ]
            """;

        var result = await fixtures.Controller.ImportEditedRows("budget", Guid.NewGuid(), rowsJson);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ExcelImportController.Index), redirect.ActionName);
        Assert.Equal("ExcelImport", redirect.ControllerName);
        Assert.Equal(rowsJson, service.LastRowsJson);
    }

    [Fact]
    public async Task ImportEditedRows_Returns_Error_For_Invalid_RowJson()
    {
        var fixtures = CreateController(new SensitiveInvalidRowsBudgetImportService());

        var result = await fixtures.Controller.ImportEditedRows("budget", Guid.NewGuid(), "[]");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", viewResult.ViewName);

        var model = Assert.IsType<ExcelImportPageVm>(viewResult.Model);
        Assert.Equal("error", model.ImportMessageType);
        Assert.NotNull(model.ImportMessage);
        Assert.Equal("alert-danger", model.ImportAlertClass);
        Assert.Contains("Referens: 4f892abc", model.ImportMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", model.ImportMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fixtures.Logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("SupportId=4f892abc", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(fixtures.TransientStore.Records);
    }

    [Fact]
    public async Task Upload_Rejects_Files_Over_Max_Size_Before_Queueing()
    {
        var fixtures = CreateController(new ThrowingExcelImportService());
        var tooLargeFile = new FormFile(Stream.Null, 0, 50L * 1024L * 1024L + 1L, "file", "budget.xlsx");

        var result = await fixtures.Controller.Upload(new List<IFormFile> { tooLargeFile }, "budget");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", viewResult.ViewName);
        var model = Assert.IsType<ExcelImportPageVm>(viewResult.Model);
        Assert.Equal("error", model.ImportMessageType);
        Assert.Equal("alert-danger", model.ImportAlertClass);
    }

    [Fact]
    public async Task Upload_Rejects_Unsupported_File_Extension_Before_Queueing()
    {
        var fixtures = CreateController(new ThrowingExcelImportService());
        var unsupportedFile = new FormFile(Stream.Null, 0, 128, "file", "budget.txt");

        var result = await fixtures.Controller.Upload(new List<IFormFile> { unsupportedFile }, "budget");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", viewResult.ViewName);
        var model = Assert.IsType<ExcelImportPageVm>(viewResult.Model);
        Assert.Equal("error", model.ImportMessageType);
        Assert.Equal("alert-danger", model.ImportAlertClass);
    }

    [Fact]
    public async Task Upload_Queues_Each_Selected_File_As_Separate_Background_Job()
    {
        var importService = new QueuingExcelImportService();
        var fixtures = CreateController(importService);
        var firstFile = new FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "file", "first.xlsx");
        var secondFile = new FormFile(new MemoryStream(new byte[] { 2 }), 0, 1, "file", "second.xlsx");

        var result = await fixtures.Controller.Upload(new List<IFormFile> { firstFile, secondFile }, "budget");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ExcelImportController.Index), redirect.ActionName);
        Assert.Equal("ExcelImport", redirect.ControllerName);
        Assert.Equal("excel-runtime-status-slot", redirect.RouteValues?["scrollTarget"]);
        Assert.Null(redirect.RouteValues?["focusRuntimeKey"]);
        Assert.Equal(new[] { "first.xlsx", "second.xlsx" }, importService.QueuedFiles.Select(file => file.FileName));
        Assert.All(importService.QueuedRequests, request => Assert.Equal("budget", request.ImportType));
    }

    [Fact]
    public async Task Upload_Returns_Safe_Error_When_Queueing_Fails()
    {
        var importService = new ThrowingQueueExcelImportService();
        var fixtures = CreateController(importService);
        var file = new FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "file", "budget.xlsx");

        var result = await fixtures.Controller.Upload(new List<IFormFile> { file }, "budget");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", viewResult.ViewName);
        var model = Assert.IsType<ExcelImportPageVm>(viewResult.Model);
        Assert.Equal("error", model.ImportMessageType);
        Assert.Equal("alert-danger", model.ImportAlertClass);
        Assert.Contains("Referens: 4f892abc", model.ImportMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", model.ImportMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fixtures.Logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("SupportId=4f892abc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Upload_Explains_Missing_Secure_Storage_Without_Exposing_Internal_Details()
    {
        var fixtures = CreateController(new MissingStorageExcelImportService());
        var file = new FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "file", "budget.xlsx");

        var result = await fixtures.Controller.Upload(new List<IFormFile> { file }, "budget");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", viewResult.ViewName);
        var model = Assert.IsType<ExcelImportPageVm>(viewResult.Model);
        Assert.Equal("error", model.ImportMessageType);
        Assert.Contains("säker fillagring saknas", model.ImportMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Referens: 4f892abc", model.ImportMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal-storage-path", model.ImportMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fixtures.Logger.Entries, entry =>
            entry.Level == LogLevel.Error &&
            entry.Message.Contains("storage is not configured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureImportTables_Returns_Safe_Admin_Details_With_SupportReference()
    {
        var fixtures = CreateController(
            excelImportService: new ThrowingExcelImportService(),
            initializationService: new ReportingExcelImportTableInitializationService());

        var result = await fixtures.Controller.EnsureImportTables();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ExcelImportController.Index), redirect.ActionName);
        Assert.Equal("danger", fixtures.Controller.TempData["ExcelImportAdminMessageType"]?.ToString());
        var message = fixtures.Controller.TempData["ExcelImportAdminMessage"]?.ToString();
        Assert.Contains("Referens: 4f892abc", message, StringComparison.OrdinalIgnoreCase);
        var details = fixtures.Controller.TempData["ExcelImportAdminDetails"]?.ToString();
        Assert.Contains("Referens: 4f892abc", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", details, StringComparison.OrdinalIgnoreCase);
    }

    private static ControllerFixtures CreateController(
        IExcelImportService? excelImportService = null,
        IExcelImportTableInitializationService? initializationService = null)
    {
        var companyId = Guid.NewGuid();
        var aggregateKey = "excel-import:budget:job-1";
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = CreateHttpContext(companyId)
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var transientStore = new ExcelImportTransientStatusStore(cache);
        var logger = new CapturingLogger<ExcelImportController>();
        transientStore.Record(new SidebarRuntimeEventRecord
        {
            CompanyId = companyId,
            AggregateKey = aggregateKey,
            Source = "ExcelImport",
            Title = "Budget",
            Summary = "Budget importerades. Rader: 1, giltiga: 1.",
            LinkUrl = $"/ExcelImport/EditRecentImport?aggregateKey={Uri.EscapeDataString(aggregateKey)}",
            StatusLabel = "Completed",
            StatusTone = "success",
            IconClass = "fas fa-file-excel",
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ColumnHeaders = new List<string> { "KolumnA", "KolumnB" },
            ImportedRows = new List<ExcelImportRuntimeRowViewModel>
            {
                new()
                {
                    RowNo = 1,
                    IsValid = true,
                    Cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["KolumnA"] = "A1",
                        ["KolumnB"] = "B1"
                    }
                }
            }
        });

        var transientStatusStore = new RecordingTransientStatusStore(transientStore);
        var controller = new ExcelImportController(
            excelImportService ?? new SuccessfulBudgetImportService(),
            httpContextAccessor,
            new AllowAllTenantGuard(),
            new AllowAllFeatureAccessService(),
            new AllowAllCompanyPermissionGuard(),
            initializationService ?? new ThrowingExcelImportTableInitializationService(),
            new ExcelImportRuntimeStatusService(transientStore),
            transientStatusStore,
            new EmptyExcelImportRowResultStore(),
            new DummyStringLocalizer(),
            logger);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContextAccessor.HttpContext
        };
        controller.TempData = new TempDataDictionary(httpContextAccessor.HttpContext, new TestTempDataProvider());

        return new ControllerFixtures(controller, aggregateKey, transientStatusStore, logger);
    }

    private static HttpContext CreateHttpContext(Guid companyId)
    {
        var context = new DefaultHttpContext();
        context.Items[PortalObservability.SupportIdItemKey] = "4f892abc";
        context.Features.Set<ISessionFeature>(new SessionFeature
        {
            Session = new TestSession(new UserSession
            {
                UserId = "user-1",
                Email = "user@example.com",
                CompanyId = companyId,
                JeevesActiveCompany = 9900
            })
        });

        return context;
    }

    private sealed record ControllerFixtures(
        ExcelImportController Controller,
        string AggregateKey,
        RecordingTransientStatusStore TransientStore,
        CapturingLogger<ExcelImportController> Logger);

    private sealed class ThrowingExcelImportService : IExcelImportService
    {
        public bool IsSupportedImportType(string? importType) => throw new NotSupportedException();
        public bool IsEditSessionSupported(string? importType) => throw new NotSupportedException();
        public ExcelImportEditSessionInfo GetEditSessionInfo(string importType) => throw new NotSupportedException();
        public Task<BackgroundJobSnapshot> QueueUploadAsync(IFormFile file, ExcelImportUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> RunAsync(IFormFile file, ExcelImportRunRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportDirectAsync(string importType, IFormFile file, string importedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(ExcelImportEditSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportEditedRowsAsync(ExcelImportEditedRowsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingQueueExcelImportService : IExcelImportService
    {
        public bool IsSupportedImportType(string? importType) => true;
        public bool IsEditSessionSupported(string? importType) => false;
        public ExcelImportEditSessionInfo GetEditSessionInfo(string importType) => throw new NotSupportedException();
        public Task<BackgroundJobSnapshot> QueueUploadAsync(IFormFile file, ExcelImportUploadRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("authorization=secret-value");
        public Task<ExcelImportResult> RunAsync(IFormFile file, ExcelImportRunRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportDirectAsync(string importType, IFormFile file, string importedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(ExcelImportEditSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportEditedRowsAsync(ExcelImportEditedRowsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class MissingStorageExcelImportService : IExcelImportService
    {
        public bool IsSupportedImportType(string? importType) => true;
        public bool IsEditSessionSupported(string? importType) => false;
        public ExcelImportEditSessionInfo GetEditSessionInfo(string importType) => throw new NotSupportedException();
        public Task<BackgroundJobSnapshot> QueueUploadAsync(IFormFile file, ExcelImportUploadRequest request, CancellationToken cancellationToken = default)
            => throw new ExcelImportStorageConfigurationException("internal-storage-path");
        public Task<ExcelImportResult> RunAsync(IFormFile file, ExcelImportRunRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportDirectAsync(string importType, IFormFile file, string importedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(ExcelImportEditSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportEditedRowsAsync(ExcelImportEditedRowsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingExcelImportTableInitializationService : IExcelImportTableInitializationService
    {
        public Task<ExcelImportTableInitializationResult> EnsureImportTablesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ReportingExcelImportTableInitializationService : IExcelImportTableInitializationService
    {
        public Task<ExcelImportTableInitializationResult> EnsureImportTablesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ExcelImportTableInitializationResult
            {
                Success = false,
                Items = new List<ExcelImportTableInitializationItem>
                {
                    new()
                    {
                        TableName = "dbo.q_zu_StagingBudget",
                        Success = false,
                        Message = "authorization=secret-value"
                    }
                }
            });
    }

    private sealed class SuccessfulBudgetImportService : IExcelImportService
    {
        public bool IsSupportedImportType(string? importType) => true;
        public bool IsEditSessionSupported(string? importType) => true;
        public ExcelImportEditSessionInfo GetEditSessionInfo(string importType) => new()
        {
            ImportType = "budget",
            EditSessionFileName = "Budget (redigering)",
            MaxEditableRows = 1000
        };
        public Task<BackgroundJobSnapshot> QueueUploadAsync(IFormFile file, ExcelImportUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> RunAsync(IFormFile file, ExcelImportRunRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportDirectAsync(string importType, IFormFile file, string importedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(ExcelImportEditSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportEditedRowsAsync(ExcelImportEditedRowsRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ExcelImportResult
            {
                ImportType = "budget",
                ImportBatchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                TotalRows = 1,
                ValidRows = 1,
                InvalidRows = 0,
                RowHeaders = new List<string> { "KolumnA", "KolumnB" },
                RowResults = new List<ExcelImportRowResult>
                {
                    new()
                    {
                        RowNo = 1,
                        IsValid = true,
                        Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["KolumnA"] = "A1",
                            ["KolumnB"] = "B1"
                        }
                    }
                }
            });
    }

    private sealed class CapturingBudgetImportService : IExcelImportService
    {
        public string? LastRowsJson { get; private set; }

        public bool IsSupportedImportType(string? importType) => true;
        public bool IsEditSessionSupported(string? importType) => true;
        public ExcelImportEditSessionInfo GetEditSessionInfo(string importType) => new()
        {
            ImportType = "budget",
            EditSessionFileName = "Budget (redigering)",
            MaxEditableRows = 1000
        };
        public Task<BackgroundJobSnapshot> QueueUploadAsync(IFormFile file, ExcelImportUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> RunAsync(IFormFile file, ExcelImportRunRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportDirectAsync(string importType, IFormFile file, string importedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(ExcelImportEditSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportEditedRowsAsync(ExcelImportEditedRowsRequest request, CancellationToken cancellationToken = default)
        {
            LastRowsJson = request.RowsJson;
            return Task.FromResult(new ExcelImportResult
            {
                ImportType = "budget",
                ImportBatchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                TotalRows = 1,
                ValidRows = 1,
                InvalidRows = 0,
                RowHeaders = new List<string> { "KolumnA", "KolumnB" },
                RowResults = new List<ExcelImportRowResult>
                {
                    new()
                    {
                        RowNo = 1,
                        IsValid = true,
                        Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["KolumnA"] = "Ändrad",
                            ["KolumnB"] = "B1"
                        }
                    }
                }
            });
        }
    }

    private sealed class SensitiveInvalidRowsBudgetImportService : IExcelImportService
    {
        public bool IsSupportedImportType(string? importType) => true;
        public bool IsEditSessionSupported(string? importType) => true;
        public ExcelImportEditSessionInfo GetEditSessionInfo(string importType) => new()
        {
            ImportType = "budget",
            EditSessionFileName = "Budget (redigering)",
            MaxEditableRows = 1000
        };
        public Task<BackgroundJobSnapshot> QueueUploadAsync(IFormFile file, ExcelImportUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> RunAsync(IFormFile file, ExcelImportRunRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportDirectAsync(string importType, IFormFile file, string importedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(ExcelImportEditSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportEditedRowsAsync(ExcelImportEditedRowsRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidExcelImportRowsException("authorization=secret-value");
    }

    private sealed class QueuingExcelImportService : IExcelImportService
    {
        public List<IFormFile> QueuedFiles { get; } = new();
        public List<ExcelImportUploadRequest> QueuedRequests { get; } = new();

        public bool IsSupportedImportType(string? importType) => true;
        public bool IsEditSessionSupported(string? importType) => false;
        public ExcelImportEditSessionInfo GetEditSessionInfo(string importType) => throw new NotSupportedException();
        public Task<BackgroundJobSnapshot> QueueUploadAsync(IFormFile file, ExcelImportUploadRequest request, CancellationToken cancellationToken = default)
        {
            QueuedFiles.Add(file);
            QueuedRequests.Add(request);
            return Task.FromResult(new BackgroundJobSnapshot
            {
                Id = Guid.NewGuid(),
                CompanyId = request.CompanyId,
                JobType = ExcelImportBackgroundJobConstants.ExecuteJobType,
                Status = BackgroundJobStatus.Queued
            });
        }

        public Task<ExcelImportResult> RunAsync(IFormFile file, ExcelImportRunRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportDirectAsync(string importType, IFormFile file, string importedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(ExcelImportEditSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportEditedRowsAsync(ExcelImportEditedRowsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class AllowAllTenantGuard : ITenantGuard
    {
        public OperationResult<bool> Validate(UserSession? session, int? requestedCompanyCode = null)
            => OperationResult<bool>.Ok(true);
    }

    private sealed class AllowAllFeatureAccessService : IFeatureAccessService
    {
        public IReadOnlyList<FeatureAccessSelection> GetSelections(ISession session) => Array.Empty<FeatureAccessSelection>();
        public void SaveSelections(ISession session, IEnumerable<FeatureAccessSelection> selections) { }
        public bool IsEnabled(ISession session, int companyCode, FeatureFlag feature) => true;
    }

    private sealed class AllowAllCompanyPermissionGuard : ICompanyPermissionGuard
    {
        public Task<bool> HasAccessAsync(Guid companyId, Guid subModuleId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class RecordingTransientStatusStore : IExcelImportTransientStatusStore
    {
        private readonly IExcelImportTransientStatusStore _inner;
        public SidebarRuntimeEventRecord? LastRecord { get; private set; }
        public List<SidebarRuntimeEventRecord> Records { get; } = new();

        public RecordingTransientStatusStore(IExcelImportTransientStatusStore inner)
        {
            _inner = inner;
        }

        public void Record(SidebarRuntimeEventRecord record)
        {
            LastRecord = record;
            Records.Add(record);
            _inner.Record(record);
        }

        public IReadOnlyList<SidebarRuntimeStatusItemViewModel> ListRecent(Guid companyId, int take = 5)
            => _inner.ListRecent(companyId, take);

        public IReadOnlyList<SidebarRuntimeStatusItemViewModel> ListRecentSummaries(Guid companyId, int take = 5)
            => _inner.ListRecentSummaries(companyId, take);

        public void ClearCompany(Guid companyId)
            => _inner.ClearCompany(companyId);
    }

    private sealed class EmptyExcelImportRowResultStore : IExcelImportRowResultStore
    {
        public Task<bool> TableExistsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task BulkInsertAsync(IEnumerable<ExcelImportStoredRowResult> rows, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CleanupOldRowsAsync(int retentionDays = JeevesExcelImportRowResultStore.DefaultRetentionDays, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ExcelImportStoredRowPage> GetPageAsync(
            Guid companyId,
            string importType,
            Guid importBatchId,
            int page,
            int pageSize,
            bool showOnlyInvalidRows,
            bool showAllRows = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ExcelImportStoredRowPage());
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments]
            => new(name, arguments.Length == 0 ? name : $"{name}: {string.Join(", ", arguments)}");
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);

            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, properties));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception, IReadOnlyDictionary<string, object?> Properties);

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, object> LoadTempData(HttpContext context) => _values;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _values.Clear();
            foreach (var value in values)
            {
                _values[value.Key] = value.Value;
            }
        }
    }

    private sealed class SessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession(new UserSession());
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.OrdinalIgnoreCase);
        private readonly UserSession _userSession;

        public TestSession(UserSession userSession)
        {
            _userSession = userSession;
            this.Set("UserObject", _userSession);
        }

        public bool IsAvailable => true;
        public string Id => Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[]? value) => _values.TryGetValue(key, out value);
    }
}
