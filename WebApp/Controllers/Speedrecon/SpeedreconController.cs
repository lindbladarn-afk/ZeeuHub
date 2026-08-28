// Owns the Speedrecon module page and controlled hub-side execution.
using Entities.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Helpers;
using WebApp.Services;
using WebApp.Services.Application;
using WebApp.Services.Integration;
using WebApp.Services.Integration.Speedrecon;

namespace WebApp.Controllers;

[Authorize(Roles = "Administrator, User, SuperUser, Dashboard")]
[Route("Integration/[action]")]
[Route("[controller]/[action]")]
public sealed class SpeedreconController : Controller
{
    private static readonly Guid SpeedreconSubModuleId = Guid.Parse("adbc1e55-6f13-4f6b-968d-5b2f7d73b441");

    private readonly ICompanyPermissionGuard _companyPermissionGuard;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly ISpeedreconPageService _pageService;

    public SpeedreconController(
        ICompanyPermissionGuard companyPermissionGuard,
        IHttpContextAccessor contextAccessor,
        ISpeedreconPageService pageService)
    {
        _companyPermissionGuard = companyPermissionGuard;
        _contextAccessor = contextAccessor;
        _pageService = pageService;
    }

    [HttpGet]
    public async Task<IActionResult> Speedrecon(DateTime? reconDate, CancellationToken cancellationToken)
    {
        if (!await HasSpeedreconAccessAsync())
            return Forbid();

        var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
        var model = await _pageService.BuildPageAsync(
            user,
            reconDate,
            TempData["SpeedreconStatusMessage"] as string,
            TempData["SpeedreconStatusTone"] as string,
            cancellationToken);

        return View("~/Views/Integration/Speedrecon/Speedrecon.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SpeedreconRun(DateTime reconDate, CancellationToken cancellationToken)
    {
        if (!await HasSpeedreconAccessAsync())
            return Forbid();

        var safeReconDate = reconDate.Date;
        if (safeReconDate < new DateTime(2000, 1, 1) || safeReconDate > DateTime.Today.AddDays(1))
        {
            TempData["SpeedreconStatusTone"] = "warning";
            TempData["SpeedreconStatusMessage"] = "Avstamningsdatumet ar inte giltigt.";
            return RedirectToAction(nameof(Speedrecon), new { reconDate = safeReconDate.ToString("yyyy-MM-dd") });
        }

        var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
        try
        {
            TempData["SpeedreconStatusMessage"] = await _pageService.RunAsync(user, safeReconDate, cancellationToken);
            TempData["SpeedreconStatusTone"] = "info";
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            TempData["SpeedreconStatusTone"] = "warning";
            TempData["SpeedreconStatusMessage"] = $"Speedrecon kunde inte koras: {IntegrationLogSanitizer.Diagnostic(ex.Message)}";
        }

        return RedirectToAction(nameof(Speedrecon), new { reconDate = safeReconDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SpeedreconCreateYear(int fiscalYear, DateTime reconDate, CancellationToken cancellationToken)
    {
        if (!await HasSpeedreconAccessAsync())
            return Forbid();

        var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
        try
        {
            TempData["SpeedreconStatusMessage"] = await _pageService.CreateYearAsync(user, fiscalYear, cancellationToken);
            TempData["SpeedreconStatusTone"] = "info";
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            TempData["SpeedreconStatusTone"] = "warning";
            TempData["SpeedreconStatusMessage"] = $"Speedrecon kunde inte skapa år: {IntegrationLogSanitizer.Diagnostic(ex.Message)}";
        }

        return RedirectToAction(nameof(Speedrecon), new { reconDate = reconDate.Date.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SpeedreconRunDepreciation(DateTime reconDate, CancellationToken cancellationToken)
    {
        if (!await HasSpeedreconAccessAsync())
            return Forbid();

        var safeReconDate = reconDate.Date;
        var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
        try
        {
            TempData["SpeedreconStatusMessage"] = await _pageService.RunStandaloneDepreciationAsync(user, safeReconDate, cancellationToken);
            TempData["SpeedreconStatusTone"] = "info";
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            TempData["SpeedreconStatusTone"] = "warning";
            TempData["SpeedreconStatusMessage"] = $"Speedrecon kunde inte köra fristående avskrivning: {IntegrationLogSanitizer.Diagnostic(ex.Message)}";
        }

        return RedirectToAction(nameof(Speedrecon), new { reconDate = safeReconDate.ToString("yyyy-MM-dd") });
    }

    private async Task<bool> HasSpeedreconAccessAsync()
    {
        var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
        return user?.CompanyId is Guid companyId
               && companyId != Guid.Empty
               && await _companyPermissionGuard.HasAccessAsync(companyId, SpeedreconSubModuleId);
    }
}
