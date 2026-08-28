// Handles sales approval list, detail and update actions for WebApproval.
using Entities.ViewModels.WebApproval;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using WebApp.Services.WebApproval;

namespace WebApp.Controllers
{
    public partial class WebApprovalController
    {
        private const string SalesApprovalStoredProcedure = "q_zu_CustomerPortal_WebApprovalSales";

        [HttpGet]
        public async Task<IActionResult> SalesApproval()
        {
            try
            {
                await InitializeAsync();
                ViewData["CompanyId"] = CurrentUser.CompanyId;
                var currentPersSign = CurrentUserPersSign;

                var data = await _orderRepository.GetAllSalesAttestOrdersAsync(SqlConnectionString, CurrentUser.JeevesActiveCompany, CurrentUserEmail);
                var myActiveOrders = data
                    .Where(x => x.IsActive == true && x.AttestantPersSign.ToLower() == currentPersSign.ToLower())
                    .ToList();

                return View(myActiveOrders);
            }
            catch (SqlException ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildSqlErrorDetails(
                    "fetching data in SalesOrderApproval",
                    ex,
                    SalesApprovalStoredProcedure,
                    $"ForetagKod {_userObject?.JeevesActiveCompany}",
                    $"EmailAddress {_userObject?.Email}");

                await NotifyWebApprovalFailureAsync("Issue when fetching sales orders", details, ex);
                _loggerManager.LogError(details);

                ErrorPopup($"Försäljningsattester kunde inte laddas just nu. Referens: {GetOrCreateSupportId()}.");
                return View();
            }
            catch (Exception ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildExceptionDetails(
                    ex.Message,
                    SalesApprovalStoredProcedure,
                    $"ForetagKod {_userObject?.JeevesActiveCompany}",
                    $"EmailAddress {_userObject?.Email}");

                await NotifyWebApprovalFailureAsync("Issue when fetching sales orders", details, ex);

                LogWebApprovalError("SalesApproval failed", ex);
                return RenderModuleUnavailable(
                    "Försäljningsattester kunde inte laddas",
                    "Portalen fungerar, men försäljningsattester från den aktiva datakällan kunde inte laddas just nu.");
            }
        }

        [HttpGet]
        [Route("WebApproval/SalesApprovalDetails/{companyId}/{id}")]
        [Route("WebApproval/SalesOrder/{companyId}/{id}/{language}")]
        public async Task<IActionResult> SalesApprovalDetails(Guid companyId, Guid id, string? language = null)
        {
            try
            {
                await InitializeAsync();
                var data = await _orderRepository.GetAttestOrderWithRowsAsync(SqlConnectionString, id);

                if (companyId == Guid.Parse("BF930279-3918-4C46-AF7C-67D57CB29DDC"))
                    ViewBag.CompanyName = "Xvivo";

                if (language != null)
                    ViewData["ExternalSource"] = "True";

                return View(data);
            }
            catch (SqlException ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildSqlErrorDetails(
                    "fetching data in SalesApprovalDetails",
                    ex,
                    SalesApprovalStoredProcedure,
                    $"Id {id}");

                await NotifyWebApprovalFailureAsync("Issue when fetching sales order details", details, ex);
                _loggerManager.LogError(details);

                ErrorPopup($"Försäljningsattest kunde inte laddas just nu. Referens: {GetOrCreateSupportId()}.");
                return View();
            }
            catch (Exception ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildExceptionDetails(
                    $"Error when fetching data in SalesApprovalDetails: {ex.Message}",
                    SalesApprovalStoredProcedure,
                    $"CompanyId {companyId}",
                    $"Id {id}");

                await NotifyWebApprovalFailureAsync("Issue when fetching Sales order details", details, ex);

                LogWebApprovalError("SalesApprovalDetails failed", ex);
                return RenderModuleUnavailable(
                    "Försäljningsattest kunde inte laddas",
                    "Detaljerna för vald försäljningsattest kunde inte läsas från den aktiva datakällan.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("WebApproval/SalesApprovalDetails/{companyId}/{id}")]
        [Route("WebApproval/SalesOrder/{companyId}/{id}/{language}")]
        public async Task<IActionResult> SalesApprovalDetails([FromForm] WebApprovalSaleOrderVM model, string action, Guid Id, string? message, string? language = null)
        {
            try
            {
                await InitializeAsync();
                if (!ModelState.IsValid)
                {
                    Error(string.Join(Environment.NewLine, ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)));
                    return View();
                }

                if (model.ApprovalStatus == 1 || model.ApprovalStatus == 2)
                {
                    Error(string.Join(Environment.NewLine, _sharedLocalizer["ApproveSales_AlreadyHandled"]));
                    return View(model);
                }

                await _orderRepository.UpdateAttestOrderStatusAsync(SqlConnectionString, Id, action, message, CurrentUserPersSign);

                if (language != null)
                    return RedirectToAction("ThankYou");

                SuccessPopup(_sharedLocalizer["Success_OrderUpdated"]);
                return RedirectToAction("SalesApproval");
            }
            catch (SqlException ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildSqlErrorDetails(
                    "updating SalesApprovalDetails",
                    ex,
                    SalesApprovalStoredProcedure,
                    $"Id {Id}",
                    $"Status {action}",
                    $"ApprovedBy {_userObject?.PersSign}",
                    $"Message {message}");

                await NotifyWebApprovalFailureAsync("Issue when updating Sales approval details", details, ex);
                _loggerManager.LogError(details);

                ErrorPopup($"Försäljningsattesten kunde inte uppdateras just nu. Referens: {GetOrCreateSupportId()}.");
                return View();
            }
            catch (Exception ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildExceptionDetails(
                    ex.Message,
                    SalesApprovalStoredProcedure,
                    $"Id {Id}",
                    $"Status {action}",
                    $"ApprovedBy {_userObject?.PersSign}",
                    $"Message {message}");

                await NotifyWebApprovalFailureAsync("Issue when approving Sales order", details, ex);

                LogWebApprovalError("SalesApprovalDetails update failed", ex);
                ErrorPopup(_sharedLocalizer["Error_OrderUpdateFail"]);
                return View(model);
            }
        }
    }
}
