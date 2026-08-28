// Handles invoice pages and enforces active-company access before reading business data.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using WebApp.Services.Invoices;
using Entities.Application;
using WebApp.Services;
using WebApp.Services.Application;
using WebApp.Filters;
using WebApp.Models.Application;
using WebApp.Models.Invoices;
using WebApp.ViewModels.Invoices;
using WebApp.ViewModels.Shared;

namespace WebApp.Controllers
{
    /// <summary>
    /// Handles invoice list and detail pages for the active tenant/company context.
    /// </summary>
    [Authorize(Roles = "Administrator, User")]
    [ServiceFilter(typeof(TenantValidationFilter))]
    public class InvoicesController : Controller
    {
        private readonly IInvoicesService _invoicesService;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;
        private readonly IFeatureAccessService _featureAccessService;
        private readonly ICompanyPermissionGuard _companyPermissionGuard;
        private readonly IPortalEventLogService _portalEventLogService;
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(IInvoicesService invoicesService, IHttpContextAccessor contextAccessor, IJeevesRuntimeContextService jeevesRuntimeContextService, IFeatureAccessService featureAccessService, ICompanyPermissionGuard companyPermissionGuard, IPortalEventLogService portalEventLogService, ILogger<InvoicesController> logger)
        {
            _invoicesService = invoicesService;
            _contextAccessor = contextAccessor;
            _jeevesRuntimeContextService = jeevesRuntimeContextService;
            _featureAccessService = featureAccessService;
            _companyPermissionGuard = companyPermissionGuard;
            _portalEventLogService = portalEventLogService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string tab = "unpaid", int page = 1, int? year = null)
        {
            var userContext = await GetUserContextAsync();
            if (userContext is null || !IsFeatureAllowed(userContext.CompanyCode))
                return Forbid();
            if (!await HasCompanyPermissionAsync(userContext.CompanyId))
                return Forbid();
            var search = HttpContext.Request.Query["search"].ToString();
            DateTime? fromDate = null;
            DateTime? toDate = null;
            if (DateTime.TryParse(HttpContext.Request.Query["from"], out var f)) fromDate = f;
            if (DateTime.TryParse(HttpContext.Request.Query["to"], out var t)) toDate = t;
            // Keep year/default-period rules identical between invoices and orders.
            var period = ListPeriodSelection.Create(fromDate, toDate, year);

            try
            {
                var model = await _invoicesService.GetInvoiceListAsync(
                    userContext.ConnectionString,
                    new GetInvoicesQuery
                    {
                        CompanyCode = userContext.CompanyCode,
                        Search = search,
                        FromDate = period.FromDate,
                        ToDate = period.ToDate,
                        ActiveTab = tab,
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
                _logger.LogError(ex, "Failed to load Invoices index. CompanyId: {CompanyId}, CompanyCode: {CompanyCode}", userContext.CompanyId, userContext.CompanyCode);
                await _portalEventLogService.RecordAsync(BuildErrorEntry(userContext, "Invoices", "Index", "Failed to load invoices index.", ex, HttpContext?.Request?.Path.Value));
                return View("ModuleUnavailable", BuildUnavailableViewModel("Fakturor kunde inte laddas", "Fakturor", userContext.CompanyName, "Portalen fungerar, men fakturadata från den aktiva datakällan kunde inte laddas just nu."));
            }
        }

        [HttpGet]
        [Route("Invoices/api")]
        public async Task<IActionResult> Api(string tab = "unpaid", int page = 1, int? year = null)
        {
            var userContext = await GetUserContextAsync();
            if (userContext is null || !IsFeatureAllowed(userContext.CompanyCode))
                return Forbid();
            if (!await HasCompanyPermissionAsync(userContext.CompanyId))
                return Forbid();
            var search = HttpContext.Request.Query["search"].ToString();
            DateTime? fromDate = null;
            DateTime? toDate = null;
            if (DateTime.TryParse(HttpContext.Request.Query["from"], out var f)) fromDate = f;
            if (DateTime.TryParse(HttpContext.Request.Query["to"], out var t)) toDate = t;
            // API responses must use the exact same normalized period as the server-rendered page.
            var period = ListPeriodSelection.Create(fromDate, toDate, year);

            try
            {
                var model = await _invoicesService.GetInvoiceListAsync(
                    userContext.ConnectionString,
                    new GetInvoicesQuery
                    {
                        CompanyCode = userContext.CompanyCode,
                        Search = search,
                        FromDate = period.FromDate,
                        ToDate = period.ToDate,
                        ActiveTab = tab,
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
                _logger.LogError(ex, "Failed to load Invoices API. CompanyId: {CompanyId}, CompanyCode: {CompanyCode}", userContext.CompanyId, userContext.CompanyCode);
                await _portalEventLogService.RecordAsync(BuildErrorEntry(userContext, "Invoices", "Api", "Failed to load invoices API.", ex, HttpContext?.Request?.Path.Value));
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    error = "Fakturadata kunde inte laddas från den aktiva datakällan."
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Detail(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return RedirectToAction(nameof(Index));

            var userContext = await GetUserContextAsync();
            if (userContext is null || !IsFeatureAllowed(userContext.CompanyCode))
                return Forbid();
            if (!await HasCompanyPermissionAsync(userContext.CompanyId))
                return Forbid();
            try
            {
                var invoice = await _invoicesService.GetInvoiceAsync(userContext.ConnectionString, userContext.CompanyCode, id);
                if (invoice == null)
                    return RedirectToAction(nameof(Index));

                return View(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load invoice detail. InvoiceNo: {InvoiceNo}, CompanyId: {CompanyId}, CompanyCode: {CompanyCode}", id, userContext.CompanyId, userContext.CompanyCode);
                await _portalEventLogService.RecordAsync(BuildErrorEntry(userContext, "Invoices", "Detail", $"Failed to load invoice detail for invoice {id}.", ex, HttpContext?.Request?.Path.Value));
                return View("ModuleUnavailable", BuildUnavailableViewModel("Fakturadetaljer kunde inte laddas", "Fakturor", userContext.CompanyName, "Detaljerna för vald faktura kunde inte läsas från den aktiva datakällan."));
            }
        }

        public IActionResult Overdue()
        {
            return RedirectToAction(nameof(Index), new { tab = "unpaid" });
        }

        private async Task<JeevesRuntimeContext?> GetUserContextAsync()
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var contextResult = await _jeevesRuntimeContextService.ResolveAsync(user, HttpContext.RequestAborted);
            return contextResult.Success ? contextResult.Value : null;
        }

        private bool IsFeatureAllowed(int companyCode)
        {
            return _featureAccessService.IsEnabled(HttpContext.Session, companyCode, FeatureFlag.Invoices);
        }

        private async Task<bool> HasCompanyPermissionAsync(Guid companyId)
        {
            return await _companyPermissionGuard.HasAccessAsync(companyId, PortalModuleIds.InvoicesSubModule);
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
                    IconClass = "fa fa-file-text-o",
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
