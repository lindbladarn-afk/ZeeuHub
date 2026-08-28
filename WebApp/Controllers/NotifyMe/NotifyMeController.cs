// Handles the NotifyMe portal pages and editor flow for the active company context.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using NotificationService;
using Repository.Contracts;
using System.ComponentModel.DataAnnotations;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Services;
using WebApp.Services.NotifyMe;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Controllers;

[Authorize(Roles = "Administrator, User, SuperUser")]
public class NotifyMeController : BaseController
{
    private const string LibraryViewPath = "~/Views/NotifyMe/Library.cshtml";

    private readonly INotifyMeService _notifyMeService;
    private readonly INotifyMeDemoService _notifyMeDemoService;
    private readonly IStringLocalizer<SharedResources> _sharedLocalizer;

    public NotifyMeController(
        IHttpContextAccessor contextAccessor,
        IApplicationUserRepository applicationUserRepository,
        INotificationManager notificationManager,
        INotifyMeService notifyMeService,
        INotifyMeDemoService notifyMeDemoService,
        IStringLocalizer<SharedResources> sharedLocalizer,
        IApplicationHelper applicationHelper,
        ApplicationDbContext context)
        : base(contextAccessor, applicationUserRepository, notificationManager, applicationHelper, context)
    {
        _notifyMeService = notifyMeService;
        _notifyMeDemoService = notifyMeDemoService;
        _sharedLocalizer = sharedLocalizer;
    }

    [HttpGet("/NotifyMe")]
    public async Task<IActionResult> NotifyMe(
        CancellationToken cancellationToken = default)
    {
        var runtimeContext = await ResolveCurrentRuntimeContextAsync(cancellationToken);
        if (runtimeContext is null)
            return Forbid();

        var model = await _notifyMeService.GetOverviewAsync(
            runtimeContext.ConnectionString,
            runtimeContext.CompanyCode,
            cancellationToken: cancellationToken);

        return View(model);
    }

    public async Task<IActionResult> Notifications(
        string? search,
        string? status,
        string? type,
        string? priority,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var runtimeContext = await ResolveCurrentRuntimeContextAsync(cancellationToken);
        if (runtimeContext is null)
            return Forbid();

        var model = await _notifyMeService.GetOverviewAsync(
            runtimeContext.ConnectionString,
            runtimeContext.CompanyCode,
            search,
            status,
            type,
            priority,
            page,
            cancellationToken: cancellationToken);

        return View("Notifications", model);
    }

    public async Task<IActionResult> Library(
        string? search,
        string? category,
        CancellationToken cancellationToken = default)
    {
        var runtimeContext = await ResolveCurrentRuntimeContextAsync(cancellationToken);
        if (runtimeContext is null)
            return Forbid();

        var model = await _notifyMeService.GetTemplateLibraryAsync(
            runtimeContext.ConnectionString,
            runtimeContext.CompanyCode,
            search,
            category,
            cancellationToken: cancellationToken);

        return View(LibraryViewPath, model);
    }

    public async Task<IActionResult> Statistics(CancellationToken cancellationToken)
    {
        var runtimeContext = await ResolveCurrentRuntimeContextAsync(cancellationToken);
        if (runtimeContext is null)
            return Forbid();

        var model = await _notifyMeService.GetStatisticsAsync(
            runtimeContext.ConnectionString,
            runtimeContext.CompanyCode,
            cancellationToken);

        return View(model);
    }

    public async Task<IActionResult> History(
        int? historyNotificationId,
        string? historySearch,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var runtimeContext = await ResolveCurrentRuntimeContextAsync(cancellationToken);
        if (runtimeContext is null)
            return Forbid();

        var model = await _notifyMeService.GetHistoryAsync(
            runtimeContext.ConnectionString,
            runtimeContext.CompanyCode,
            historyNotificationId,
            historySearch,
            page,
            cancellationToken);

        return View(model);
    }

    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var runtimeContext = await ResolveCurrentRuntimeContextAsync(cancellationToken);
        if (runtimeContext is null)
            return Forbid();

        var model = await _notifyMeService.GetDetailsAsync(
            runtimeContext.ConnectionString,
            runtimeContext.CompanyCode,
            id,
            cancellationToken);
        model.DefaultTestRecipientEmail = runtimeContext.Email;

