// Handles order pages and enforces active-company access before reading business data.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WebApp.Services.Orders;
using Entities.Application;
using WebApp.Services;
using WebApp.Filters;
using WebApp.Services.Application;
using WebApp.Models.Application;
using WebApp.Models.Orders;
using WebApp.ViewModels.Shared;

namespace WebApp.Controllers
{
    /// <summary>
    /// Handles the orders list and detail views for the active tenant/company context.
    /// </summary>
    [Authorize(Roles = "Administrator, User")]
    [ServiceFilter(typeof(TenantValidationFilter))]
    public class OrdersController : Controller
    {
        private readonly IOrdersService _ordersService;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;
        private readonly IFeatureAccessService _featureAccessService;
        private readonly ICompanyPermissionGuard _companyPermissionGuard;
        private readonly IPortalEventLogService _portalEventLogService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrdersService ordersService, IHttpContextAccessor contextAccessor, IJeevesRuntimeContextService jeevesRuntimeContextService, IFeatureAccessService featureAccessService, ICompanyPermissionGuard companyPermissionGuard, IPortalEventLogService portalEventLogService, ILogger<OrdersController> logger)
        {
            _ordersService = ordersService;
            _contextAccessor = contextAccessor;
            _jeevesRuntimeContextService = jeevesRuntimeContextService;
            _featureAccessService = featureAccessService;
            _companyPermissionGuard = companyPermissionGuard;
            _portalEventLogService = portalEventLogService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string sort = "date", string dir = "desc", int page = 1, int? year = null)
        {
            var userContext = await GetUserContextAsync();
            if (userContext is null || !IsFeatureAllowed(userContext.CompanyCode))
                return Forbid();
            if (!await HasCompanyPermissionAsync(userContext.CompanyId))
                return Forbid();
            var search = HttpContext.Request.Query["search"].ToString();
            var payment = HttpContext.Request.Query["payment"].ToString();
            DateTime? fromDate = null;
            DateTime? toDate = null;
            if (DateTime.TryParse(HttpContext.Request.Query["from"], out var f)) fromDate = f;
            if (DateTime.TryParse(HttpContext.Request.Query["to"], out var t)) toDate = t;
            // Keep year/default-period rules identical between orders and invoices.
            var period = ListPeriodSelection.Create(fromDate, toDate, year);

            try
            {
                var model = await _ordersService.GetOrdersAsync(
                    userContext.ConnectionString,
                    new GetOrdersQuery
                    {
                        Sort = sort,
                        Desc = dir?.ToLower() != "asc",
                        CompanyCode = userContext.CompanyCode,
                        Search = search,
                        FromDate = period.FromDate,
                        ToDate = period.ToDate,
                        PaymentFilter = payment,
                        Page = page,
                        PageSize = 50,
                        SelectedYear = period.SelectedYear,
                        AvailableYears = period.AvailableYears,
                        UsesDefaultPeriod = period.UsesDefaultPeriod
                    });
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load Orders index. CompanyId: {CompanyId}, CompanyCode: {CompanyCode}", userContext.CompanyId, userContext.CompanyCode);
                await _portalEventLogService.RecordAsync(BuildErrorEntry(userContext, "Orders", "Index", "Failed to load orders index.", ex, HttpContext?.Request?.Path.Value));
                return View("ModuleUnavailable", BuildUnavailableViewModel("Orderdata kunde inte laddas", "Orders", userContext.CompanyName, "Portalen fungerar, men orderdata från den aktiva datakällan kunde inte laddas just nu."));
            }
        }

