// Handles Document Signing pages, status polling and protected commands on existing integration routes.
using Entities.Application;
using Microsoft.AspNetCore.Mvc;
using WebApp.Models;
using WebApp.Models.Application;
using WebApp.Models.DocumentSigning;
using WebApp.Models.Integration;
using WebApp.Models.Orders;
using WebApp.Services;
using WebApp.Services.Application;
using WebApp.Services.DocumentSigning;
using WebApp.Services.Orders;

namespace WebApp.Controllers
{
    public partial class IntegrationController
    {
        private const long MaxPdfBytes = 15 * 1024 * 1024;
        private const int MaxAttachmentCount = 10;
        private readonly IOrdersService _ordersService;
        private readonly IDocumentSigningService _documentSigningService;
        private readonly ISidebarRuntimeStatusService _sidebarRuntimeStatusService;

        [HttpGet]
        public async Task<IActionResult> DocumentSigning(long? orderNo, Guid? selectedSigningId, bool loadOneflowWorkspaces = false, int? loadOneflowTemplatesWorkspaceId = null, CancellationToken cancellationToken = default)
        {
            if (!await HasCompanyPermissionAsync(PortalModuleIds.DocumentSigningSubModule))
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is null)
                return Forbid();

            var model = new DocumentSigningIntegrationViewModel
            {
                IsConfigured = _documentSigningService.IsEnabledForCompany(user.CompanyId.Value),
                SelectedOrderNo = orderNo,
                SelectedSigningId = selectedSigningId,
                LookupWorkspaceId = loadOneflowTemplatesWorkspaceId
            };

