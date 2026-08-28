// Handles purchase requests and reports safe Jeeves failures to the portal UI.
using Entities.Application;
using Entities.Contracts;
using Entities.Purchase;
using LoggerService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Localization;
using NotificationService;
using Repository.Contracts;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Models.Identity;
using WebApp.Observability;
using WebApp.Services;
using WebApp.Services.Integration;
using WebApp.Services.Purchase.Demo;
using WebApp.Services.Purchase.Context;
using WebApp.Services.Purchase.Lookup;
using WebApp.Services.Purchase.Orders;
using WebApp.ViewModels.Shared;

namespace WebApp.Controllers
{
    // Handles purchase order browsing, acknowledgements, and lookup flows in the portal.
    [Authorize(Roles = "Administrator, User")]
    public class PurchaseController : BaseController
    {
        private readonly IPurchaseLookupService _purchaseLookupService;
        private readonly IPurchaseOrderService _purchaseOrderService;
        private readonly IPurchaseDemoModeService _purchaseDemoModeService;
        private readonly IPurchaseDemoDataService _purchaseDemoDataService;
        private readonly IStringLocalizer<SharedResources> _localizer;
        private readonly ILoggerManager _loggerManager;

        public PurchaseController(IHttpContextAccessor contextAccessor,
                                    IApplicationUserRepository applicationUserRepository,
                                    INotificationManager notificationManager,
                                    IPurchaseLookupService purchaseLookupService,
                                    IPurchaseOrderService purchaseOrderService,
                                    IPurchaseDemoModeService purchaseDemoModeService,
                                    IPurchaseDemoDataService purchaseDemoDataService,
                                    IStringLocalizer<SharedResources> localizer,
                                    ILoggerManager loggerManager,
                                    IApplicationHelper applicationHelper,
                                    ApplicationDbContext context)
                                :base(contextAccessor,
                                        applicationUserRepository,
                                        notificationManager,
                                        applicationHelper,
                                        context)
        {
            _purchaseLookupService = purchaseLookupService;
            _purchaseOrderService = purchaseOrderService;
            _purchaseDemoModeService = purchaseDemoModeService;
            _purchaseDemoDataService = purchaseDemoDataService;
            _localizer = localizer;
            _loggerManager = loggerManager;
        }

        private IActionResult RenderModuleUnavailable(string title, string message)
        {
            var companyName = HttpContext?.Session?.Get<UserSession>("UserObject")?.CompanyName;

            return View("ModuleUnavailable", new ModuleUnavailableViewModel
            {
                ModuleLabel = "Inköp",
                Title = title,
                Subtitle = string.IsNullOrWhiteSpace(companyName) ? null : $"Visar data för: {companyName}",
                State = new ModuleStateViewModel
                {
                    Title = title,
                    Message = message,
                    Note = "Portalen fungerar, men inköpsdata från den aktiva datakällan kunde inte laddas just nu.",
                    Tone = "warning",
                    IconClass = "fa fa-shopping-cart",
                    ActionText = "Ladda om sidan",
                    ActionUrl = string.Empty
                }
            });
        }

