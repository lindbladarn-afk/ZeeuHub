using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Presentation;
using WebApp.ViewModels.Shared;

namespace WebApp.Services.Integration.BankReconciliation.Queries;

// Resolves customer, supplier and demo invoice pages for bank reconciliation.
public sealed class BankReconciliationInvoicePageQueryService : IBankReconciliationInvoicePageQueryService
{
    private readonly IBankReconciliationInvoiceCandidateService _invoiceCandidateService;
    private readonly IBankReconciliationDemoSessionService _demoSessionService;
    private readonly IStringLocalizer<SharedResources> _sharedLocalizer;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BankReconciliationInvoicePageQueryService> _logger;

    public BankReconciliationInvoicePageQueryService(
        IBankReconciliationInvoiceCandidateService invoiceCandidateService,
        IBankReconciliationDemoSessionService demoSessionService,
        IStringLocalizer<SharedResources> sharedLocalizer,
        IHttpContextAccessor httpContextAccessor,
        ILogger<BankReconciliationInvoicePageQueryService> logger)
    {
        _invoiceCandidateService = invoiceCandidateService;
        _demoSessionService = demoSessionService;
        _sharedLocalizer = sharedLocalizer;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BankReconciliationInvoicePageQueryResult> BuildPageAsync(
        UserSession? user,
        int page,
        int pageSize,
        string? classificationFilter,
        string? groupFilter,
        CancellationToken cancellationToken)
    {
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var safePage = Math.Max(page, 1);
        if (user is null)
        {
            return Empty(safePage, safePageSize, "User session is missing.");
        }

        if (_demoSessionService.IsDemoMode(user.CompanyId ?? Guid.Empty))
        {
            return await BuildDemoPageAsync(user, safePage, safePageSize, classificationFilter, groupFilter, cancellationToken);
        }

        return await BuildLivePageAsync(user, safePage, safePageSize, classificationFilter, groupFilter, cancellationToken);
    }

    private async Task<BankReconciliationInvoicePageQueryResult> BuildDemoPageAsync(
        UserSession user,
        int page,
        int pageSize,
        string? classificationFilter,
        string? groupFilter,
        CancellationToken cancellationToken)
    {
        var candidateSource = BankReconciliationInvoiceCandidateService.ResolveInvoiceCandidateSource(null, classificationFilter, groupFilter);
        var result = candidateSource is BankReconciliationInvoiceCandidateSource.Supplier or BankReconciliationInvoiceCandidateSource.None
            ? new BankReconciliationInvoiceCandidateResult()
            : await _invoiceCandidateService.LoadAsync(
                true,
                user,
                cancellationToken,
                page: page,
                pageSize: pageSize,
                demoScenarioKey: _demoSessionService.ResolveScenarioKey(user.CompanyId ?? Guid.Empty));

        return FromCandidateResult(
            result,
            page,
            pageSize,
            _sharedLocalizer["BankRec_DemoActiveMessage"].Value);
    }

    private async Task<BankReconciliationInvoicePageQueryResult> BuildLivePageAsync(
        UserSession user,
        int page,
        int pageSize,
        string? classificationFilter,
        string? groupFilter,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidateSource = BankReconciliationInvoiceCandidateService.ResolveInvoiceCandidateSource(null, classificationFilter, groupFilter);
            var result = candidateSource switch
            {
                BankReconciliationInvoiceCandidateSource.Customer => await _invoiceCandidateService.LoadCustomerPageAsync(
                    user,
                    page,
                    pageSize,
                    cancellationToken),
                BankReconciliationInvoiceCandidateSource.Supplier => await _invoiceCandidateService.LoadSupplierPageAsync(
                    user,
                    page,
                    pageSize,
                    cancellationToken),
                BankReconciliationInvoiceCandidateSource.All => await _invoiceCandidateService.LoadCombinedPageAsync(
                    user,
                    page,
                    pageSize,
                    cancellationToken),
                _ => new BankReconciliationInvoiceCandidateResult()
            };

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                return Empty(page, pageSize, result.ErrorMessage);
            }

            return FromCandidateResult(
                result,
                page,
                pageSize,
                "Bankavstämningen visar kundfakturor för inbetalningar och leverantörsfakturor för utbetalningar.");
        }
        catch (Exception ex)
        {
            return Empty(
                page,
                pageSize,
                BankReconciliationErrorHandling.LogAndBuildUserMessage(
                    _logger,
                    _httpContextAccessor.HttpContext,
                    nameof(BankReconciliationInvoicePageQueryService),
                    "Bankavstämningens fakturor kunde inte laddas just nu.",
                    ex));
        }
    }

    private static BankReconciliationInvoicePageQueryResult FromCandidateResult(
        BankReconciliationInvoiceCandidateResult result,
        int page,
        int pageSize,
        string dataSourceNotice)
    {
        return new BankReconciliationInvoicePageQueryResult
        {
            Items = result.Invoices.Select(BankReconciliationInvoicePayloadMapper.MapInvoice).ToList(),
            ActiveTab = "unpaid",
            UsesHistoricalFactSource = false,
            DataSourceNotice = dataSourceNotice,
            Page = page,
            PageSize = pageSize,
            TotalCount = result.TotalCount,
            TotalPages = CalculateTotalPages(result.TotalCount, pageSize)
        };
    }

    private static BankReconciliationInvoicePageQueryResult Empty(int page, int pageSize, string? errorMessage)
        => new()
        {
            Page = page,
            PageSize = pageSize,
            ErrorMessage = errorMessage
        };

    private static int CalculateTotalPages(int totalCount, int pageSize)
        => totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
}
