// Handles price list approval listing, detail and row updates for WebApproval.
using Entities.Dto;
using Entities.ViewModels.WebApproval;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using WebApp.Services.WebApproval;

namespace WebApp.Controllers
{
    public partial class WebApprovalController
    {
        [HttpGet]
        public async Task<IActionResult> PriceListApproval()
        {
            try
            {
                await InitializeAsync();

                ViewData["CompanyId"] = _userObject!.CompanyId;

                var data = await _priceListRepository.GetPriceListWithRowsAsync(
                    _sqlConnectionString!,
                    _userObject.JeevesActiveCompany,
                    _userObject.PersSign);

                return View(data);
            }
            catch (InvalidOperationException ex)
            {
                LogWebApprovalError("PriceListApproval init failed", ex);
                await _notificationManager.Warning(_sharedLocalizer["Error_NotAuthorizedForThisResource"]);
                return RedirectToAction("Index", "Home");
            }
            catch (SqlException ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildSqlErrorDetails(
                    "fetching data in PriceListApproval",
                    ex,
                    "q_zu_CustomerPortal_WebApprovalPriceList",
                    $"ForetagKod {_userObject?.JeevesActiveCompany}",
                    $"PersSign2 {_userObject?.PersSign}");

                await NotifyWebApprovalFailureAsync("Issue when fetching pricelists", details, ex);
                _loggerManager.LogError(details);
                ErrorPopup($"Prislistor kunde inte laddas just nu. Referens: {GetOrCreateSupportId()}.");
                return View();
            }
            catch (Exception ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildExceptionDetails(
                    ex.Message,
                    "q_zu_CustomerPortal_WebApprovalPriceList",
                    $"ForetagKod {_userObject?.JeevesActiveCompany}",
                    $"PersSign2 {_userObject?.PersSign}");

                await NotifyWebApprovalFailureAsync("Issue when fetching pricelists", details, ex);
                LogWebApprovalError("PriceListApproval failed", ex);
                ErrorPopup($"Prislistor kunde inte laddas just nu. Referens: {GetOrCreateSupportId()}.");
                return View();
            }
        }