        return View(model);
    }

    public Task<IActionResult> CreateNew(int? id, string? templateKey, CancellationToken cancellationToken)
    {
        return Task.FromResult<IActionResult>(RedirectToAction(nameof(Editor), new { id, templateKey }));
    }

    public async Task<IActionResult> Editor(int? id, string? templateKey, CancellationToken cancellationToken)
    {
        var runtimeContext = await ResolveCurrentRuntimeContextAsync(cancellationToken);
        if (runtimeContext is null)
            return Forbid();

        var model = await _notifyMeService.GetCreatePrototypeAsync(
            runtimeContext.ConnectionString,
            runtimeContext.CompanyCode,
            id,
            cancellationToken);

        if (!model.IsEditMode && !string.IsNullOrWhiteSpace(templateKey))
        {
            var template = _notifyMeDemoService.GetTemplate(templateKey);
            if (template != null)
            {
                model.TemplateKey = template.Key;
                model.TemplateName = template.Title;
                model.StatusMessage = _sharedLocalizer["NotifyMe_TemplateLoadedAsBase", template.Title];
                model.Draft = new WebApp.ViewModels.NotifyMe.NotifyMeDraftVm
                {
                    Description = template.Draft.Description,
                    WarningText = template.Draft.WarningText,
                    Comment = template.Draft.Comment,
                    TypeCode = template.Draft.TypeCode,
                    PriorityCode = template.Draft.PriorityCode,
                    PrimaryEmail = template.Draft.PrimaryEmail,
                    SecondaryEmail = template.Draft.SecondaryEmail,
                    Cc = template.Draft.Cc,
                    Bcc = template.Draft.Bcc,
                    SchemaCode = template.Draft.SchemaCode,
                    ScheduleCode = template.Draft.ScheduleCode,
                    StartDate = template.Draft.StartDate,
                    EscalateAfterCount = template.Draft.EscalateAfterCount,
                    EscalationEmail = template.Draft.EscalationEmail,
                    SqlPreview = template.Draft.SqlPreview
                };
            }
        }

        // The editor route is intentionally neutral, but it still renders the existing CreateNew view.
        return View("CreateNew", model);
    }

    [HttpPost]
    [ActionName("Editor")]
    [Authorize(Roles = "Administrator,SuperUser")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEditor(NotifyMeDraftVm draft, CancellationToken cancellationToken)
    {
        var runtimeContext = await ResolveCurrentRuntimeContextAsync(cancellationToken);
        if (runtimeContext is null)
            return Forbid();

        ValidateDraft(draft, ModelState);

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildEditorModelAsync(runtimeContext.ConnectionString, runtimeContext.CompanyCode, draft, cancellationToken);
            invalidModel.StatusMessage = _sharedLocalizer["NotifyMe_CheckHighlightedFields"];
            return View("CreateNew", invalidModel);
        }

        try
        {
            var notificationId = await _notifyMeService.SaveNotificationAsync(
                runtimeContext.ConnectionString,
                runtimeContext.CompanyCode,
                draft,
                User?.Identity?.Name ?? "PORTAL",
                cancellationToken);

            TempData["NotifyMeEditorLevel"] = "success";
            TempData["NotifyMeEditorMessage"] = draft.NotificationId.HasValue
                ? _sharedLocalizer["NotifyMe_NotificationUpdated", notificationId].Value
                : _sharedLocalizer["NotifyMe_NotificationCreated", notificationId].Value;

            return RedirectToAction(nameof(Detail), new { id = notificationId });
        }
        catch (Exception)
        {
            var failedModel = await BuildEditorModelAsync(runtimeContext.ConnectionString, runtimeContext.CompanyCode, draft, cancellationToken);
            failedModel.StatusMessage = _sharedLocalizer["NotifyMe_CouldNotSaveNotification", "ett tekniskt fel"].Value;
            return View("CreateNew", failedModel);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,SuperUser")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestRun(int id, string overrideRecipient, CancellationToken cancellationToken)
    {
        var runtimeContext = await ResolveCurrentRuntimeContextAsync(cancellationToken);
        if (runtimeContext is null)
        {
            TempData["NotifyMeTestRunLevel"] = "danger";
            TempData["NotifyMeTestRunMessage"] = "Testkörning kräver en aktiv Jeeves-koppling. Försök igen när tenantdatat är tillgängligt.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        try
        {
            var result = await _notifyMeService.RunTestNotificationAsync(
                runtimeContext.ConnectionString,
                runtimeContext.CompanyCode,
                id,
                overrideRecipient,
                cancellationToken);

            if (result.MailQueued)
            {
                TempData["NotifyMeTestRunLevel"] = "success";
                TempData["NotifyMeTestRunMessage"] =
                    _sharedLocalizer["NotifyMe_TestRunQueued", result.NotificationId, result.OverrideRecipient].Value +
                    $"{(result.MailItemId.HasValue ? $" {_sharedLocalizer["NotifyMe_MailItemIdLabel"]}: {result.MailItemId.Value}." : string.Empty)}" +
                    $"{(!string.IsNullOrWhiteSpace(result.MailStatus) ? $" {_sharedLocalizer["Status"]}: {result.MailStatus}." : string.Empty)}";
            }
            else
            {
                TempData["NotifyMeTestRunLevel"] = "warning";
                TempData["NotifyMeTestRunMessage"] =
                    _sharedLocalizer["NotifyMe_TestRunNoMailVerified", result.NotificationId, result.OverrideRecipient].Value;
            }
        }
        catch (Exception)
        {
            TempData["NotifyMeTestRunLevel"] = "danger";
            TempData["NotifyMeTestRunMessage"] = _sharedLocalizer["NotifyMe_TestRunFailed", "ett tekniskt fel"].Value;
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    private async Task<NotifyMeCreatePrototypeVm> BuildEditorModelAsync(
        string? connectionString,
        int? companyCode,
        NotifyMeDraftVm draft,
        CancellationToken cancellationToken)
    {
        var model = await _notifyMeService.GetCreatePrototypeAsync(
            connectionString,
            companyCode,
            draft.NotificationId,
            cancellationToken);

        model.Draft = draft;
        model.IsEditMode = draft.NotificationId.HasValue;
        model.NotificationId = draft.NotificationId;
        return model;
    }

    private void ValidateDraft(NotifyMeDraftVm draft, ModelStateDictionary modelState)
    {
        if (string.IsNullOrWhiteSpace(draft.Description))
            modelState.AddModelError(nameof(draft.Description), "DescriptionRequired");

        if (string.IsNullOrWhiteSpace(draft.WarningText))
            modelState.AddModelError(nameof(draft.WarningText), "WarningTextRequired");

        if (string.IsNullOrWhiteSpace(draft.TypeCode))
            modelState.AddModelError(nameof(draft.TypeCode), "TypeRequired");

        if (string.IsNullOrWhiteSpace(draft.PriorityCode))
            modelState.AddModelError(nameof(draft.PriorityCode), "PriorityRequired");

        if (string.IsNullOrWhiteSpace(draft.SchemaCode))
            modelState.AddModelError(nameof(draft.SchemaCode), "ScheduleSchemaRequired");

        if (string.IsNullOrWhiteSpace(draft.ScheduleCode))
            modelState.AddModelError(nameof(draft.ScheduleCode), "FrequencyRequired");

        if (!draft.StartDate.HasValue)
            modelState.AddModelError(nameof(draft.StartDate), "StartDateRequired");

        if (string.IsNullOrWhiteSpace(draft.SqlPreview))
            modelState.AddModelError(nameof(draft.SqlPreview), "SqlSourceRequired");

        if (draft.EscalateAfterCount.HasValue && draft.EscalateAfterCount.Value < 0)
            modelState.AddModelError(nameof(draft.EscalateAfterCount), "EscalationCannotBeNegative");

        if (draft.IsActive &&
            !draft.UsesDynamicRecipients &&
            string.IsNullOrWhiteSpace(draft.PrimaryEmail) &&
            string.IsNullOrWhiteSpace(draft.SecondaryEmail))
        {
            modelState.AddModelError(nameof(draft.PrimaryEmail), "AtLeastOneRecipientRequiredForActiveNotification");
        }

        ValidateSingleEmail(draft.PrimaryEmail, nameof(draft.PrimaryEmail), _sharedLocalizer["NotifyMe_PrimaryRecipient"]);
        ValidateSingleEmail(draft.SecondaryEmail, nameof(draft.SecondaryEmail), _sharedLocalizer["NotifyMe_SecondaryRecipient"]);
        ValidateSingleEmail(draft.EscalationEmail, nameof(draft.EscalationEmail), _sharedLocalizer["NotifyMe_EscalationEmail"]);
        ValidateRecipientList(draft.Cc, nameof(draft.Cc), _sharedLocalizer["NotifyMe_Cc"]);
        ValidateRecipientList(draft.Bcc, nameof(draft.Bcc), _sharedLocalizer["NotifyMe_BccEscalation"]);

        if (draft.EscalateAfterCount.GetValueOrDefault() > 0 && string.IsNullOrWhiteSpace(draft.EscalationEmail))
            modelState.AddModelError(nameof(draft.EscalationEmail), "EscalationEmailRequiredWhenEscalationEnabled");

        return;

        static bool IsValidEmail(string value)
        {
            var validator = new EmailAddressAttribute();
            return validator.IsValid(value);
        }

        void ValidateSingleEmail(string? value, string key, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (!IsValidEmail(value.Trim()))
                modelState.AddModelError(key, _sharedLocalizer["FieldMustBeValidEmail", label]);
        }

        void ValidateRecipientList(string? value, string key, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var invalidRecipients = value
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !IsValidEmail(x))
                .ToArray();

            if (invalidRecipients.Length > 0)
                modelState.AddModelError(key, _sharedLocalizer["FieldContainsInvalidEmails", label, string.Join(", ", invalidRecipients)]);
        }
    }
}
