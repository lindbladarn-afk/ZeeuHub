// Verifies document-signing, customer-sync and Flow Engine behavior at the integration-controller boundary.
using Entities.Application;
using Entities.Contracts;
using LoggerService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using NotificationService;
using Repository.Contracts;
using WebApp.Controllers;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Models;
using WebApp.Models.Application;
using WebApp.Models.DocumentSigning;
using WebApp.Models.BackgroundJobs;
using WebApp.Models.Integration;
using WebApp.Models.Integration.CustomerSync;
using WebApp.Models.Orders;
using WebApp.Models.Identity;
using WebApp.Services.Application;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.DocumentSigning;
using WebApp.Services.Integration.Akeneo;
using WebApp.Services.Integration.CustomerSync.Application;
using WebApp.Services.Integration.CustomerSync.Persistence;
using WebApp.Services.Integration.CustomerSync.Presentation;
using WebApp.Services.Integration.FlowEngine;
using WebApp.Services.Orders;
using WebApp.Services;
using WebApp.ViewModels.DocumentSigning;
using WebApp.ViewModels.Integration.CustomerSync;
using WebApp.ViewModels.Shared;

namespace WebApp.Tests;

public sealed class IntegrationControllerBehaviorTests
{
    [Fact]
    public async Task DocumentSigningLaunch_Returns_Safe_BadRequest_On_Failure()
    {
        var fixtures = CreateController(new ThrowingDocumentSigningService());

        var result = await fixtures.Controller.DocumentSigningLaunch(Guid.NewGuid(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("Referens:", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization=secret-value", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomerSync_Returns_The_Module_View_When_Authorized()
    {
        var fixtures = CreateController(new ThrowingDocumentSigningService());

        var result = await fixtures.Controller.CustomerSync();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Integration/CustomerSync/CustomerSync.cshtml", viewResult.ViewName);
    }

    [Fact]
    public async Task CustomerSync_Returns_Forbid_When_Company_Lacks_Permission()
    {
        var fixtures = CreateController(new ThrowingDocumentSigningService(), new DenyAllCompanyPermissionGuard());

        var result = await fixtures.Controller.CustomerSync();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CustomerSyncImportHubSpotCompanies_Imports_And_Redirects()
    {
        var importService = new NoopCustomerSyncHubSpotImportService
        {
            Result = new CustomerSyncHubSpotImportResult
            {
                ImportedCount = 2,
                Summary = "2 företag hämtades från HubSpot och visas nu i huben."
            }
        };
        var fixtures = CreateController(
            new ThrowingDocumentSigningService(),
            importService: importService);

        var result = await fixtures.Controller.CustomerSyncImportHubSpotCompanies(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(IntegrationController.CustomerSync), redirect.ActionName);
        Assert.Equal(1, importService.ImportCallCount);
        Assert.Equal("info", fixtures.Controller.TempData["CustomerSyncStatusTone"]);
    }

    [Fact]
    public async Task CustomerSyncSetEnabled_Persists_Runtime_Enabled_Flag()
    {
        var runtimeService = new NoopCustomerSyncRuntimeConfigurationService
        {
            CurrentConfiguration = new CustomerSyncRuntimeConfiguration
            {
                Enabled = true,
                PollIntervalMinutes = 60
            }
        };

        var fixtures = CreateController(
            new ThrowingDocumentSigningService(),
            runtimeService: runtimeService);

        var result = await fixtures.Controller.CustomerSyncSetEnabled(false, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(IntegrationController.CustomerSync), redirect.ActionName);
        Assert.NotNull(runtimeService.LastSavedConfiguration);
        Assert.False(runtimeService.LastSavedConfiguration!.Enabled);
        Assert.Equal("info", fixtures.Controller.TempData["CustomerSyncStatusTone"]);
    }

    [Fact]
    public async Task CustomerSync_Uses_Runtime_Configuration_For_Enabled_State()
    {
        var fixtures = CreateController(
            new ThrowingDocumentSigningService(),
            runtimeService: new NoopCustomerSyncRuntimeConfigurationService
            {
                CurrentConfiguration = new CustomerSyncRuntimeConfiguration
                {
                    Enabled = false,
                    PollIntervalMinutes = 60
                }
            });

        var result = await fixtures.Controller.CustomerSync();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CustomerSyncPageViewModel>(viewResult.Model);
        Assert.False(model.IsEnabled);
    }

    [Fact]
    public async Task FlowEngineRunCheckOrders_ShowsSuccessAndRedirectsToCentra()
    {
        var executionService = new RecordingFlowEngineExecutionService();
        var fixtures = CreateController(
            new ThrowingDocumentSigningService(),
            flowEngineExecutionService: executionService);
        SetAction(fixtures.Controller, nameof(IntegrationController.FlowEngineRunCheckOrders));

        var result = await fixtures.Controller.FlowEngineRunCheckOrders(
            new FlowEngineRunCheckOrdersInput { DateUtc = " 2026-07-29 ", Limit = 15 },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(IntegrationController.FlowEngineCentra), redirect.ActionName);
        Assert.NotNull(executionService.LastRequest);
        Assert.Equal("2026-07-29", executionService.LastRequest!.Params.DateUtc);
        Assert.Equal(15, executionService.LastRequest.Params.Limit);

        var alert = Assert.Single(ScopedAlertTempDataHelper.Read(fixtures.Controller.TempData, peek: true));
        Assert.Equal(Alert.SUCCESS, alert.Level);
        Assert.Equal("FlowEngine-korning klar: Centra check orders sparad i jobbhistoriken.", alert.Message);
    }

    [Fact]
    public async Task FlowEngineRunCheckOrders_ShowsDetailedIntegrationError()
    {
        const string detailedError = "Centra API rejected order 42: invalid warehouse.";
        var executionService = new RecordingFlowEngineExecutionService
        {
            Error = new InvalidOperationException(detailedError)
        };
        var fixtures = CreateController(
            new ThrowingDocumentSigningService(),
            flowEngineExecutionService: executionService);
        SetAction(fixtures.Controller, nameof(IntegrationController.FlowEngineRunCheckOrders));

        var result = await fixtures.Controller.FlowEngineRunCheckOrders(
            new FlowEngineRunCheckOrdersInput { DateUtc = "2026-07-29" },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(IntegrationController.FlowEngineCentra), redirect.ActionName);

        var alert = Assert.Single(ScopedAlertTempDataHelper.Read(fixtures.Controller.TempData, peek: true));
        Assert.Equal(Alert.DANGER, alert.Level);
        Assert.Equal(detailedError, alert.Message);
    }

    [Fact]
    public async Task FlowEngineRunCheckOrders_ReturnsForbidWithoutCompanyPermission()
    {
        var executionService = new RecordingFlowEngineExecutionService();
        var fixtures = CreateController(
            new ThrowingDocumentSigningService(),
            companyPermissionGuard: new DenyAllCompanyPermissionGuard(),
            flowEngineExecutionService: executionService);
        SetAction(fixtures.Controller, nameof(IntegrationController.FlowEngineRunCheckOrders));

        var result = await fixtures.Controller.FlowEngineRunCheckOrders(
            new FlowEngineRunCheckOrdersInput { DateUtc = "2026-07-29" },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(executionService.LastRequest);
    }

    [Fact]
    public async Task FlowEngineRunCentraFetchOrder_RejectsMissingOrderIdBeforeExecution()
    {
        var executionService = new RecordingFlowEngineExecutionService();
        var fixtures = CreateController(
            new ThrowingDocumentSigningService(),
            flowEngineExecutionService: executionService);
        SetAction(fixtures.Controller, nameof(IntegrationController.FlowEngineRunCentraFetchOrder));

        var result = await fixtures.Controller.FlowEngineRunCentraFetchOrder(
            new FlowEngineRunCentraFetchOrderInput { OrderId = "   " },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(IntegrationController.FlowEngineCentra), redirect.ActionName);
        Assert.Null(executionService.LastRequest);

        var alert = Assert.Single(ScopedAlertTempDataHelper.Read(fixtures.Controller.TempData, peek: true));
        Assert.Equal(Alert.DANGER, alert.Level);
        Assert.Equal("Integration_OrderIdRequiredForCentraFetchOrder", alert.Message);
    }

    private static ControllerFixtures CreateController(
        IDocumentSigningService documentSigningService,
        ICompanyPermissionGuard? companyPermissionGuard = null,
        ICustomerSyncHubSpotImportService? importService = null,
        NoopCustomerSyncRuntimeConfigurationService? runtimeService = null,
        IFlowEngineExecutionService? flowEngineExecutionService = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        httpContext.Session.Set("UserObject", new UserSession
        {
            UserId = "user-1",
            Email = "user@example.com",
            CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            JeevesActiveCompany = 1000,
            CompanyName = "Acme"
        });

        var controller = new IntegrationController(
            companyPermissionGuard ?? new AllowAllCompanyPermissionGuard(),
            new HttpContextAccessor { HttpContext = httpContext },
            new NoopAkeneoExportService(),
            new NoopJeevesRuntimeContextService(),
            new NoopOrdersService(),
            documentSigningService,
            flowEngineExecutionService ?? new NoopFlowEngineExecutionService(),
            new NoopFlowEngineRequestNormalizer(),
            new FlowEngineCentraCommandFactory(TimeProvider.System),
            new NoopFlowEngineImportOrderWorkflowService(),
            new NoopFlowEngineOrderDocumentExtractionService(),
            new NoopFlowEngineModuleService(),
            new NoopFlowEngineHealthProbeService(),
            new CustomerSyncPagePresenter(new CustomerSyncConfigurationPresenter(), new NoopBackgroundJobStore(), new NoopCustomerSyncMappingRepository()),
            runtimeService ?? new NoopCustomerSyncRuntimeConfigurationService(),
            importService ?? new NoopCustomerSyncHubSpotImportService(),
            new NoopSidebarRuntimeStatusService(),
            new DummyStringLocalizer());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = new TempDataDictionary(httpContext, new NoopTempDataProvider());

        return new ControllerFixtures(controller);
    }

    private static void SetAction(IntegrationController controller, string actionName)
    {
        controller.ControllerContext.ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor
        {
            ControllerName = "Integration",
            ActionName = actionName
        };
    }

    private sealed record ControllerFixtures(IntegrationController Controller);

    private sealed class ThrowingDocumentSigningService : IDocumentSigningService
    {
        public bool IsEnabledForCompany(Guid companyId) => true;
        public bool CanPingForCompany(Guid companyId) => true;
        public Task<IReadOnlyList<DocumentSigningListItem>> ListForOrderAsync(Guid companyId, int? jeevesCompanyCode, long orderNo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentSigningListItem>> ListRecentAsync(Guid companyId, int? jeevesCompanyCode, int take = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DocumentSigningCreateResult> CreateAndStartAsync(DocumentSigningCreateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DocumentSigningListItem?> SyncAsync(Guid companyId, Guid signingId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DocumentSigningLaunchResult?> LaunchAsync(Guid companyId, Guid signingId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("authorization=secret-value");
        public Task PingAsync(Guid companyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentSigningOneflowWorkspaceViewModel>> ListWorkspacesAsync(Guid companyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentSigningOneflowTemplateViewModel>> ListTemplatesAsync(Guid companyId, int? workspaceId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DocumentSigningPublicResultViewModel?> GetPublicResultAsync(Guid publicToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class AllowAllCompanyPermissionGuard : ICompanyPermissionGuard
    {
        public Task<bool> HasAccessAsync(
            Guid companyId,
            Guid subModuleId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class DenyAllCompanyPermissionGuard : ICompanyPermissionGuard
    {
        public Task<bool> HasAccessAsync(
            Guid companyId,
            Guid subModuleId,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class NoopAkeneoExportService : IAkeneoExportService
    {
        public Task<AkeneoExportResult> ExportProductsXmlAsync(int limit, string? fileName, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AkeneoExportResult> ExportProductsXmlAsync(IReadOnlyList<string> skus, int limit, string? fileName, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoopJeevesRuntimeContextService : IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Ok(new JeevesRuntimeContext
            {
                UserId = sessionUser?.UserId ?? "user-1",
                CompanyId = sessionUser?.CompanyId ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                CompanyCode = sessionUser?.JeevesActiveCompany ?? 1000,
                ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
                CompanyName = sessionUser?.CompanyName ?? "Acme",
                Email = sessionUser?.Email,
                PersSign = sessionUser?.PersSign
            }));
    }

    private sealed class NoopOrdersService : IOrdersService
    {
        public Task<OrdersListViewModel> GetOrdersAsync(string connectionString, GetOrdersQuery query) => throw new NotSupportedException();
        public Task<OrderDetailsViewModel?> GetOrderDetailsAsync(string connectionString, GetOrderDetailsQuery query) => throw new NotSupportedException();
        public Task<OrderDeliveryForecastViewModel> GetDeliveryForecastAsync(string connectionString, GetDeliveryForecastQuery query) => throw new NotSupportedException();
    }

    private sealed class NoopFlowEngineExecutionService : IFlowEngineExecutionService
    {
        public Task<FlowEngineJobSnapshot> ExecuteAsync(UserSession sessionUser, FlowEngineExecuteJobRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public FlowEngineJobSnapshot? Get(Guid companyId, Guid jobId) => null;
        public IReadOnlyList<FlowEngineJobSnapshot> ListRecent(Guid companyId, int take = 10) => Array.Empty<FlowEngineJobSnapshot>();
        public FlowEngineHistoryPageResult ListPage(Guid companyId, int page = 1, int pageSize = 15, string? systemKey = null, FlowEngineHistoryFilterState? filters = null) => throw new NotSupportedException();
    }

    private sealed class RecordingFlowEngineExecutionService : IFlowEngineExecutionService
    {
        public Exception? Error { get; init; }
        public FlowEngineExecuteJobRequest? LastRequest { get; private set; }

        public Task<FlowEngineJobSnapshot> ExecuteAsync(
            UserSession sessionUser,
            FlowEngineExecuteJobRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            return Error is null
                ? Task.FromResult(new FlowEngineJobSnapshot { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") })
                : Task.FromException<FlowEngineJobSnapshot>(Error);
        }

        public FlowEngineJobSnapshot? Get(Guid companyId, Guid jobId) => null;
        public IReadOnlyList<FlowEngineJobSnapshot> ListRecent(Guid companyId, int take = 10) => Array.Empty<FlowEngineJobSnapshot>();
        public FlowEngineHistoryPageResult ListPage(Guid companyId, int page = 1, int pageSize = 15, string? systemKey = null, FlowEngineHistoryFilterState? filters = null) => throw new NotSupportedException();
    }

    private sealed class NoopFlowEngineRequestNormalizer : IFlowEngineRequestNormalizer
    {
        public FlowEngineExecuteJobRequest Normalize(FlowEngineExecuteJobRequest request, IFormCollection? form) => request;
    }

    private sealed class NoopFlowEngineImportOrderWorkflowService : IFlowEngineImportOrderWorkflowService
    {
        public FlowEngineImportOrderSessionState? LoadState() => null;
        public void SaveState(FlowEngineImportOrderSessionState state) { }
        public FlowEngineRunImportOrderInput NormalizeInput(FlowEngineRunImportOrderInput input) => input;
        public FlowEngineImportOrderSessionState BuildState(FlowEngineRunImportOrderInput form, FlowEngineImportOrderSessionState? currentState, IReadOnlyCollection<FlowEngineDeliveryAddressOption>? deliveryAddressOptions = null, FlowEngineImportAddressLookupContext? addressLookupContext = null, FlowEngineImportDocumentReview? documentReview = null, IReadOnlyCollection<FlowEngineJeevesArtStatusRow>? artStatusRows = null) => new();
        public string? ResolveDeliveryPlaceCode(int companyCode, string customerNumber, string? selectedCode) => null;
        public List<FlowEngineDeliveryAddressOption> ParseDeliveryAddressOptionsFromJob(FlowEngineJobSnapshot job) => new();
        public List<FlowEngineJeevesArtStatusRow> ParseArtStatusRowsFromJob(FlowEngineJobSnapshot job) => new();
        public FlowEngineImportDocumentReview BuildDocumentReview(string fileName, FlowEngineOrderDocumentExtractionResult extractionResult) => new();
        public FlowEngineImportDocumentReview BuildDocumentErrorReview(string? fileName, string errorMessage) => new();
        public string MergeDocumentLines(string currentLines, IReadOnlyCollection<FlowEngineImportDocumentReviewLine> extractedLines) => currentLines;
        public List<FlowEngineJeevesImportLineInput> ParseImportOrderLines(string rawLines) => new();
    }

    private sealed class NoopFlowEngineOrderDocumentExtractionService : IFlowEngineOrderDocumentExtractionService
    {
        public Task<FlowEngineOrderDocumentExtractionResult> ExtractAsync(FlowEngineOrderDocumentInput document, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoopFlowEngineModuleService : IFlowEngineModuleService
    {
        public Task<FlowEngineModuleViewModel> BuildModuleViewModelAsync(UserSession? sessionUser, string? activeSection, Guid? selectedJobId, int historyPage, FlowEngineHistoryFilterState? historyFilters, FlowEngineWorkbenchSettingsState? workbenchSettings, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NoopFlowEngineHealthProbeService : IFlowEngineHealthProbeService
    {
        public Task<IReadOnlyList<FlowEngineSystemStatusViewModel>> ProbeAsync(UserSession? sessionUser, string activeSection, JeevesRuntimeContext? runtimeContext, bool testMode, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NoopBackgroundJobStore : IBackgroundJobStore
    {
        public BackgroundJobSnapshot Enqueue(BackgroundJobEnqueueRequest request, DateTime utcNow) => throw new NotSupportedException();
        public BackgroundJobSnapshot? TryClaimNext(string workerId, DateTime utcNow, TimeSpan leaseDuration, Guid? companyId = null, IReadOnlyCollection<string>? allowedJobTypes = null) => throw new NotSupportedException();
        public IReadOnlyList<Guid> ListQueuedCompanyIds(DateTime utcNow, int take, IReadOnlyCollection<string>? allowedJobTypes = null) => Array.Empty<Guid>();
        public BackgroundJobSnapshot? FindActive(Guid companyId, string jobType, string correlationKey, Guid? excludeJobId = null) => null;
        public BackgroundJobSnapshot? Get(Guid companyId, Guid jobId) => throw new NotSupportedException();
        public IReadOnlyList<BackgroundJobSnapshot> ListRecent(Guid companyId, int take) => Array.Empty<BackgroundJobSnapshot>();
        public IReadOnlyList<BackgroundJobSnapshot> ListActive(Guid companyId, int take) => Array.Empty<BackgroundJobSnapshot>();
        public BackgroundJobSnapshot Heartbeat(Guid companyId, Guid jobId, string workerId, DateTime utcNow, TimeSpan leaseDuration) => throw new NotSupportedException();
        public BackgroundJobSnapshot Complete(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? resultJson = null) => throw new NotSupportedException();
        public BackgroundJobSnapshot Fail(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? errorCode, string? errorMessage, TimeSpan? retryDelay = null, string? resultJson = null) => throw new NotSupportedException();
        public BackgroundJobSnapshot Cancel(Guid companyId, Guid jobId, DateTime utcNow, string? errorMessage = null) => throw new NotSupportedException();
        public int RequeueExpiredLeases(DateTime utcNow, TimeSpan retryDelay) => throw new NotSupportedException();
    }

    private sealed class NoopCustomerSyncRuntimeConfigurationService : ICustomerSyncRuntimeConfigurationService
    {
        public int QueuedCount { get; init; }
        public int QueueManualRunsCallCount { get; private set; }
        public CustomerSyncRuntimeConfiguration? LastSavedConfiguration { get; private set; }
        public CustomerSyncRuntimeConfiguration CurrentConfiguration { get; init; } = new CustomerSyncRuntimeConfiguration();

        public Task<CustomerSyncOptions> GetEffectiveOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CustomerSyncOptions());

        public Task<CustomerSyncRuntimeConfiguration> GetRuntimeConfigurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentConfiguration);

        public Task SaveRuntimeConfigurationAsync(CustomerSyncRuntimeConfiguration configuration, CancellationToken cancellationToken = default)
        {
            LastSavedConfiguration = configuration;
            return Task.CompletedTask;
        }

        public Task<int> QueueManualRunsAsync(CancellationToken cancellationToken = default)
        {
            QueueManualRunsCallCount++;
            return Task.FromResult(QueuedCount);
        }
    }

    private sealed class NoopCustomerSyncHubSpotImportService : ICustomerSyncHubSpotImportService
    {
        public CustomerSyncHubSpotImportResult Result { get; init; } = new();
        public int ImportCallCount { get; private set; }

        public Task<CustomerSyncHubSpotImportResult> ImportCompaniesAsync(CancellationToken cancellationToken = default)
        {
            ImportCallCount++;
            return Task.FromResult(Result);
        }
    }

    private sealed class NoopCustomerSyncMappingRepository : ICustomerSyncMappingRepository
    {
        public Task<CustomerSyncMappingRecord?> FindByJeevesCustomerAsync(Guid companyId, int jeevesCompanyCode, string jeevesCustomerNumber, CancellationToken cancellationToken)
            => Task.FromResult<CustomerSyncMappingRecord?>(null);

        public Task<CustomerSyncMappingRecord?> FindByHubSpotCompanyAsync(Guid companyId, string hubSpotCompanyId, CancellationToken cancellationToken)
            => Task.FromResult<CustomerSyncMappingRecord?>(null);

        public Task<IReadOnlyList<CustomerSyncMappingRecord>> FindByOrganizationNumberAsync(Guid companyId, string organizationNumber, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CustomerSyncMappingRecord>>(Array.Empty<CustomerSyncMappingRecord>());

        public Task<int> CountHubSpotMappingsAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<IReadOnlyList<CustomerSyncMappingRecord>> ListHubSpotMappingsAsync(IReadOnlyCollection<Guid> companyIds, int skip, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CustomerSyncMappingRecord>>(Array.Empty<CustomerSyncMappingRecord>());

        public Task<CustomerSyncMappingRecord> UpsertAsync(CustomerSyncMappingRecord mapping, DateTime utcNow, CancellationToken cancellationToken)
            => Task.FromResult(mapping);
    }

    private sealed class NoopTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class NoopSidebarRuntimeStatusService : ISidebarRuntimeStatusService
    {
        public SidebarRuntimeStatusViewModel GetStatus(UserSession? sessionUser) => new();
        public Task<SidebarRuntimeStatusViewModel> GetStatusAsync(UserSession? sessionUser, CancellationToken cancellationToken = default) => Task.FromResult(new SidebarRuntimeStatusViewModel());
        public void RecordEvent(UserSession sessionUser, SidebarRuntimeEventRecord record) { }
        public void RecordEvent(Guid companyId, SidebarRuntimeEventRecord record) { }
        public void MarkAllRead(UserSession sessionUser) { }
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, $"{name}: {string.Join(", ", arguments)}");
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);
        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value.ToArray();
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
