using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Application;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Invoices;
using WebApp.Services.Integration.BankReconciliation.SupplierInvoices;
using WebApp.ViewModels.Invoices;

namespace WebApp.Services.Integration.BankReconciliation.Invoices;

// Resolves customer and supplier invoice candidates for the bank reconciliation workflow.
public sealed class BankReconciliationInvoiceCandidateService : IBankReconciliationInvoiceCandidateService
{
    private const int MaxCombinedWindowSize = 10_000;
    private readonly IInvoicesService _invoicesService;
    private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;
    private readonly IBankReconciliationDemoDataService _demoDataService;
    private readonly IBankReconciliationSupplierInvoiceService _supplierInvoiceService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BankReconciliationInvoiceCandidateService> _logger;

    public BankReconciliationInvoiceCandidateService(
        IInvoicesService invoicesService,
        IJeevesRuntimeContextService jeevesRuntimeContextService,
        IBankReconciliationDemoDataService demoDataService,
        IBankReconciliationSupplierInvoiceService supplierInvoiceService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<BankReconciliationInvoiceCandidateService> logger)
    {
        _invoicesService = invoicesService;
        _jeevesRuntimeContextService = jeevesRuntimeContextService;
        _demoDataService = demoDataService;
        _supplierInvoiceService = supplierInvoiceService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BankReconciliationInvoiceCandidateResult> LoadAsync(
        bool isDemoMode,
        UserSession user,
        CancellationToken cancellationToken,
        BankReconciliationParsedTransaction? transaction = null,
        string? classificationFilter = null,
        string? groupFilter = null,
        int? page = null,
        int? pageSize = null,
        string? demoScenarioKey = null)
    {
        if (isDemoMode)
        {
            var demoScenario = await _demoDataService.LoadScenarioAsync(demoScenarioKey, cancellationToken);
            var demoInvoices = demoScenario.Data.Invoices.Select(MapDemoInvoiceToInvoiceItem).ToList();
            return PageInvoiceCandidates(demoInvoices, page, pageSize, null);
        }

        var runtimeContext = await _jeevesRuntimeContextService.ResolveAsync(user, cancellationToken);
        if (!runtimeContext.Success || runtimeContext.Value is null)
        {
            return new BankReconciliationInvoiceCandidateResult
            {
                ErrorMessage = BuildRuntimeContextError(runtimeContext.Error)
            };
        }

        try
        {
            var invoices = new List<InvoiceItem>();
            var candidateSource = ResolveInvoiceCandidateSource(transaction, classificationFilter, groupFilter);

            if (candidateSource is BankReconciliationInvoiceCandidateSource.Customer or BankReconciliationInvoiceCandidateSource.All)
            {
                invoices.AddRange(await LoadCustomerInvoiceCandidatesAsync(
                    runtimeContext.Value.ConnectionString,
                    runtimeContext.Value.CompanyCode));
            }

            if (candidateSource is BankReconciliationInvoiceCandidateSource.Supplier or BankReconciliationInvoiceCandidateSource.All)
            {
                invoices.AddRange(await LoadSupplierInvoiceCandidatesAsync(
                    runtimeContext.Value.ConnectionString,
                    runtimeContext.Value.CompanyCode,
                    cancellationToken));
            }

            return PageInvoiceCandidates(invoices, page, pageSize, null);
        }
        catch (Exception ex)
        {
            return new BankReconciliationInvoiceCandidateResult
            {
                ErrorMessage = BankReconciliationErrorHandling.LogAndBuildUserMessage(
                    _logger,
                    _httpContextAccessor.HttpContext,
                    nameof(BankReconciliationInvoiceCandidateService),
                    "Bankavstämningens fakturor kunde inte laddas just nu.",
                    ex)
            };
        }
    }

    public async Task<BankReconciliationInvoiceCandidateResult> LoadCustomerPageAsync(
        UserSession user,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var runtimeContext = await _jeevesRuntimeContextService.ResolveAsync(user, cancellationToken);
        if (!runtimeContext.Success || runtimeContext.Value is null)
        {
            return new BankReconciliationInvoiceCandidateResult
            {
                ErrorMessage = BuildRuntimeContextError(runtimeContext.Error)
            };
        }

        return await LoadCustomerInvoiceCandidatesPageAsync(
            runtimeContext.Value.ConnectionString,
            runtimeContext.Value.CompanyCode,
            page,
            pageSize);
    }

    public async Task<BankReconciliationInvoiceCandidateResult> LoadSupplierPageAsync(
        UserSession user,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var runtimeContext = await _jeevesRuntimeContextService.ResolveAsync(user, cancellationToken);
        if (!runtimeContext.Success || runtimeContext.Value is null)
        {
            return new BankReconciliationInvoiceCandidateResult
            {
                ErrorMessage = BuildRuntimeContextError(runtimeContext.Error)
            };
        }

        var result = await _supplierInvoiceService.GetPaymentCandidatesAsync(
            runtimeContext.Value.ConnectionString,
            new BankReconciliationSupplierInvoiceQuery
            {
                CompanyCode = runtimeContext.Value.CompanyCode,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken);

        return new BankReconciliationInvoiceCandidateResult
        {
            Invoices = result.Invoices.ToList(),
            TotalCount = result.TotalCount
        };
    }

    public async Task<BankReconciliationInvoiceCandidateResult> LoadCombinedPageAsync(
        UserSession user,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var requiredWindow = (long)safePage * safePageSize;
        if (requiredWindow > MaxCombinedWindowSize)
        {
            return new BankReconciliationInvoiceCandidateResult
            {
                ErrorMessage =
                    "Den kombinerade fakturalistan är för stor för sidnavigering. " +
                    "Filtrera på kund- eller leverantörsfakturor."
            };
        }

        var runtimeContext = await _jeevesRuntimeContextService.ResolveAsync(user, cancellationToken);
        if (!runtimeContext.Success || runtimeContext.Value is null)
        {
            return new BankReconciliationInvoiceCandidateResult
            {
                ErrorMessage = BuildRuntimeContextError(runtimeContext.Error)
            };
        }

        var windowSize = (int)requiredWindow;
        var customerTask = LoadCustomerInvoiceCandidatesPageAsync(
            runtimeContext.Value.ConnectionString,
            runtimeContext.Value.CompanyCode,
            page: 1,
            pageSize: windowSize);
        var supplierTask = _supplierInvoiceService.GetPaymentCandidatesAsync(
            runtimeContext.Value.ConnectionString,
            new BankReconciliationSupplierInvoiceQuery
            {
                CompanyCode = runtimeContext.Value.CompanyCode,
                Page = 1,
                PageSize = windowSize
            },
            cancellationToken);

        await Task.WhenAll(customerTask, supplierTask);
        var customerResult = await customerTask;
        var supplierResult = await supplierTask;
        var offset = (safePage - 1) * safePageSize;
        var invoices = customerResult.Invoices
            .Concat(supplierResult.Invoices)
            .OrderBy(invoice => invoice.DueDate)
            .ThenBy(invoice => invoice.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(invoice => invoice.IsSupplierInvoice)
            .Skip(offset)
            .Take(safePageSize)
            .ToList();

        return new BankReconciliationInvoiceCandidateResult
        {
            Invoices = invoices,
            TotalCount = customerResult.TotalCount + supplierResult.TotalCount
        };
    }

    private string BuildRuntimeContextError(string? diagnostic)
        => BankReconciliationErrorHandling.LogDiagnosticAndBuildUserMessage(
            _logger,
            _httpContextAccessor.HttpContext,
            "ResolveBankReconciliationRuntimeContext",
            "Tenantdata kunde inte laddas just nu.",
            diagnostic);

    private async Task<IReadOnlyList<InvoiceItem>> LoadCustomerInvoiceCandidatesAsync(
        string connectionString,
        int? companyCode)
    {
        var defaultPeriod = ListPeriodSelection.Create(null, null, null);
        var model = await _invoicesService.GetInvoiceListAsync(
            connectionString,
            new GetInvoicesQuery
            {
                CompanyCode = companyCode,
                FromDate = defaultPeriod.FromDate,
                ToDate = defaultPeriod.ToDate,
                ActiveTab = "unpaid",
                SelectedYear = defaultPeriod.SelectedYear,
                AvailableYears = defaultPeriod.AvailableYears,
                UsesDefaultPeriod = defaultPeriod.UsesDefaultPeriod
            });

        return model.UnpaidInvoices;
    }

    private async Task<BankReconciliationInvoiceCandidateResult> LoadCustomerInvoiceCandidatesPageAsync(
        string connectionString,
        int? companyCode,
        int page,
        int pageSize)
    {
        var defaultPeriod = ListPeriodSelection.Create(null, null, null);
        var model = await _invoicesService.GetInvoiceListAsync(
            connectionString,
            new GetInvoicesQuery
            {
                CompanyCode = companyCode,
                FromDate = defaultPeriod.FromDate,
                ToDate = defaultPeriod.ToDate,
                ActiveTab = "unpaid",
                SelectedYear = defaultPeriod.SelectedYear,
                AvailableYears = defaultPeriod.AvailableYears,
                UsesDefaultPeriod = defaultPeriod.UsesDefaultPeriod,
                Page = page,
                PageSize = pageSize
            });

        return new BankReconciliationInvoiceCandidateResult
        {
            Invoices = model.UnpaidInvoices.ToList(),
            TotalCount = model.TotalCount
        };
    }

    private async Task<IReadOnlyList<InvoiceItem>> LoadSupplierInvoiceCandidatesAsync(
        string connectionString,
        int? companyCode,
        CancellationToken cancellationToken)
    {
        var result = await _supplierInvoiceService.GetPaymentCandidatesAsync(
            connectionString,
            new BankReconciliationSupplierInvoiceQuery
            {
                CompanyCode = companyCode,
                Page = 1,
                PageSize = 1000
            },
            cancellationToken);

        return result.Invoices;
    }

    private static BankReconciliationInvoiceCandidateResult PageInvoiceCandidates(
        IReadOnlyList<InvoiceItem> invoices,
        int? page,
        int? pageSize,
        string? errorMessage)
    {
        var ordered = invoices
            .OrderBy(invoice => invoice.DueDate)
            .ThenBy(invoice => invoice.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!page.HasValue || !pageSize.HasValue)
        {
            return new BankReconciliationInvoiceCandidateResult
            {
                Invoices = ordered,
                TotalCount = ordered.Count,
                ErrorMessage = errorMessage
            };
        }

        var safePage = Math.Max(page.Value, 1);
        var safePageSize = Math.Clamp(pageSize.Value, 1, 1000);
        return new BankReconciliationInvoiceCandidateResult
        {
            Invoices = ordered
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .ToList(),
            TotalCount = ordered.Count,
            ErrorMessage = errorMessage
        };
    }

    private static InvoiceItem MapDemoInvoiceToInvoiceItem(BankReconciliationDemoInvoice invoice)
    {
        var dueDate = DateTime.TryParse(invoice.DueDate, out var parsedDueDate)
            ? parsedDueDate
            : DateTime.Today;

        return new InvoiceItem
        {
            InvoiceNo = string.IsNullOrWhiteSpace(invoice.InvoiceNo) ? invoice.Id : invoice.InvoiceNo!,
            Customer = invoice.CustomerName ?? string.Empty,
            DueDate = dueDate,
            AmountSek = invoice.Amount,
            AmountExclVat = invoice.Amount,
            RemainingAmount = invoice.Amount,
            Ocr = invoice.Ocr ?? string.Empty,
            CompanyCode = "DEMO",
            IsPaid = false,
            Status = dueDate.Date < DateTime.Today ? "Förfallen" : "Obetald"
        };
    }

    public static BankReconciliationInvoiceCandidateSource ResolveInvoiceCandidateSource(
        BankReconciliationParsedTransaction? transaction,
        string? classificationFilter,
        string? groupFilter)
    {
        if (transaction is not null)
        {
            if (IsSupplierInvoiceTransaction(transaction))
                return BankReconciliationInvoiceCandidateSource.Supplier;

            if (IsCustomerInvoiceTransaction(transaction))
                return BankReconciliationInvoiceCandidateSource.Customer;

            return BankReconciliationInvoiceCandidateSource.None;
        }

        var normalizedClassification = NormalizeFilter(classificationFilter);
        if (normalizedClassification is "bankinbetalningar" or "def")
            return BankReconciliationInvoiceCandidateSource.Customer;

        if (normalizedClassification == "leverantorsbetalning")
            return BankReconciliationInvoiceCandidateSource.Supplier;

        if (!string.IsNullOrWhiteSpace(normalizedClassification) && normalizedClassification != "all")
            return BankReconciliationInvoiceCandidateSource.None;

        var normalizedGroup = NormalizeFilter(groupFilter);
        if (normalizedGroup == "kundinbetalningar")
            return BankReconciliationInvoiceCandidateSource.Customer;

        if (normalizedGroup == "leverantorsutbetalningar")
            return BankReconciliationInvoiceCandidateSource.Supplier;

        if (!string.IsNullOrWhiteSpace(normalizedGroup) && normalizedGroup != "all")
            return BankReconciliationInvoiceCandidateSource.None;

        return BankReconciliationInvoiceCandidateSource.All;
    }

    private static bool IsCustomerInvoiceTransaction(BankReconciliationParsedTransaction transaction)
        => string.Equals(transaction.Group, "Kundinbetalningar", StringComparison.OrdinalIgnoreCase)
           || string.Equals(transaction.Classification?.TypeKey, "bankinbetalningar", StringComparison.OrdinalIgnoreCase)
           || string.Equals(transaction.Classification?.TypeKey, "def", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupplierInvoiceTransaction(BankReconciliationParsedTransaction transaction)
        => string.Equals(transaction.Group, "Leverantorsutbetalningar", StringComparison.OrdinalIgnoreCase)
           || string.Equals(transaction.Classification?.TypeKey, "leverantorsbetalning", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}

public enum BankReconciliationInvoiceCandidateSource
{
    None,
    Customer,
    Supplier,
    All
}