        [HttpGet]
        [Route("WebApproval/PriceListApprovalDetails/{companyId}/{companyCode}/{priceListId}")]
        public async Task<IActionResult> PriceListApprovalDetailsAsync(Guid companyId, int companyCode, int? priceListId)
        {
            try
            {
                await InitializeAsync();

                if (CurrentUser.CompanyId != companyId)
                {
                    await _notificationManager.Warning(_sharedLocalizer["Error_NotAuthorizedForThisResource"]);
                    return RedirectToAction("Index", "Home");
                }

                var data = (await _priceListRepository.GetPriceListWithRowsAsync(
                    SqlConnectionString,
                    companyCode,
                    CurrentUser.PersSign,
                    priceListId)).FirstOrDefault();

                if (data is null)
                    throw new Exception("Could not find the price list");

                ViewData["UserObject"] = await GetCurrentUserAsync();
                return View(data);
            }
            catch (Exception ex) when (ex is ArgumentNullException || ex is NullReferenceException)
            {
                ErrorPopup(_sharedLocalizer["Error_NotAuthorizedForThisResource"]);
                return RedirectToAction("Index", "Home");
            }
            catch (InvalidOperationException ex)
            {
                LogWebApprovalError("PriceListApprovalDetails init failed", ex);
                await _notificationManager.Warning(_sharedLocalizer["Error_NotAuthorizedForThisResource"]);
                return RedirectToAction("Index", "Home");
            }
            catch (SqlException ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildSqlErrorDetails(
                    "fetching data in PriceListApprovalDetails",
                    ex,
                    "q_zu_CustomerPortal_WebApprovalPriceList",
                    $"ForetagKod {_userObject?.JeevesActiveCompany}",
                    $"PersSign2 {_userObject?.PersSign}");

                await NotifyWebApprovalFailureAsync("Issue when fetching pricelists details", details, ex);
                _loggerManager.LogError(details);
                ErrorPopup($"Prislistedata kunde inte laddas just nu. Referens: {GetOrCreateSupportId()}.");
                return View();
            }
            catch (Exception ex)
            {
                var details = WebApprovalErrorDetailsBuilder.BuildExceptionDetails(
                    ex.Message,
                    "q_zu_CustomerPortal_WebApprovalPriceList",
                    $"ForetagKod {_userObject?.JeevesActiveCompany}",
                    $"PersSign2 {_userObject?.PersSign}");

                await NotifyWebApprovalFailureAsync("Issue when fetching pricelist details", details, ex);
                LogWebApprovalError("PriceListApprovalDetails failed", ex);
                ErrorPopup($"Prislistedata kunde inte laddas just nu. Referens: {GetOrCreateSupportId()}.");
                return View();
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApprovePriceList([FromForm] WebApprovalPriceListDto model)
        {
            try
            {
                await InitializeAsync();

                if (!ModelState.IsValid)
                {
                    Error(string.Join(Environment.NewLine, ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)));
                    ViewData["CompanyId"] = _userObject!.CompanyId;
                    ViewData["UserObject"] = await GetCurrentUserAsync();
                    return View("PriceListApprovalDetails", model);
                }

                if (model.Rows.Count(x => x.IsApproved == true) == 0 && model.Rows.Count(x => x.IsRejected == true) == 0)
                {
                    Error(_sharedLocalizer["Error_NoRowsSelected"]);
                    return View("PriceListApprovalDetails", model);
                }

                for (int i = 0; i < model.Rows.Count; i++)
                {
                    if (model.Rows[i].IsApproved == true && model.Rows[i].IsRejected == true)
                    {
                        Error(_sharedLocalizer["Error_RowCannotBeApprovedAndRejected"]);
                        return View("PriceListApprovalDetails", model);
                    }

                    if (model.Rows[i].ApprovalStatus == 1 || model.Rows[i].ApprovalStatus == 2)
                    {
                        Error(string.Join(Environment.NewLine, _sharedLocalizer["Error_RowAlreadyHandled"]));
                        return View("PriceListApprovalDetails", model);
                    }

                    if (model.Rows[i].IsRejected || model.Rows[i].IsApproved)
                    {
                        var action = model.Rows[i].IsApproved == true ? "1" : "2";
                        try
                        {
                            await _priceListRepository.UpdatePriceListStatusAsync(
                                _sqlConnectionString!,
                                model.Rows[i].Id,
                                action,
                                model.Rows[i].Message,
                                _userObject?.PersSign!);
                        }
                        catch (SqlException ex)
                        {
                            var details = WebApprovalErrorDetailsBuilder.BuildSqlErrorDetails(
                                "fetching data in ApprovePriceList",
                                ex,
                                "q_zu_CustomerPortal_WebApprovalPriceList",
                                $"ForetagKod {_userObject?.JeevesActiveCompany}",
                                $"Message {model.Rows[i].Id}",
                                $"Status {action}",
                                $"Message {model.Rows[i].Message}",
                                $"ApprovedBy {_userObject?.PersSign}");

                            await NotifyWebApprovalFailureAsync("Issue when approving Price list row", details, ex);
                            _loggerManager.LogError(details);

                            ErrorPopup($"An error occured when approving article {model.Rows[i].ArticleNumber}");
                            return View();
                        }
                        catch (Exception ex)
                        {
                            var details = WebApprovalErrorDetailsBuilder.BuildExceptionDetails(
                                ex.Message,
                                "q_zu_CustomerPortal_WebApprovalPriceList",
                                $"ForetagKod {_userObject?.JeevesActiveCompany}",
                                $"Message {model.Rows[i].Id}",
                                $"Status {action}",
                                $"Message {model.Rows[i].Message}",
                                $"ApprovedBy {_userObject?.PersSign}");

                            await NotifyWebApprovalFailureAsync("Issue when approving Price list row", details, ex);
                            ErrorPopup($"An error occured when approving article {model.Rows[i].ArticleNumber}");
                        }
                    }
                }

                SuccessPopup(_sharedLocalizer["Success_OrderUpdated"]);
                return RedirectToAction("PriceListApproval");
            }
            catch (InvalidOperationException ex)
            {
                LogWebApprovalError("ApprovePriceList init failed", ex);
                return RenderModuleUnavailable(
                    "Prislistor kunde inte uppdateras",
                    "Portalen fungerar, men prislistedata från den aktiva datakällan kunde inte nås just nu.");
            }
        }
    }
}
