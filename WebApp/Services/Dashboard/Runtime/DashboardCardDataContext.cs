// Loads shared dashboard sources once per request and preserves source-specific failure state.
using WebApp.Models.Dashboard;
using WebApp.Services.Application;
using WebApp.Services.Invoices;
using WebApp.Services.Orders;
using WebApp.ViewModels.Invoices;

namespace WebApp.Services.Dashboard;

public sealed class DashboardCardDataContext
{
    private readonly object _gate = new();
    private readonly JeevesRuntimeContext? _runtimeContext;
    private readonly IInvoicesService _invoicesService;
    private readonly IOrdersAnalyticsService _ordersAnalyticsService;
    private readonly ILogger<DashboardCardDataContext> _logger;
    private Task<DashboardDataResult<InvoiceListViewModel>>? _invoices;
    private Task<DashboardDataResult<RevenueDataModel>>? _revenue;

    public DashboardCardDataContext(
        JeevesRuntimeContext? runtimeContext,
        IInvoicesService invoicesService,
        IOrdersAnalyticsService ordersAnalyticsService,
        ILogger<DashboardCardDataContext> logger)
    {
        _runtimeContext = runtimeContext;
        _invoicesService = invoicesService;
        _ordersAnalyticsService = ordersAnalyticsService;
        _logger = logger;
    }

    public Task<DashboardDataResult<InvoiceListViewModel>> GetInvoicesAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return _invoices ??= LoadInvoicesAsync(cancellationToken);
        }
    }

    public Task<DashboardDataResult<RevenueDataModel>> GetRevenueAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return _revenue ??= LoadRevenueAsync(cancellationToken);
        }
    }

    private async Task<DashboardDataResult<InvoiceListViewModel>> LoadInvoicesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_runtimeContext is null)
        {
            return DashboardDataResult<InvoiceListViewModel>.Failure(new InvoiceListViewModel());
        }

        try
        {
            var invoices = await _invoicesService.GetDashboardSummaryAsync(
                _runtimeContext.ConnectionString,
                _runtimeContext.CompanyCode);
            cancellationToken.ThrowIfCancellationRequested();
            return DashboardDataResult<InvoiceListViewModel>.Success(invoices);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load invoice dashboard data for company {CompanyName} ({CompanyCode}).",
                _runtimeContext.CompanyName,
                _runtimeContext.CompanyCode);
            return DashboardDataResult<InvoiceListViewModel>.Failure(new InvoiceListViewModel());
        }
    }

    private async Task<DashboardDataResult<RevenueDataModel>> LoadRevenueAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_runtimeContext is null)
        {
            return DashboardDataResult<RevenueDataModel>.Failure(new RevenueDataModel());
        }

        try
        {
            var revenue = await _ordersAnalyticsService.GetRevenueAsync(
                _runtimeContext.ConnectionString,
                _runtimeContext.CompanyCode);
            cancellationToken.ThrowIfCancellationRequested();
            return DashboardDataResult<RevenueDataModel>.Success(revenue);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load revenue dashboard data for company {CompanyName} ({CompanyCode}).",
                _runtimeContext.CompanyName,
                _runtimeContext.CompanyCode);
            return DashboardDataResult<RevenueDataModel>.Failure(new RevenueDataModel());
        }
    }
}

public sealed record DashboardDataResult<T>(T Value, bool Failed)
{
    public static DashboardDataResult<T> Success(T value) => new(value, Failed: false);
    public static DashboardDataResult<T> Failure(T fallback) => new(fallback, Failed: true);
}