            if (loadOneflowWorkspaces || loadOneflowTemplatesWorkspaceId.HasValue)
            {
                try
                {
                    if (loadOneflowWorkspaces || loadOneflowTemplatesWorkspaceId.HasValue)
                        model.OneflowWorkspaces = await _documentSigningService.ListWorkspacesAsync(user.CompanyId.Value, cancellationToken);

                    if (loadOneflowTemplatesWorkspaceId.HasValue)
                    {
                        model.LookupWorkspaceId = loadOneflowTemplatesWorkspaceId;
                        model.OneflowTemplates = await _documentSigningService.ListTemplatesAsync(user.CompanyId.Value, loadOneflowTemplatesWorkspaceId, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    model.OneflowLookupError = GetSafeIntegrationFailureMessage(ex, "Oneflow-data kunde inte laddas just nu");
                }
            }

            var runtimeContext = await ResolveRuntimeContextAsync(user, cancellationToken);
            if (!runtimeContext.Success || runtimeContext.Value is null)
            {
                model.IsRuntimeAvailable = false;
                model.RuntimeUnavailableReason = runtimeContext.Error;
            }

            if (orderNo.HasValue && runtimeContext.Value is not null)
            {
                var order = await _ordersService.GetOrderDetailsAsync(
                    runtimeContext.Value.ConnectionString,
                    new GetOrderDetailsQuery
                    {
                        OrderNo = orderNo.Value,
                        CompanyCode = runtimeContext.Value.CompanyCode,
                        CompanyId = user.CompanyId
                    });
                if (order?.Header != null)
                {
                    model.OrderExists = true;
                    model.SelectedOrderCustomerName = order.Header.CustomerName;
                    model.Signings = order.DocumentSignings;
                    model.DocumentSigningForm = order.DocumentSigningForm;
                }
            }

            if (!model.OrderExists)
            {
                model.Signings = await _documentSigningService.ListRecentAsync(user.CompanyId.Value, user.JeevesActiveCompany, 20, cancellationToken);

                if (orderNo.HasValue)
                {
                    model.DocumentSigningForm = new OrderDocumentSigningFormViewModel
                    {
                        RelatedOrderNo = orderNo.Value,
                        DocumentTitle = $"Offert {orderNo.Value}"
                    };
                }
            }

            if (model.Signings.Count > 0)
            {
                model.SelectedSigning = model.Signings.FirstOrDefault(x => x.Id == selectedSigningId) ?? model.Signings.FirstOrDefault();
                model.SelectedSigningId = model.SelectedSigning?.Id;
            }

            return View("~/Views/Integration/DocumentSigning/DocumentSigning.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> DocumentSigningStatusSnapshot(long? orderNo, Guid? selectedSigningId, CancellationToken cancellationToken = default)
        {
            if (!await HasCompanyPermissionAsync(PortalModuleIds.DocumentSigningSubModule))
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is null)
                return Forbid();

            IReadOnlyList<DocumentSigningListItem> signings;
            var runtimeContext = await ResolveRuntimeContextAsync(user, cancellationToken);

            if (orderNo.HasValue && runtimeContext.Success && runtimeContext.Value is not null)
            {
                var order = await _ordersService.GetOrderDetailsAsync(
                    runtimeContext.Value.ConnectionString,
                    new GetOrderDetailsQuery
                    {
                        OrderNo = orderNo.Value,
                        CompanyCode = runtimeContext.Value.CompanyCode,
                        CompanyId = user.CompanyId
                    });

                signings = order?.Header != null
                    ? order.DocumentSignings
                    : await _documentSigningService.ListRecentAsync(user.CompanyId.Value, user.JeevesActiveCompany, 20, cancellationToken);
            }
            else
            {
                signings = await _documentSigningService.ListRecentAsync(user.CompanyId.Value, user.JeevesActiveCompany, 20, cancellationToken);
            }

            var signingVersion = string.Join("|", signings
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(20)
                .Select(x => $"{x.Id:N}:{x.PortalStatus}:{x.ProviderStatus}:{x.SignedAndSealed}:{(x.LastSyncedAtUtc.HasValue ? x.LastSyncedAtUtc.Value.ToUniversalTime().Ticks : x.CreatedAtUtc.ToUniversalTime().Ticks)}"));
            var selectedSigning = signings.FirstOrDefault(x => x.Id == selectedSigningId);
            var version = string.Join("||", new[]
            {
                $"count:{signings.Count}",
                $"selected:{selectedSigning?.Id.ToString("N") ?? "none"}:{selectedSigning?.PortalStatus ?? "-"}:{selectedSigning?.ProviderStatus ?? "-"}:{selectedSigning?.SignedAndSealed.ToString() ?? "-"}",
                $"signings:{signingVersion}"
            });

            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";

            return Json(new
            {
                version
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DocumentSigningPing(long? orderNo, Guid? selectedSigningId, CancellationToken cancellationToken)
        {
            if (!await HasCompanyPermissionAsync(PortalModuleIds.DocumentSigningSubModule))
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is null)
                return Forbid();

            var canPing = _documentSigningService.CanPingForCompany(user.CompanyId.Value);
            if (!canPing)
            {
                _sidebarRuntimeStatusService.RecordEvent(user, new SidebarRuntimeEventRecord
                {
                    Source = "Oneflow",
                    Title = "Ping",
                    Summary = "Ping är bara tillgänglig när en token är konfigurerad.",
                    LinkUrl = Url.Action(nameof(DocumentSigning), "Integration"),
                    StatusLabel = "Skipped",
                    StatusTone = "muted",
                    IconClass = "fa fa-plug"
                });
                return RedirectToAction(nameof(DocumentSigning), new { orderNo, selectedSigningId });
            }

            try
            {
                await _documentSigningService.PingAsync(user.CompanyId.Value, cancellationToken);
                _sidebarRuntimeStatusService.RecordEvent(user, new SidebarRuntimeEventRecord
                {
                    Source = "Oneflow",
                    Title = "Ping",
                    Summary = "Oneflow svarade OK.",
                    LinkUrl = Url.Action(nameof(DocumentSigning), "Integration"),
                    StatusLabel = "Completed",
                    StatusTone = "success",
                    IconClass = "fa fa-plug"
                });
            }
            catch (Exception ex)
            {
                _sidebarRuntimeStatusService.RecordEvent(user, new SidebarRuntimeEventRecord
                {
                        Source = "Oneflow",
                        Title = "Ping",
                        Summary = GetSafeIntegrationFailureMessage(ex, "Oneflow-svaret kunde inte läsas"),
                        LinkUrl = Url.Action(nameof(DocumentSigning), "Integration"),
                        StatusLabel = "Failed",
                        StatusTone = "danger",
                    IconClass = "fa fa-plug"
                });
            }

            return RedirectToAction(nameof(DocumentSigning), new { orderNo, selectedSigningId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DocumentSigningSelectOrder(long orderNo)
        {
            if (!await HasCompanyPermissionAsync(PortalModuleIds.DocumentSigningSubModule))
                return Forbid();

            return RedirectToAction(nameof(DocumentSigning), new { orderNo });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DocumentSigningSend([Bind(Prefix = "DocumentSigningForm")] OrderDocumentSigningFormViewModel form, IFormFile? mainFile, List<IFormFile>? attachments, CancellationToken cancellationToken)
        {
            if (!await HasCompanyPermissionAsync(PortalModuleIds.DocumentSigningSubModule))
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId == null)
                return Forbid();

            var isConfigured = _documentSigningService.IsEnabledForCompany(user.CompanyId.Value);
            var relatedOrderNo = form.RelatedOrderNo;
            if (!isConfigured)
            {
                SetScopedAlertForAction(nameof(DocumentSigning), Alert.INFORMATION, "Dokumentsignering är inte konfigurerad för det här bolaget ännu.");
                return RedirectToAction(nameof(DocumentSigning), new { orderNo = relatedOrderNo });
            }

            var runtimeContext = await ResolveRuntimeContextAsync(user, cancellationToken);
            if (!runtimeContext.Success || runtimeContext.Value is null)
            {
                SetScopedAlertForAction(
                    nameof(DocumentSigning),
                    Alert.DANGER,
                    string.IsNullOrWhiteSpace(runtimeContext.Error)
                        ? "Dokumentsignering kräver just nu en aktiv Jeeves-koppling. Försök igen om en stund."
                        : runtimeContext.Error);
                return RedirectToAction(nameof(DocumentSigning), new { orderNo = relatedOrderNo });
            }

            var validationError = ValidateDocumentSigningUpload(mainFile, attachments);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                SetScopedAlertForAction(nameof(DocumentSigning), Alert.DANGER, validationError);
                return RedirectToAction(nameof(DocumentSigning), new { orderNo = relatedOrderNo });
            }

            if (!ModelState.IsValid)
            {
                SetScopedAlertForAction(
                    nameof(DocumentSigning),
                    Alert.DANGER,
                    string.Join(Environment.NewLine, ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)));
                return RedirectToAction(nameof(DocumentSigning), new { orderNo = relatedOrderNo });
            }

            OrderDetailsViewModel? order = null;
            if (relatedOrderNo.HasValue && relatedOrderNo.Value > 0)
            {
                order = await _ordersService.GetOrderDetailsAsync(
                    runtimeContext.Value.ConnectionString,
                    new GetOrderDetailsQuery
                    {
                        OrderNo = relatedOrderNo.Value,
                        CompanyCode = runtimeContext.Value.CompanyCode,
                        CompanyId = user.CompanyId
                    });

                if (order?.Header == null)
                {
                    SetScopedAlertForAction(nameof(DocumentSigning), Alert.DANGER, _sharedLocalizer["Integration_CouldNotFindSelectedOrder"].Value);
                    return RedirectToAction(nameof(DocumentSigning), new { orderNo = relatedOrderNo });
                }
            }

            try
            {
                var effectiveOrderNo = relatedOrderNo.GetValueOrDefault();
                var request = new DocumentSigningCreateRequest
                {
                    CompanyId = user.CompanyId.Value,
                    JeevesCompanyCode = runtimeContext.Value.CompanyCode,
                    OrderNo = effectiveOrderNo,
                    OrderCustomerName = order?.Header?.CustomerName ?? string.Empty,
                    CreatedByUserId = user.UserId,
                    CreatedByEmail = user.Email ?? string.Empty,
                    DocumentTitle = string.IsNullOrWhiteSpace(form.DocumentTitle)
                        ? (relatedOrderNo.HasValue && relatedOrderNo.Value > 0 ? $"Dokument {relatedOrderNo.Value}" : "Nytt dokument")
                        : form.DocumentTitle.Trim(),
                    SignerFirstName = form.SignerFirstName.Trim(),
                    SignerLastName = form.SignerLastName.Trim(),
                    SignerEmail = form.SignerEmail.Trim(),
                    SignerMobile = string.IsNullOrWhiteSpace(form.SignerMobile) ? null : form.SignerMobile.Trim(),
                    InvitationMessage = string.IsNullOrWhiteSpace(form.InvitationMessage) ? null : form.InvitationMessage.Trim(),
                    MainFile = mainFile != null && mainFile.Length > 0
                        ? await ReadPdfAsync(mainFile, cancellationToken)
                        : null,
                    Attachments = await ReadPdfListAsync(attachments ?? new List<IFormFile>(), cancellationToken),
                    Participants = new[]
                    {
                        new DocumentSigningParticipantInput
                        {
                            Name = $"{form.SignerFirstName.Trim()} {form.SignerLastName.Trim()}".Trim(),
                            Email = form.SignerEmail.Trim(),
                            PhoneNumber = string.IsNullOrWhiteSpace(form.SignerMobile) ? null : form.SignerMobile.Trim(),
                            Role = "signatory",
                            IsSignatory = true,
                            CanUpdateContract = true
                        }
                    }
                };

                var createResult = await _documentSigningService.CreateAndStartAsync(request, cancellationToken);
                _sidebarRuntimeStatusService.RecordEvent(
                    user,
                    DocumentSigningRuntimeEventFactory.CreateSentEvent(
                        effectiveOrderNo,
                        createResult.SigningId,
                        request.DocumentTitle,
                        request.SignerFirstName,
                        request.SignerLastName));
                SetScopedAlertForAction(nameof(DocumentSigning), Alert.SUCCESS, _sharedLocalizer["Integration_QuoteSentForSigning"].Value);
            }
            catch (Exception ex)
            {
                SetScopedAlertForAction(nameof(DocumentSigning), Alert.DANGER, GetSafeIntegrationFailureMessage(ex, "Dokumentsigneringen kunde inte skickas just nu"));
            }

            return RedirectToAction(nameof(DocumentSigning), new { orderNo = relatedOrderNo });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("api/signing/{id:guid}/launch")]
        public async Task<IActionResult> DocumentSigningLaunch(Guid id, CancellationToken cancellationToken)
        {
            if (!await HasCompanyPermissionAsync(PortalModuleIds.DocumentSigningSubModule))
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId == null)
                return Forbid();

            var isConfigured = _documentSigningService.IsEnabledForCompany(user.CompanyId.Value);
            if (!isConfigured)
                return BadRequest(new { message = "Dokumentsignering är inte tillgänglig utan Oneflow-konfiguration." });

            try
            {
                var launchResult = await _documentSigningService.LaunchAsync(user.CompanyId.Value, id, cancellationToken);
                if (launchResult == null)
                    return NotFound();

                return Json(new
                {
                    signingId = launchResult.SigningId,
                    participantId = launchResult.ParticipantId,
                    url = launchResult.AccessLinkUrl
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = GetSafeIntegrationFailureMessage(ex, "Dokumentsigneringen kunde inte startas just nu") });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DocumentSigningSync(Guid signingId, long? orderNo, CancellationToken cancellationToken)
        {
            if (!await HasCompanyPermissionAsync(PortalModuleIds.DocumentSigningSubModule))
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId == null)
                return Forbid();

            var isConfigured = _documentSigningService.IsEnabledForCompany(user.CompanyId.Value);
            if (!isConfigured)
            {
                SetScopedAlertForAction(nameof(DocumentSigning), Alert.INFORMATION, "Statussynk kräver Oneflow-konfiguration.");
                return RedirectToAction(nameof(DocumentSigning), new { orderNo });
            }

            try
            {
                var existingSignings = orderNo.HasValue
                    ? await _documentSigningService.ListForOrderAsync(
                        user.CompanyId.Value,
                        null,
                        orderNo.Value,
                        cancellationToken)
                    : Array.Empty<DocumentSigningListItem>();
                var previous = existingSignings.FirstOrDefault(x => x.Id == signingId);

                var updated = await _documentSigningService.SyncAsync(user.CompanyId.Value, signingId, cancellationToken);

                if (updated != null
                    && (previous == null
                        || !string.Equals(previous.PortalStatus, updated.PortalStatus, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(previous.ProviderStatus, updated.ProviderStatus, StringComparison.OrdinalIgnoreCase)
                        || previous.SignedAndSealed != updated.SignedAndSealed))
                {
                    _sidebarRuntimeStatusService.RecordEvent(
                        user,
                        DocumentSigningRuntimeEventFactory.CreateStatusChangedEvent(updated));
                }

                SetScopedAlertForAction(nameof(DocumentSigning), Alert.SUCCESS, _sharedLocalizer["Integration_SigningStatusUpdated"].Value);
            }
            catch (Exception ex)
            {
                SetScopedAlertForAction(nameof(DocumentSigning), Alert.DANGER, GetSafeIntegrationFailureMessage(ex, "Dokumentsigneringen kunde inte synkas just nu"));
            }

            return RedirectToAction(nameof(DocumentSigning), new { orderNo });
        }

        private static string? ValidateDocumentSigningUpload(IFormFile? mainFile, IReadOnlyCollection<IFormFile>? attachments)
        {
            if (mainFile is { Length: > 0 })
            {
                if (!IsPdf(mainFile))
                    return "Huvudfilen måste vara en PDF.";

                if (mainFile.Length > MaxPdfBytes)
                    return "Huvudfilen är för stor. Max 15 MB.";
            }

            if (attachments != null && attachments.Count > MaxAttachmentCount)
                return $"Max {MaxAttachmentCount} bilagor tillåts.";

            if (attachments == null)
                return null;

            foreach (var attachment in attachments.Where(x => x != null && x.Length > 0))
            {
                if (!IsPdf(attachment))
                    return "Alla bilagor måste vara PDF-filer.";

                if (attachment.Length > MaxPdfBytes)
                    return $"Bilagan {attachment.FileName} är för stor. Max 15 MB per fil.";
            }

            return null;
        }

        private static bool IsPdf(IFormFile file)
        {
            return string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<DocumentSigningUploadFile> ReadPdfAsync(IFormFile file, CancellationToken cancellationToken)
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            return new DocumentSigningUploadFile(SanitizeFileName(file.FileName), ms.ToArray());
        }

        private static async Task<IReadOnlyList<DocumentSigningUploadFile>> ReadPdfListAsync(IEnumerable<IFormFile> files, CancellationToken cancellationToken)
        {
            var result = new List<DocumentSigningUploadFile>();
            foreach (var file in files.Where(x => x != null && x.Length > 0))
                result.Add(await ReadPdfAsync(file, cancellationToken));

            return result;
        }

        private static string SanitizeFileName(string fileName)
        {
            var name = Path.GetFileName(fileName);
            return string.Concat(name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
