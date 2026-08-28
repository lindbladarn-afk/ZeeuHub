using Entities.Application;
using Microsoft.AspNetCore.Mvc;
using WebApp.Models.Application;
using WebApp.Services;

namespace WebApp.Controllers;

public partial class AdminController
{
    [HttpGet]
    public async Task<IActionResult> EventLogs(
        int daysBack = 7,
        string? module = null,
        string? severity = null,
        Guid? companyId = null,
        string? search = null,
        int latestPage = 1,
        int latestPageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var vm = await _adminEventLogService.GetPortalEventLogsAsync(
            daysBack,
            module,
            severity,
            companyId,
            search,
            latestPage,
            latestPageSize,
            cancellationToken: cancellationToken);

        return View("~/Views/Admin/Events/EventLogs.cshtml", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTestEventLog(
        int daysBack = 7,
        string? module = null,
        string? severity = null,
        Guid? companyId = null,
        string? search = null,
        int latestPage = 1,
        int latestPageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var sessionUser = HttpContext?.Session.Get<UserSession>("UserObject");

        await _portalEventLogService.RecordAsync(
            new PortalEventLogEntry
            {
                OccurredAtUtc = DateTime.UtcNow,
                Module = "Admin",
                Action = "CreateTestEventLog",
                CompanyId = sessionUser?.CompanyId,
                CompanyName = sessionUser?.CompanyName,
                JeevesCompanyCode = sessionUser?.JeevesActiveCompany,
                UserId = sessionUser?.UserId,
                UserEmail = sessionUser?.Email ?? User?.Identity?.Name,
                RequestPath = HttpContext?.Request?.Path.Value,
                CorrelationId = $"admin-test-{Guid.NewGuid():N}",
                Severity = "Error",
                Message = "Manual test event log created from admin UI.",
                AdditionalData = $"Created by admin test button at {DateTime.UtcNow:O}."
            },
            cancellationToken);

        TempData["AdminEventLogsMessageType"] = "success";
        TempData["AdminEventLogsMessage"] = "Testlogg skapad.";

        return RedirectToAction(nameof(EventLogs), new
        {
            daysBack,
            module,
            severity,
            companyId,
            search,
            latestPage,
            latestPageSize
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEventLog(
        Guid id,
        int daysBack = 7,
        string? module = null,
        string? severity = null,
        Guid? companyId = null,
        string? search = null,
        int latestPage = 1,
        int latestPageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _adminEventLogService.DeletePortalEventLogAsync(id, cancellationToken);

        TempData["AdminEventLogsMessageType"] = deleted ? "success" : "info";
        TempData["AdminEventLogsMessage"] = deleted ? "Felloggen raderades." : "Felloggen hittades inte.";

        return RedirectToAction(nameof(EventLogs), new
        {
            daysBack,
            module,
            severity,
            companyId,
            search,
            latestPage,
            latestPageSize
        });
    }
}