        [HttpGet("Orders/api")]
        public async Task<IActionResult> Api(string sort = "date", string dir = "desc", int page = 1, int? year = null)
        {
            var userContext = await GetUserContextAsync();
            if (userContext is null || !IsFeatureAllowed(userContext.CompanyCode))
                return Forbid();
            if (!await HasCompanyPermissionAsync(userContext.CompanyId))
                return Forbid();
            var search = HttpContext.Request.Query["search"].ToString();
            var payment = HttpContext.Request.Query["payment"].ToString();
            DateTime? fromDate = null;
            DateTime? toDate = null;
            if (DateTime.TryParse(HttpContext.Request.Query["from"], out var f)) fromDate = f;
            if (DateTime.TryParse(HttpContext.Request.Query["to"], out var t)) toDate = t;
            // API responses must use the exact same normalized period as the server-rendered page.
            var period = ListPeriodSelection.Create(fromDate, toDate, year);

            try
            {
                var model = await _ordersService.GetOrdersAsync(
                    userContext.ConnectionString,
                    new GetOrdersQuery
                    {
                        Sort = sort,
                        Desc = dir?.ToLower() != "asc",
                        CompanyCode = userContext.CompanyCode,
                        Search = search,
                        FromDate = period.FromDate,
                        ToDate = period.ToDate,
                        PaymentFilter = payment,
                        Page = page,
                        PageSize = 50,
                        SelectedYear = period.SelectedYear,
                        AvailableYears = period.AvailableYears,
                        UsesDefaultPeriod = period.UsesDefaultPeriod
                    });
                return Json(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load Orders API. CompanyId: {CompanyId}, CompanyCode: {CompanyCode}", userContext.CompanyId, userContext.CompanyCode);
                await _portalEventLogService.RecordAsync(BuildErrorEntry(userContext, "Orders", "Api", "Failed to load orders API.", ex, HttpContext?.Request?.Path.Value));
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    error = "Orderdata kunde inte laddas från den aktiva datakällan."
                });
            }
        }

        public async Task<IActionResult> Detail(long id)
        {
            var userContext = await GetUserContextAsync();
            if (userContext is null || !IsFeatureAllowed(userContext.CompanyCode))
                return Forbid();
            if (!await HasCompanyPermissionAsync(userContext.CompanyId))
                return Forbid();
            try
            {
                var model = await _ordersService.GetOrderDetailsAsync(
                    userContext.ConnectionString,
                    new GetOrderDetailsQuery
                    {
                        OrderNo = id,
                        CompanyCode = userContext.CompanyCode,
                        CompanyId = userContext.CompanyId
                    });
                if (model == null) return RedirectToAction(nameof(Index));
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load order detail. OrderNo: {OrderNo}, CompanyId: {CompanyId}, CompanyCode: {CompanyCode}", id, userContext.CompanyId, userContext.CompanyCode);
                await _portalEventLogService.RecordAsync(BuildErrorEntry(userContext, "Orders", "Detail", $"Failed to load order detail for order {id}.", ex, HttpContext?.Request?.Path.Value));
                return View("ModuleUnavailable", BuildUnavailableViewModel("Orderdetaljer kunde inte laddas", "Orders", userContext.CompanyName, "Detaljerna för vald order kunde inte läsas från den aktiva datakällan."));
            }
        }

        public async Task<IActionResult> DeliveryForecast(int months = 6, string? customer = null, int page = 1)
        {
            var userContext = await GetUserContextAsync();
            if (userContext is null || !IsFeatureAllowed(userContext.CompanyCode))
                return Forbid();
            if (!await HasCompanyPermissionAsync(userContext.CompanyId))
                return Forbid();

            try
            {
                ViewData["OrdersSection"] = "forecast";
                var model = await _ordersService.GetDeliveryForecastAsync(
                    userContext.ConnectionString,
                    new GetDeliveryForecastQuery
                    {
                        CompanyCode = userContext.CompanyCode,
                        MonthsAhead = months,
                        CustomerNo = customer,
                        Page = page,
                        PageSize = 25
                    });
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load Orders delivery forecast. CompanyId: {CompanyId}, CompanyCode: {CompanyCode}", userContext.CompanyId, userContext.CompanyCode);
                await _portalEventLogService.RecordAsync(BuildErrorEntry(userContext, "Orders", "DeliveryForecast", "Failed to load delivery forecast.", ex, HttpContext?.Request?.Path.Value));
                return View("ModuleUnavailable", BuildUnavailableViewModel("Orderprognosen kunde inte laddas", "Orders", userContext.CompanyName, "Kommande orders och prognosdata kunde inte läsas från den aktiva datakällan."));
            }
        }

        private async Task<JeevesRuntimeContext?> GetUserContextAsync()
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var contextResult = await _jeevesRuntimeContextService.ResolveAsync(user, HttpContext.RequestAborted);
            return contextResult.Success ? contextResult.Value : null;
        }

        private bool IsFeatureAllowed(int companyCode)
        {
            return _featureAccessService.IsEnabled(HttpContext.Session, companyCode, FeatureFlag.Orders);
        }

        private async Task<bool> HasCompanyPermissionAsync(Guid companyId)
        {
            return await _companyPermissionGuard.HasAccessAsync(companyId, PortalModuleIds.OrdersSubModule);
        }

        private static ModuleUnavailableViewModel BuildUnavailableViewModel(string title, string moduleLabel, string? companyName, string message)
        {
            return new ModuleUnavailableViewModel
            {
                ModuleLabel = moduleLabel,
                Title = title,
                Subtitle = string.IsNullOrWhiteSpace(companyName) ? null : $"Visar data för: {companyName}",
                State = new ModuleStateViewModel
                {
                    Title = title,
                    Message = message,
                    Tone = "warning",
                    IconClass = "fa fa-box-open",
                    ActionText = "Ladda om sidan",
                    ActionUrl = string.Empty
                }
            };
        }

        private static PortalEventLogEntry BuildErrorEntry(
            JeevesRuntimeContext userContext,
            string module,
            string action,
            string message,
            Exception exception,
            string? requestPath)
        {
            return new PortalEventLogEntry
            {
                Module = module,
                Action = action,
                CompanyId = userContext.CompanyId,
                CompanyName = userContext.CompanyName,
                JeevesCompanyCode = userContext.CompanyCode,
                UserEmail = userContext.Email,
                RequestPath = requestPath,
                CorrelationId = null,
                Message = message,
                Exception = exception
            };
        }
    }
}
