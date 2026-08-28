// Creates request-local dashboard data caches from scoped source services.
using WebApp.Services.Application;
using WebApp.Services.Invoices;
using WebApp.Services.Orders;

namespace WebApp.Services.Dashboard;

public sealed class DashboardCardDataContextFactory
{
    private readonly IInvoicesService _invoicesService;
    private readonly IOrdersAnalyticsService _ordersAnalyticsService;
    private readonly ILogger<DashboardCardDataContext> _logger;

    public DashboardCardDataContextFactory(
        IInvoicesService invoicesService,
        IOrdersAnalyticsService ordersAnalyticsService,
        ILogger<DashboardCardDataContext> logger)
    {
        _invoicesService = invoicesService;
        _ordersAnalyticsService = ordersAnalyticsService;
        _logger = logger;
    }

    public DashboardCardDataContext Create(JeevesRuntimeContext? runtimeContext)
        => new(runtimeContext, _invoicesService, _ordersAnalyticsService, _logger);
}
