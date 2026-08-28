// Handles the Action Center views and status updates.
using System;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApp.Models.ActionCenter;
using WebApp.Services;
using WebApp.Services.ActionCenter;

namespace WebApp.Controllers;

[Authorize]
[Route("[controller]/[action]")]
public class ActionCenterController : Controller
{
    private const string GenericUpdateFailureMessage = "Kunde inte uppdatera status just nu.";

    private readonly IActionCenterStateStore _stateStore;
    private readonly IActionCenterService _actionCenterService;
    private readonly ILogger<ActionCenterController> _logger;

    public ActionCenterController(
        IActionCenterStateStore stateStore,
        IActionCenterService actionCenterService,
        ILogger<ActionCenterController> logger)
    {
        _stateStore = stateStore;
        _actionCenterService = actionCenterService;
        _logger = logger;
    }

    [HttpGet("/ActionCenter")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var user = HttpContext?.Session.Get<UserSession>("UserObject");
        if (user == null)
        {
            return Challenge();
        }

        var model = await _actionCenterService.GetInsightsAsync(user, 50, cancellationToken);
        return View("~/Views/ActionCenter/Index.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var user = HttpContext?.Session.Get<UserSession>("UserObject");
        if (user == null)
        {
            return Unauthorized();
        }

        var summary = await _actionCenterService.GetSummaryAsync(user, cancellationToken);
        return Ok(new
        {
            count = summary.Count,
            hasHighPriority = summary.HasHighPriority,
            isDegraded = summary.IsDegraded,
            latestDetectedAt = summary.LatestDetectedAt,
            hasNew = summary.Count > 0
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Consumes("application/json")]
    public async Task<IActionResult> UpdateStatus([FromBody] ActionCenterUpdateRequest? request, CancellationToken cancellationToken)
    {
        var user = HttpContext?.Session.Get<UserSession>("UserObject");
        if (user == null || string.IsNullOrWhiteSpace(user.UserId))
        {
            return Unauthorized();
        }

        try
        {
            if (!TryValidateUpdateRequest(request, out var status, out var validationMessage))
            {
                return Ok(new { success = false, message = validationMessage });
            }

            request!.DetectedAt ??= DateTime.UtcNow;

            await _stateStore.UpsertAsync(request.InsightId, status, user.CompanyId, user.UserId, request, cancellationToken);
            _actionCenterService.InvalidateCache(user);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update Action Center status for user {UserId} in company {CompanyId} and insight {InsightId} with status {Status}",
                user.UserId,
                user.CompanyId,
                request?.InsightId,
                request?.Status);

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = GenericUpdateFailureMessage
            });
        }
    }

    private static bool TryValidateUpdateRequest(
        ActionCenterUpdateRequest? request,
        out ActionCenterItemStatus status,
        out string message)
    {
        status = ActionCenterItemStatus.Active;
        message = string.Empty;

        if (request == null)
        {
            message = "Payload saknas.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.InsightId))
        {
            message = "InsightId saknas i payload.";
            return false;
        }

        if (request.InsightId.Length > ActionCenterUpdateRequest.MaxInsightIdLength)
        {
            message = "InsightId är för långt.";
            return false;
        }

        if (!Enum.TryParse(request.Status, true, out status) || !Enum.IsDefined(status))
        {
            message = "Ogiltig status.";
            return false;
        }

        if (request.Priority.HasValue && !Enum.IsDefined(request.Priority.Value))
        {
            message = "Ogiltig prioritet.";
            return false;
        }

        if (IsTooLong(request.Comment, ActionCenterUpdateRequest.MaxCommentLength)
            || IsTooLong(request.Title, ActionCenterUpdateRequest.MaxTitleLength)
            || IsTooLong(request.Description, ActionCenterUpdateRequest.MaxDescriptionLength)
            || IsTooLong(request.Category, ActionCenterUpdateRequest.MaxCategoryLength))
        {
            message = "Payload innehåller för lång text.";
            return false;
        }

        return true;
    }

    private static bool IsTooLong(string? value, int maxLength)
        => !string.IsNullOrEmpty(value) && value.Length > maxLength;
}