        public async Task<IActionResult> PurchaseOrders()
        {
            try
            {
                ViewBag.PurchaseIsDemoMode = _purchaseDemoModeService.IsEnabled();
                ViewBag.PurchaseCanUseDemo = User.IsInRole("Administrator");
                var data = await _purchaseOrderService.GetMyPurchaseOrdersAsync(HttpContext.RequestAborted);

                return View(data);
            }
            catch (Exception ex)
            {
                LogPurchaseError("Failed to load PurchaseOrders", ex);
                return RenderModuleUnavailable(
                    "Inköpsorder kunde inte laddas",
                    "Portalen fungerar, men inköpsorder från den aktiva datakällan kunde inte laddas just nu.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> OrderAcknowledgement(int orderNumber = 12345)
        {
            try
            {
                var data = await _purchaseDemoDataService.FindOrderAsync(orderNumber, HttpContext.RequestAborted);
                if (data is null)
                {
                    return RenderModuleUnavailable(
                        "Ordererkännande kunde inte laddas",
                        "Demovyn för ordererkännande kunde inte läsas från den lokala demodatakällan.");
                }

                ViewBag.PurchaseAcknowledgementDemo = true;
                return View("PurchaseOrder", data);
            }
            catch (Exception ex)
            {
                LogPurchaseError($"Failed to load OrderAcknowledgement {orderNumber}", ex);
                return RenderModuleUnavailable(
                    "Ordererkännande kunde inte laddas",
                    "Demovyn för ordererkännande kunde inte laddas just nu.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult OrderAcknowledgement(PurchaseOrderVM purchaseOrder)
        {
            try
            {
                ViewBag.PurchaseAcknowledgementDemo = true;

                var selectedRows = purchaseOrder.OrderRows?
                    .Where(x => x.AddToStock)
                    .ToList() ?? new List<PurchaseOrderRowVM>();

                if (selectedRows.Count == 0)
                {
                    ErrorPopup("Markera minst en rad innan du skickar till Jeeves.");
                    return View("PurchaseOrder", purchaseOrder);
                }

                _loggerManager.LogInfo(
                    $"Order acknowledgement for order {purchaseOrder.OrderNumber} submitted with {selectedRows.Count} selected rows.");

                SuccessPopup($"{selectedRows.Count} rader från order {purchaseOrder.OrderNumber} skickades till Jeeves.");
                return RedirectToAction(nameof(PurchaseOrders));
            }
            catch (Exception ex)
            {
                LogPurchaseError($"Failed to submit OrderAcknowledgement {purchaseOrder.OrderNumber}", ex);
                ViewBag.PurchaseAcknowledgementDemo = true;
                ErrorPopup("Ordererkännandet kunde inte skickas till Jeeves just nu.");
                return View("PurchaseOrder", purchaseOrder);
            }
        }

        public async Task<IActionResult> PurchaseOrder(int orderNumber)
        {
            try
            {
                ViewBag.PurchaseIsDemoMode = _purchaseDemoModeService.IsEnabled();
                ViewBag.PurchaseCanUseDemo = User.IsInRole("Administrator");
                var data = await _purchaseOrderService.GetPurchaseOrderAsync(orderNumber, HttpContext.RequestAborted);
                return View(data);
            }
            catch (Exception ex)
            {
                LogPurchaseError($"Failed to load PurchaseOrder {orderNumber}", ex);
                return RenderModuleUnavailable(
                    "Inköpsorder kunde inte laddas",
                    "Detaljerna för vald inköpsorder kunde inte läsas från den aktiva datakällan.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> CreatePurchaseOrder()
        {
            try
            {
                if (_purchaseDemoModeService.IsEnabled())
                {
                    ErrorPopup("Demoläget är skrivskyddat. Slå av demo för att skapa riktiga inköpsorder.");
                    return RedirectToAction(nameof(PurchaseOrders));
                }

                var model = await _purchaseOrderService.CreateEmptyPurchaseOrderAsync(HttpContext.RequestAborted);
                return View(model);
            }
            catch (Exception ex)
            {
                LogPurchaseError("Failed to prepare CreatePurchaseOrder", ex);
                return RenderModuleUnavailable(
                    "Inköpsordern kunde inte förberedas",
                    "Portalen fungerar, men nödvändig inköpsdata kunde inte laddas från den aktiva datakällan.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreatePurchaseOrder(PurchaseOrderVM purchaseOrder)
        {
            try
            {
                if (_purchaseDemoModeService.IsEnabled())
                {
                    ErrorPopup("Demoläget är skrivskyddat. Slå av demo för att skapa riktiga inköpsorder.");
                    return RedirectToAction(nameof(PurchaseOrders));
                }

                if (!ModelState.IsValid)
                    return View(purchaseOrder);

                if (purchaseOrder.OrderRows?.Count < 1)
                {
                    ErrorPopup(_localizer["Purchase_NoOrderRows"]);
                    return View(purchaseOrder);
                }

                var res = await _purchaseOrderService.CreatePurchaseOrderAsync(purchaseOrder, HttpContext.RequestAborted);
                if (res.ValidationFailed)
                {
                    ReportCreatePurchaseOrderFailure(
                        "CreatePurchaseOrder validation failed",
                        res.Message,
                        "Inköpsordern kunde inte skickas");
                    return View(purchaseOrder);
                }

                if (res.Success)
                {
                    SuccessPopup($"Order {res.OrderNumber} was successfully created");
                    return RedirectToAction("PurchaseOrders");
                }
                else
                {
                    ReportCreatePurchaseOrderFailure(
                        "CreatePurchaseOrder rejected by Jeeves",
                        res.Message,
                        "Jeeves nekade inköpsordern");
                }
            }
            catch (SqlException sqlEx)
            {
                if (sqlEx.Errors[0].Class == 16)
                {
                    ReportCreatePurchaseOrderFailure(
                        "CreatePurchaseOrder rejected by SQL",
                        sqlEx.Message,
                        "Jeeves nekade inköpsordern");
                }
            }
            catch (Exception ex)
            {
                ReportCreatePurchaseOrderFailure(
                    "CreatePurchaseOrder failed",
                    ex.Message,
                    "Inköpsordern kunde inte skapas");
            }
            return View(purchaseOrder);
        }

        [HttpPost]
        public async Task<IActionResult> PurchaseOrder(PurchaseOrderVM purchaseOrder)
        {
            try
            {
                if (_purchaseDemoModeService.IsEnabled())
                {
                    ErrorPopup("Demoläget är skrivskyddat. Slå av demo för att uppdatera inköpsorder.");
                    return RedirectToAction(nameof(PurchaseOrders));
                }

                if (!ModelState.IsValid)
                {
                    return View(purchaseOrder);
                }

                if (purchaseOrder.OrderRows.Where(x => x.AddToStock == true).Count() == 0) 
                {
                    ErrorPopup(_localizer["Popup_NoRowsToAdd"]);
                    return View(purchaseOrder);
                }

                var res = await _purchaseOrderService.CreateStockDeliveryAsync(purchaseOrder, HttpContext.RequestAborted);
                if (res.Success)
                {
                    SuccessPopup($"Order {res.OrderNumber} was successfully updated");
                    return RedirectToAction("PurchaseOrders");
                }
                else
                {
                    Error($"Could not update purchase order: {res.Message}");
                }
            }
            catch (SqlException sqlEx)
            {
                if (sqlEx.Errors[0].Class == 16)
                {
                    var supportId = GetOrCreateSupportId();
                    _loggerManager.LogError($"PurchaseOrder update rejected by SQL. SupportId={supportId} {IntegrationLogSanitizer.Diagnostic(sqlEx.Message)}");
                    Error($"Inköpsordern kunde inte uppdateras just nu. Referens: {supportId}.");
                }
            }
            catch (Exception ex)
            {
                var supportId = GetOrCreateSupportId();
                _loggerManager.LogError($"PurchaseOrder update failed. SupportId={supportId} {IntegrationLogSanitizer.Diagnostic(ex.Message)}");
                Error($"Inköpsordern kunde inte uppdateras just nu. Referens: {supportId}.");
            }

            return View(purchaseOrder);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PurchaseToggleDemoMode()
        {
            if (!User.Identity?.IsAuthenticated ?? true || !User.IsInRole("Administrator"))
                return Forbid();

            _purchaseDemoModeService.SetEnabled(!_purchaseDemoModeService.IsEnabled());
            HubToast(_purchaseDemoModeService.IsEnabled()
                ? "Demodata för inköp är aktiverad."
                : "Demodata för inköp är avstängd.");
            return RedirectToAction(nameof(PurchaseOrders));
        }


        [HttpPost]
        public async Task<IActionResult> AutoCompleteSupplier([FromBody]Auto auto)
        {
            try
            {
                var suppliers = await _purchaseLookupService.SearchSuppliersAsync(auto.searchString, HttpContext.RequestAborted);
                return Json(suppliers);
            }
            catch (Exception ex)
            {
                LogPurchaseError("Failed to autocomplete supplier", ex);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Leverantörsdata kunde inte laddas." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AutoCompleteArticleName([FromBody]Auto articleName)
        {
            try
            {
                var articles = await _purchaseLookupService.SearchArticlesAsync(articleName.searchString, HttpContext.RequestAborted);
                return Json(articles);
            }
            catch (Exception ex)
            {
                LogPurchaseError("Failed to autocomplete article name", ex);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Artikeldata kunde inte laddas." });
            }
        }
        public class Auto
        {
            public string searchString { get; set; } = string.Empty;
        }

        private void LogPurchaseError(string operation, Exception exception)
        {
            var supportId = GetOrCreateSupportId();
            _loggerManager.LogError($"{operation}. SupportId={supportId} {IntegrationLogSanitizer.Diagnostic(exception.Message)}");
        }

        private void ReportCreatePurchaseOrderFailure(string operation, string? detail, string userMessage)
        {
            var supportId = GetOrCreateSupportId();
            var diagnostic = IntegrationLogSanitizer.Diagnostic(
                string.IsNullOrWhiteSpace(detail)
                    ? "No error detail was returned from the purchase order command."
                    : detail);
            var message = $"{userMessage}: {diagnostic} Referens: {supportId}.";

            _loggerManager.LogError($"{operation}. SupportId={supportId} {diagnostic}");
            Error(message);
            ErrorPopup(message);
        }

        private string GetOrCreateSupportId()
        {
            var supportId = HttpContext?.Items[PortalObservability.SupportIdItemKey]?.ToString();
            if (!string.IsNullOrWhiteSpace(supportId))
            {
                return supportId!;
            }

            supportId = Guid.NewGuid().ToString("N")[..8];
            if (HttpContext is { } httpContext)
            {
                httpContext.Items[PortalObservability.SupportIdItemKey] = supportId;
            }

            return supportId;
        }
    }
}
