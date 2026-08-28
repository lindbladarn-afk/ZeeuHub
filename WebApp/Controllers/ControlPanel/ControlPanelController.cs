// Handles administrator access and feature selection for the control panel.
using Entities.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Filters;
using WebApp.Models.ControlPanel;
using WebApp.Services;
using WebApp.Services.Application;

namespace WebApp.Controllers;

[Authorize(Roles = "Administrator", Policy = ControlPanelPolicies.Access)]
[ServiceFilter(typeof(TenantValidationFilter))]
public class ControlPanelController : Controller
{
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly IFeatureAccessService _featureAccessService;
    private readonly IJeevesCompanyAccessService _jeevesCompanyAccessService;

    public ControlPanelController(
        IHttpContextAccessor contextAccessor,
        IFeatureAccessService featureAccessService,
        IJeevesCompanyAccessService jeevesCompanyAccessService)
    {
        _contextAccessor = contextAccessor;
        _featureAccessService = featureAccessService;
        _jeevesCompanyAccessService = jeevesCompanyAccessService;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> FeatureAccess()
    {
        var sessionUser = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
        var companies = await _jeevesCompanyAccessService.GetCompaniesAsync(sessionUser);
        var selections = _featureAccessService.GetSelections(HttpContext.Session)
            .ToDictionary(x => x.CompanyCode, x => x);
        var model = new FeatureAccessViewModel
        {
            Items = companies.Select(c => new FeatureAccessItem
            {
                CompanyCode = c.CompanyCode,
                CompanyName = c.Name,
                InvoicesEnabled = !selections.TryGetValue(c.CompanyCode, out var s) || s.InvoicesEnabled,
                OrdersEnabled = !selections.TryGetValue(c.CompanyCode, out var s2) || s2.OrdersEnabled,
                AiEnabled = !selections.TryGetValue(c.CompanyCode, out var s3) || s3.AiEnabled,
                ExcelImportEnabled = !selections.TryGetValue(c.CompanyCode, out var s5) || s5.ExcelImportEnabled,
                DashboardEnabled = !selections.TryGetValue(c.CompanyCode, out var s4) || s4.DashboardEnabled
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult FeatureAccess(FeatureAccessViewModel model)
    {
        var selections = (model?.Items ?? new List<FeatureAccessItem>())
            .Select(i => new FeatureAccessSelection
            {
                CompanyCode = i.CompanyCode,
                InvoicesEnabled = i.InvoicesEnabled,
                OrdersEnabled = i.OrdersEnabled,
                AiEnabled = i.AiEnabled,
                ExcelImportEnabled = i.ExcelImportEnabled,
                DashboardEnabled = i.DashboardEnabled
            });

        _featureAccessService.SaveSelections(HttpContext.Session, selections);
        TempData["FeatureAccessSaved"] = true;
        return RedirectToAction(nameof(FeatureAccess));
    }

    public IActionResult UserCompanyAccess()
    {
        return View();
    }

    public IActionResult AuditLog()
    {
        return View();
    }
}
