// Handles purchase approval list, detail and update actions for WebApproval.
using Entities.ViewModels.WebApproval;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using WebApp.Services.WebApproval;
using WebApp.ViewModels.WebApproval;

namespace WebApp.Controllers
{
    public partial class WebApprovalController
    {
        private const string PurchaseApprovalStoredProcedure = "q_zu_CustomerPortal_WebApprovalPurchase";

        [HttpGet]
        public async Task<IActionResult> PurchaseApproval(int? status = null)
        {
            try
            {
                await InitializeAsync();
                ViewData["CompanyId"] = CurrentUser.CompanyId;

                var selectedStatus = PurchaseApprovalListFilter.NormalizeHistoryStatus(status);
                var currentPersSign = CurrentUserPersSign;
                _loggerManager.LogInfo($"Fetching all Purchase orders for {CurrentUserEmail}, {CurrentUser.JeevesActiveCompany}");
                var data = await _purchaseRepository.GetAllPurchaseAttestOrdersAsync(SqlConnectionString, CurrentUser.JeevesActiveCompany, CurrentUserEmail, selectedStatus);
                var orders = PurchaseApprovalListFilter.ForCurrentUser(data, currentPersSign, selectedStatus);

                if (selectedStatus is not null && orders.Count == 0)
                {
                    var fallbackData = await _purchaseRepository.GetAllPurchaseAttestOrdersAsync(SqlConnectionString, CurrentUser.JeevesActiveCompany, CurrentUserEmail);
                    orders = PurchaseApprovalListFilter.ForCurrentUser(fallbackData, currentPersSign, selectedStatus);
                }

                return View(new PurchaseApprovalListViewModel
                {
                    SelectedStatus = selectedStatus,
                    Orders = orders
                });
            }
            catch (SqlException ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildSqlErrorDetails(
                    "fetching data in PurchaseApproval",
                    ex,
                    PurchaseApprovalStoredProcedure,
                    $"Fetching all Purchase orders for {CurrentUserEmail}, {CurrentUser.JeevesActiveCompany}",
                    $"ForetagKod {_userObject?.JeevesActiveCompany}",
                    $"EmailAddress {_userObject?.Email}");

                await NotifyWebApprovalFailureAsync("Issue when fetching PurchaseApproval", details, ex);
                _loggerManager.LogError(details);

                ErrorPopup($"Inköpsattester kunde inte laddas just nu. Referens: {GetOrCreateSupportId()}.");
                return View(new PurchaseApprovalListViewModel());
            }
            catch (Exception ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildExceptionDetails(
                    ex.Message,
                    PurchaseApprovalStoredProcedure,
                    $"ForetagKod {_userObject?.JeevesActiveCompany}",
                    $"EmailAddress {_userObject?.Email}");

                await NotifyWebApprovalFailureAsync("Issue when fetching all Purchase orders", details, ex);

                LogWebApprovalError("PurchaseApproval failed", ex);
                return RenderModuleUnavailable(
                    "Inköpsattester kunde inte laddas",
                    "Portalen fungerar, men inköpsattester från den aktiva datakällan kunde inte laddas just nu.");
            }
        }

        [HttpGet]
        [Route("WebApproval/PurchaseApprovalDetails/{companyId}/{id}")]
        public async Task<IActionResult> PurchaseApprovalDetails(Guid companyId, Guid id)
        {
            try
            {
                await InitializeAsync();
                var data = await _purchaseRepository.GetAttestPurchaseOrderWithRowsAsync(SqlConnectionString, id);
                return View(data);
            }
            catch (SqlException ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildSqlErrorDetails(
                    "fetching data in PurchaseApprovalDetails",
                    ex,
                    PurchaseApprovalStoredProcedure,
                    $"Id {id}");

                await NotifyWebApprovalFailureAsync("Issue when fetching purchase approval details", details, ex);
                _loggerManager.LogError(details);

                ErrorPopup($"Inköpsattest kunde inte laddas just nu. Referens: {GetOrCreateSupportId()}.");
                return View();
            }
            catch (Exception ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildExceptionDetails(
                    ex.Message,
                    PurchaseApprovalStoredProcedure,
                    $"Id {id}");

                await NotifyWebApprovalFailureAsync("Issue when fetching Purchase order details", details, ex);

                LogWebApprovalError("PurchaseApprovalDetails failed", ex);
                return RenderModuleUnavailable(
                    "Inköpsattest kunde inte laddas",
                    "Detaljerna för vald inköpsattest kunde inte läsas från den aktiva datakällan.");
            }
        }

        [HttpPost]
        [Route("WebApproval/PurchaseApprovalDetails/{companyId}/{id}")]
        public async Task<IActionResult> PurchaseApprovalDetails([FromForm] WebApprovalPurchaseOrderVM model, string action, Guid id, string? message)
        {
            if (!ModelState.IsValid)
            {
                Error(string.Join(Environment.NewLine, ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)));
                return View(model);
            }

            if (model.ApprovalStatus == 1 || model.ApprovalStatus == 2)
            {
                Error(string.Join(Environment.NewLine, _sharedLocalizer["ApprovePurchase_AlreadyHandled"]));
                return View(model);
            }

            try
            {
                await InitializeAsync();
                await _purchaseRepository.UpdateOrderStatusAsync(SqlConnectionString, id, action, CurrentUserPersSign, message);

                SuccessPopup(_sharedLocalizer["Success_OrderUpdated"]);
                return RedirectToAction("PurchaseApproval");
            }
            catch (SqlException ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildSqlErrorDetails(
                    "updating data in PurchaseApprovalDetails",
                    ex,
                    PurchaseApprovalStoredProcedure,
                    $"Id {id}",
                    $"Status {action}",
                    $"ApprovedBy {_userObject?.PersSign}",
                    $"Message {message}");

                await NotifyWebApprovalFailureAsync("Issue when updating Purchase order details", details, ex);
                _loggerManager.LogError(details);

                ErrorPopup($"Inköpsattesten kunde inte uppdateras just nu. Referens: {GetOrCreateSupportId()}.");
                return View(model);
            }
            catch (Exception ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildExceptionDetails(
                    ex.Message,
                    PurchaseApprovalStoredProcedure,
                    $"Id {id}",
                    $"Status {action}",
                    $"ApprovedBy {_userObject?.PersSign}",
                    $"Message {message}");

                await NotifyWebApprovalFailureAsync("Issue when approving Purchase order", details, ex);

                LogWebApprovalError("PurchaseApprovalDetails update failed", ex);
                ErrorPopup("Inköpsattesten kunde inte uppdateras just nu.");
                return RedirectToAction("PurchaseApproval");
            }
        }
    }
}
