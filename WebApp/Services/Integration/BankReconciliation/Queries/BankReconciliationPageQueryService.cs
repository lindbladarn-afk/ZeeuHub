using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration.BankReconciliation.CodingRules;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.UploadFlow;
using WebApp.Services.Integration.BankReconciliation.Validation;
using WebApp.Services.Integration.BankReconciliation.Workspace;
using WebApp.ViewModels.Integration.BankReconciliation;
using WebApp.ViewModels.Shared;

namespace WebApp.Services.Integration.BankReconciliation.Queries;

// Builds the bank reconciliation start page by composing workspace, demo and coding-rule state.
public sealed class BankReconciliationPageQueryService : IBankReconciliationPageQueryService
{
    private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;
    private readonly IBankReconciliationWorkspaceService _workspaceService;
    private readonly IBankReconciliationUploadFlowService _uploadFlowService;
    private readonly IBankReconciliationDemoSessionService _demoSessionService;
    private readonly IStringLocalizer<SharedResources> _sharedLocalizer;
    private readonly IBankReconciliationCamtValidationService _validationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BankReconciliationPageQueryService> _logger;

    public BankReconciliationPageQueryService(
        IJeevesRuntimeContextService jeevesRuntimeContextService,
        IBankReconciliationWorkspaceService workspaceService,
        IBankReconciliationUploadFlowService uploadFlowService,
        IBankReconciliationDemoSessionService demoSessionService,
        IStringLocalizer<SharedResources> sharedLocalizer,
        IHttpContextAccessor httpContextAccessor,
        ILogger<BankReconciliationPageQueryService> logger,
        IBankReconciliationCamtValidationService? validationService = null)
    {
        _jeevesRuntimeContextService = jeevesRuntimeContextService;
        _workspaceService = workspaceService;
        _uploadFlowService = uploadFlowService;
        _demoSessionService = demoSessionService;
        _sharedLocalizer = sharedLocalizer;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _validationService = validationService ?? new BankReconciliationCamtValidationService();
    }

    public async Task<BankReconciliationPageViewModel> BuildPageAsync(
        UserSession? user,
        string? uploadError,
        string? uploadInfo,
        string? statusMessage,
        string? statusTone,
        CancellationToken cancellationToken)
    {
        var latestCamtFile = _uploadFlowService.ResolveLatestCamtFile();
        var source = await ResolveSourceAsync(user, latestCamtFile, cancellationToken);
        var runtimeBanner = await BuildRuntimeBannerAsync(user, source.IsDemoMode, cancellationToken);
        var codingRuleSet = await LoadCodingRuleSetAsync(user, source, cancellationToken);

        return new BankReconciliationPageViewModel
        {
            TransactionsJson = "[]",
            InvoicesJson = "[]",
            RuntimeBanner = runtimeBanner,
            IsDemoMode = source.IsDemoMode,
            DemoScenarioKey = source.DemoScenarioKey ?? "overview",
            DemoScenarios = _demoSessionService.ListScenarios(),
            BankAccountKey = source.BankAccountKey ?? "default",
            BankAccountLabel = source.BankAccountLabel ?? "Okänt bankkonto",
            ActiveCompanyName = user?.CompanyName ?? string.Empty,
            CodingRulesJson = System.Text.Json.JsonSerializer.Serialize(codingRuleSet.Rows),
            CodingRulesVersion = codingRuleSet.Version,
            UploadError = uploadError ?? source.ErrorMessage,
            UploadInfo = uploadInfo,
            StatusMessage = statusMessage,
            StatusTone = statusTone ?? "info",
            LatestFileName = _uploadFlowService.ResolveLatestCamtDisplayName() ?? source.SourceLabel,
            LatestUploadedAt = source.SourceUpdatedAt,
            HasUploadedFile = source.HasSource,
            ValidationReport = BuildValidationReport(latestCamtFile)
        };
    }

    private async Task<BankReconciliationSourceContext> ResolveSourceAsync(
        UserSession? user,
        string? latestCamtFile,
        CancellationToken cancellationToken)
    {
        var companyId = user?.CompanyId ?? Guid.Empty;
        return await _workspaceService.ResolveSourceAsync(
            user,
            latestCamtFile,
            companyId != Guid.Empty && _demoSessionService.IsDemoMode(companyId),
            _demoSessionService.ResolveScenarioKey(companyId),
            cancellationToken);
    }

    private BankReconciliationCamtValidationResult? BuildValidationReport(string? latestCamtFile)
    {
        if (string.IsNullOrWhiteSpace(latestCamtFile) || !File.Exists(latestCamtFile))
            return null;

        return _validationService.Validate(latestCamtFile);
    }

    private async Task<ModuleBannerViewModel?> BuildRuntimeBannerAsync(
        UserSession? user,
        bool isDemoMode,
        CancellationToken cancellationToken)
    {
        if (isDemoMode)
        {
            return new ModuleBannerViewModel
            {
                Title = _sharedLocalizer["BankRec_DemoActiveTitle"].Value,
                Message = _sharedLocalizer["BankRec_DemoActiveMessage"].Value,
                Tone = "info",
                IconClass = "fa fa-flask"
            };
        }

        if (user is null)
        {
            return null;
        }

        var runtimeContext = await _jeevesRuntimeContextService.ResolveAsync(user, cancellationToken);
        if (runtimeContext.Success && runtimeContext.Value is not null)
        {
            return null;
        }

        return BuildTenantDataUnavailableBanner(
            "Fakturor från Jeeves kunde inte laddas.",
            BankReconciliationErrorHandling.LogDiagnosticAndBuildUserMessage(
                _logger,
                _httpContextAccessor.HttpContext,
                nameof(BankReconciliationPageQueryService),
                "Bankavstämningen kan fortfarande använda den uppladdade camt.053-filen, men fakturadelen kräver tenantdata för valt bolag.",
                runtimeContext.Error));
    }

    private async Task<BankReconciliationCodingRuleSet> LoadCodingRuleSetAsync(
        UserSession? user,
        BankReconciliationSourceContext source,
        CancellationToken cancellationToken)
    {
        if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
        {
            return new BankReconciliationCodingRuleSet();
        }

        return await _workspaceService.LoadCodingRulesAsync(user, source, cancellationToken);
    }

    private static ModuleBannerViewModel BuildTenantDataUnavailableBanner(string message, string note)
    {
        return new ModuleBannerViewModel
        {
            Title = "Tenantdata från Jeeves är tillfälligt otillgänglig",
            Message = message,
            Note = note,
            Tone = "warning",
            IconClass = "fa fa-plug-circle-xmark"
        };
    }
}
