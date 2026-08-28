using Microsoft.AspNetCore.Mvc;
using Entities.ViewModels.Admin;
using WebApp.ViewModels.Admin;

namespace WebApp.Controllers;

// This file owns company administration inside the admin area.
// It contains company CRUD, permission changes, and connection validation helpers.
public partial class AdminController
{
    [HttpGet]
    public async Task<IActionResult> Companies()
    {
        var companies = await _adminCompanyManagementService.GetCompaniesAsync();
        return View("~/Views/Admin/Companies/Companies.cshtml", companies);
    }

    [HttpGet]
    public IActionResult CreateCompany()
    {
        var model = _adminCompanyManagementService.BuildCreateCompanyViewModel();
        return View("~/Views/Admin/Companies/CreateCompany.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCompany(AdminCreateCompanyViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _adminCompanyManagementService.CreateCompanyAsync(model);
        if (result.ShouldReturnView)
            return View("~/Views/Admin/Companies/CreateCompany.cshtml", result.Model);

        if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
            await _notificationManager.Success(result.SuccessMessage);

        return RedirectToAction(nameof(ManageCompany), new { companyId = result.CreatedCompanyId });
    }

    [HttpGet]
    public async Task<IActionResult> ManageCompany(Guid companyId)
    {
        var company = await _adminCompanyManagementService.GetManageCompanyAsync(companyId);
        return View("~/Views/Admin/Companies/ManageCompany.cshtml", company);
    }

    [HttpGet]
    public async Task<IActionResult> TestCompanyConnection(Guid companyId, Guid connectionStringId)
    {
        if (companyId == Guid.Empty || connectionStringId == Guid.Empty)
            return BadRequest(new { success = false, message = "Missing companyId or connectionStringId." });

        var result = await _adminCompanyManagementService.TestCompanyConnectionAsync(companyId, connectionStringId);
        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetJeevesCompanies([FromForm] Guid companyId, [FromForm] Guid connectionStringId, [FromForm] string persSign)
    {
        if (companyId == Guid.Empty || connectionStringId == Guid.Empty)
            return BadRequest(new { success = false, message = "Missing companyId or connectionStringId." });

        if (string.IsNullOrWhiteSpace(persSign))
            return BadRequest(new { success = false, message = "PersSign is required." });

        var result = await _adminCompanyManagementService.GetJeevesCompaniesAsync(companyId, connectionStringId, persSign);
        return Json(new { success = result.Success, message = result.Message, items = result.Items });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageCompany([FromForm] ManageCompanyVM model)
    {
        var result = await _adminCompanyManagementService.UpdateCompanyAsync(model);
        foreach (var error in result.ErrorMessages)
            await _notificationManager.Error(error);
        foreach (var success in result.SuccessMessages)
            await _notificationManager.Success(success);

        var currentUser = await GetCurrentUserAsync();
        if (currentUser.CompanyId == model.Id)
        {
            await RefreshCurrentSessionAsync();
        }

        if (result.RedirectToCompanies)
            return RedirectToAction(nameof(Companies));

        return View("~/Views/Admin/Companies/ManageCompany.cshtml", result.Model ?? model);
    }
}
