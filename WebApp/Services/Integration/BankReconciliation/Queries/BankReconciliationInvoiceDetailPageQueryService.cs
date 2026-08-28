using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Application;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Presentation;
using WebApp.ViewModels.Integration.BankReconciliation;
using WebApp.ViewModels.Shared;

namespace WebApp.Services.Integration.BankReconciliation.Queries;

// Builds the invoice-focused bank reconciliation page from the current source context.
public sealed class BankReconciliationInvoiceDetailPageQueryService : IBankReconciliationInvoiceDetailPageQueryService
{
    private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;
    private readonly IBankReconciliationInvoiceCandidateService _invoiceCandidateService;
    private readonly IBankReconciliationDemoSessionService _demoSessionService;
    private readonly IStringLocalizer<SharedResources> _sharedLocalizer;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BankReconciliationInvoiceDetailPageQueryService> _logger;

    public BankReconciliationInvoiceDetailPageQueryService(
        IJeevesRuntimeContextService jeevesRuntimeContextService,
        IBankReconciliationInvoiceCandidateService invoiceCandidateService,
        IBankReconciliationDemoSessionService demoSessionService,
        IStringLocalizer<SharedResources> sharedLocalizer,
        IHttpContextAccessor httpContextAccessor,
        ILogger<BankReconciliationInvoiceDetailPageQueryService> logger)
    {
        _jeevesRuntimeContextService = jeevesRuntimeContextService;
        _invoiceCandidateService = invoiceCandidateService;
        _demoSessionService = demoSessionService;
        _sharedLocalizer = sharedLocalizer;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BankReconciliationInvoicePageViewModel> BuildPageAsync(
        UserSession? user,
        BankReconciliationSourceContext source,
        CancellationToken cancellationToken)
    {
        var (invoices, runtimeBanner) = await BuildInvoicesAsync(user, source.IsDemoMode, cancellationToken);

        return new BankReconciliationInvoicePageViewModel
        {
            TransactionsJson = System.Text.Json.JsonSerializer.Serialize(source.Transactions, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }),
            InvoicesJson = System.Text.Json.JsonSerializer.Serialize(invoices, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }),
            RuntimeBanner = runtimeBanner,
            HasUploadedFile = source.HasSource,
            IsDemoMode = source.IsDemoMode,
            LatestFileName = source.SourceLabel ?? string.Empty,
            LatestUploadedAt = source.SourceUpdatedAt
        };
    }

    private async Task<(List<BankReconciliationInvoicePayload> Invoices, ModuleBannerViewModel? RuntimeBanner)> BuildInvoicesAsync(
        UserSession? user,
        bool isDemoMode,
        CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return (new List<BankReconciliationInvoicePayload>(), null);
        }

        if (isDemoMode)
        {
            var demoScenario = await _demoSessionService.LoadScenarioAsync(user.CompanyId ?? Guid.Empty, cancellationToken);
            return (demoScenario.Data.Invoices.Select(BankReconciliationInvoicePayloadMapper.MapDemoInvoice).ToList(), new ModuleBannerViewModel
            {
                Title = _sharedLocalizer["BankRec_DemoActiveTitle"].Value,
                Message = $"{_sharedLocalizer["BankRec_DemoActiveMessage"].Value} {demoScenario.Title}. {demoScenario.Description}",
                Tone = "info",
                IconClass = "fa fa-flask"
            });
        }

        var runtimeContext = await _jeevesRuntimeContextService.ResolveAsync(user, cancellationToken);
        if (!runtimeContext.Success || runtimeContext.Value is null)
        {
            return (new List<BankReconciliationInvoicePayload>(), BuildTenantDataUnavailableBanner(
                "Fakturor från Jeeves kunde inte laddas.",
                BankReconciliationErrorHandling.LogDiagnosticAndBuildUserMessage(
                    _logger,
                    _httpContextAccessor.HttpContext,
                    nameof(BankReconciliationInvoiceDetailPageQueryService),
                    "Bankavstämningen kan fortfarande använda den uppladdade camt.053-filen, men fakturadelen kräver tenantdata för valt bolag.",
                    runtimeContext.Error)));
        }

        try
        {
            var result = await _invoiceCandidateService.LoadAsync(
                false,
                user,
                cancellationToken,
                demoScenarioKey: _demoSessionService.ResolveScenarioKey(user.CompanyId ?? Guid.Empty));

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                return (new List<BankReconciliationInvoicePayload>(), new ModuleBannerViewModel
                {
                    Title = "Fakturor från Jeeves kunde inte laddas",
                    Message = "Bankavstämningen kan fortfarande använda den uppladdade camt.053-filen, men fakturalistan kunde inte hämtas från Jeeves just nu.",
                    Note = result.ErrorMessage,
                    Tone = "warning",
                    IconClass = "fa fa-triangle-exclamation"
                });
            }

            var invoices = result.Invoices.Select(BankReconciliationInvoicePayloadMapper.MapInvoice).ToList();
            return (invoices, null);
        }
        catch (Exception ex)
        {
            return (new List<BankReconciliationInvoicePayload>(), new ModuleBannerViewModel
            {
                Title = "Fakturor från Jeeves kunde inte laddas",
                Message = "Bankavstämningen kan fortfarande använda den uppladdade camt.053-filen, men fakturalistan kunde inte hämtas från Jeeves just nu.",
                Note = BankReconciliationErrorHandling.LogAndBuildUserMessage(
                    _logger,
                    _httpContextAccessor.HttpContext,
                    nameof(BankReconciliationInvoiceDetailPageQueryService),
                    "Bankavstämningens fakturalista kunde inte laddas just nu.",
                    ex),
                Tone = "warning",
                IconClass = "fa fa-triangle-exclamation"
            });
        }
    }

    private static ModuleBannerViewModel BuildTenantDataUnavailableBanner(string message, string note)
        => new()
        {
            Title = "Tenantdata från Jeeves är tillfälligt otillgänglig",
            Message = message,
            Note = note,
            Tone = "warning",
            IconClass = "fa fa-plug-circle-xmark"
        };
}
