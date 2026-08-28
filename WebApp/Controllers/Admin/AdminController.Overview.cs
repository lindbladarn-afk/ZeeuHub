using Entities.Application;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading;
using WebApp.Models.ActionCenter;
using WebApp.Services;
using WebApp.ViewModels.Admin;

namespace WebApp.Controllers;

// This file contains overview and telemetry endpoints for the admin area.
// It keeps dashboard-style pages separated from company, user, and AI administration.
public partial class AdminController
{
    public async Task<IActionResult> AdminOverview()
    {
        var vm = await _adminOverviewService.GetOverviewAsync();

        return View("~/Views/Admin/Overview/AdminOverview.cshtml", vm);
    }

    [HttpGet]
    public async Task<IActionResult> InternalOperationsCard()
    {
        var vm = new AdminOverviewViewModel();
        var sessionUser = HttpContext?.Session.Get<UserSession>("UserObject");

        if (sessionUser != null)
        {
            using var operationsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var operationsVm = await _zeeuOperationsService.GetInsightsAsync(
                sessionUser,
                take: 5,
                operationsTimeout.Token);

            vm.InternalOperationsCount = operationsVm.TotalCount;
            vm.InternalOperationsHighPriorityCount = operationsVm.Insights.Count(x => x.Priority == ActionCenterPriority.High);
            vm.InternalOperationsDegraded = operationsVm.IsDegraded;
            vm.InternalOperations = operationsVm.Insights.Select(MapInternalOperationsItem).ToList();
            vm.InternalOperationsProviderFailures = operationsVm.ProviderFailures
                .Select(MapInternalOperationsFailure)
                .ToList();
            vm.InternalOperationsProviderFailureCount = vm.InternalOperationsProviderFailures.Count;
        }

        return PartialView("~/Views/Admin/Partials/Overview/_ZeeuOperationsCard.cshtml", vm);
    }

    [HttpGet]
    public async Task<IActionResult> HealthStatus()
    {
        var statuses = await _adminOverviewService.GetHealthAsync();
        return Json(statuses);
    }

    [HttpGet]
    public async Task<IActionResult> HealthDetail()
    {
        var vm = await _adminOverviewService.GetCompanyConnectionHealthAsync();
        return View("~/Views/Admin/Diagnostics/HealthDetail.cshtml", vm);
    }

    [HttpGet]
    public async Task<IActionResult> PortalSessions()
    {
        var vm = await _telemetryService.GetPortalSessionsAsync();
        return View("~/Views/Admin/Telemetry/PortalSessions.cshtml", vm);
    }

    [HttpGet]
    public async Task<IActionResult> ExcelImports()
    {
        var vm = await _telemetryService.GetExcelImportsAsync();
        return View("~/Views/Admin/Telemetry/ExcelImports.cshtml", vm);
    }

    private static AdminOverviewViewModel.InternalOperationsItem MapInternalOperationsItem(ActionCenterInsight insight)
    {
        var (priorityLabel, priorityCssClass) = insight.Priority switch
        {
            ActionCenterPriority.High => ("Hög", "badge bg-danger-subtle text-danger-emphasis"),
            ActionCenterPriority.Medium => ("Medel", "badge bg-warning-subtle text-warning-emphasis"),
            ActionCenterPriority.Low => ("Låg", "badge bg-info-subtle text-info-emphasis"),
            _ => ("Info", "badge bg-secondary-subtle text-secondary-emphasis")
        };

        return new AdminOverviewViewModel.InternalOperationsItem
        {
            Key = insight.Key,
            Title = insight.Title,
            Description = insight.Description,
            Category = insight.Category,
            PriorityLabel = priorityLabel,
            PriorityCssClass = priorityCssClass,
            DetectedAt = insight.DetectedAt,
            LinkText = insight.LinkText,
            LinkUrl = insight.LinkUrl
        };
    }

    private static AdminOverviewViewModel.InternalOperationsProviderFailure MapInternalOperationsFailure(ActionCenterProviderFailure failure)
    {
        return new AdminOverviewViewModel.InternalOperationsProviderFailure
        {
            ProviderKey = failure.ProviderKey,
            DisplayName = failure.ProviderKey switch
            {
                "internal-ai-query-failures" => "AI-signaler",
                "internal-excel-imports" => "Excelimporter",
                "internal-platform-health" => "Driftstatus",
                "internal-ai-quota" => "AI-kvot",
                _ => failure.ProviderKey
            },
            Message = failure.Message,
            OccurredAtUtc = failure.OccurredAtUtc
        };
    }
}
