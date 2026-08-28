// Owns the bank reconciliation module UI and request flow.
using Entities.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApp.Models.Invoices;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.Bundles;
using WebApp.Services.Integration.BankReconciliation.Commands;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Presentation;
using WebApp.Services.Integration.BankReconciliation.Queries;
using WebApp.Services.Integration.BankReconciliation.UploadFlow;
using WebApp.Services.Integration.BankReconciliation.Workspace;
using WebApp.Services;
using WebApp.Observability;

namespace WebApp.Controllers
{
    [Authorize(Roles = "Administrator, User, SuperUser, Dashboard")]
    [Route("Integration/[action]")]
    [Route("[controller]/[action]")]
    public class BankReconciliationController : Controller
    {
        private readonly ICompanyPermissionGuard _companyPermissionGuard;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IBankReconciliationService _bankReconciliationService;
        private readonly IBankReconciliationInvoiceCandidateService _bankReconciliationInvoiceCandidateService;
        private readonly IBankReconciliationPageQueryService _bankReconciliationPageQueryService;
        private readonly IBankReconciliationInvoiceDetailPageQueryService _bankReconciliationInvoiceDetailPageQueryService;
        private readonly IBankReconciliationTransactionPageService _bankReconciliationTransactionPageService;
        private readonly IBankReconciliationUploadFlowService _bankReconciliationUploadFlowService;
        private readonly IBankReconciliationWorkspaceService _bankReconciliationWorkspaceService;
        private readonly IBankReconciliationMatchCommandService _bankReconciliationMatchCommandService;
        private readonly IBankReconciliationLifecycleCommandService _bankReconciliationLifecycleCommandService;
        private readonly IBankReconciliationCodingRuleCommandService _bankReconciliationCodingRuleCommandService;
        private readonly IBankReconciliationRecommendationQueryService _bankReconciliationRecommendationQueryService;
        private readonly IBankReconciliationStateQueryService _bankReconciliationStateQueryService;
        private readonly IBankReconciliationInvoicePageQueryService _bankReconciliationInvoicePageQueryService;
        private readonly IBankReconciliationDemoSessionService _bankReconciliationDemoSessionService;
        private readonly IBankReconciliationPageTempDataService _bankReconciliationPageTempDataService;
        private readonly IBankReconciliationPaymentBundleService _bankReconciliationPaymentBundleService;
        private readonly ILogger<BankReconciliationController> _logger;

        public BankReconciliationController(
            ICompanyPermissionGuard companyPermissionGuard,
            IHttpContextAccessor contextAccessor,
            IBankReconciliationService bankReconciliationService,
            IBankReconciliationInvoiceCandidateService bankReconciliationInvoiceCandidateService,
            IBankReconciliationPageQueryService bankReconciliationPageQueryService,
            IBankReconciliationInvoiceDetailPageQueryService bankReconciliationInvoiceDetailPageQueryService,
            IBankReconciliationTransactionPageService bankReconciliationTransactionPageService,
            IBankReconciliationUploadFlowService bankReconciliationUploadFlowService,
            IBankReconciliationWorkspaceService bankReconciliationWorkspaceService,
            IBankReconciliationMatchCommandService bankReconciliationMatchCommandService,
            IBankReconciliationLifecycleCommandService bankReconciliationLifecycleCommandService,
            IBankReconciliationCodingRuleCommandService bankReconciliationCodingRuleCommandService,
            IBankReconciliationRecommendationQueryService bankReconciliationRecommendationQueryService,
            IBankReconciliationStateQueryService bankReconciliationStateQueryService,
            IBankReconciliationInvoicePageQueryService bankReconciliationInvoicePageQueryService,
            IBankReconciliationDemoSessionService bankReconciliationDemoSessionService,
            IBankReconciliationPageTempDataService bankReconciliationPageTempDataService,
            IBankReconciliationPaymentBundleService bankReconciliationPaymentBundleService,
            ILogger<BankReconciliationController> logger)
        {
            _companyPermissionGuard = companyPermissionGuard;
            _contextAccessor = contextAccessor;
            _bankReconciliationService = bankReconciliationService;
            _bankReconciliationInvoiceCandidateService = bankReconciliationInvoiceCandidateService;
            _bankReconciliationPageQueryService = bankReconciliationPageQueryService;
            _bankReconciliationInvoiceDetailPageQueryService = bankReconciliationInvoiceDetailPageQueryService;
            _bankReconciliationTransactionPageService = bankReconciliationTransactionPageService;
            _bankReconciliationUploadFlowService = bankReconciliationUploadFlowService;
            _bankReconciliationWorkspaceService = bankReconciliationWorkspaceService;
            _bankReconciliationMatchCommandService = bankReconciliationMatchCommandService;
            _bankReconciliationLifecycleCommandService = bankReconciliationLifecycleCommandService;
            _bankReconciliationCodingRuleCommandService = bankReconciliationCodingRuleCommandService;
            _bankReconciliationRecommendationQueryService = bankReconciliationRecommendationQueryService;
            _bankReconciliationStateQueryService = bankReconciliationStateQueryService;
            _bankReconciliationInvoicePageQueryService = bankReconciliationInvoicePageQueryService;
            _bankReconciliationDemoSessionService = bankReconciliationDemoSessionService;
            _bankReconciliationPageTempDataService = bankReconciliationPageTempDataService;
            _bankReconciliationPaymentBundleService = bankReconciliationPaymentBundleService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> BankReconciliation()
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var feedback = _bankReconciliationPageTempDataService.ReadFeedback(TempData);
            var model = await _bankReconciliationPageQueryService.BuildPageAsync(
                user,
                feedback.UploadError,
                feedback.UploadInfo,
                feedback.StatusMessage,
                feedback.StatusTone,
                HttpContext.RequestAborted);

            return View("~/Views/Integration/BankReconciliation/BankReconciliation.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationToggleDemo()
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
                return Forbid();

            await _bankReconciliationDemoSessionService.ToggleDemoModeAsync(companyId, user, HttpContext.RequestAborted);

            return RedirectToAction(nameof(BankReconciliation));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationSelectDemoScenario(string? scenarioKey)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
                return Forbid();

            var result = await _bankReconciliationDemoSessionService.SelectScenarioAsync(
                companyId,
                user,
                scenarioKey,
                HttpContext.RequestAborted);

            _bankReconciliationPageTempDataService.ApplyDemoScenarioResult(TempData, result);
            return RedirectToAction(nameof(BankReconciliation));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationResetDemoScenario()
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
                return Forbid();

            var result = await _bankReconciliationDemoSessionService.ResetScenarioAsync(
                companyId,
                user,
                HttpContext.RequestAborted);

            _bankReconciliationPageTempDataService.ApplyDemoScenarioResult(TempData, result);
            return RedirectToAction(nameof(BankReconciliation));
        }

        [HttpGet]
        public async Task<IActionResult> BankReconciliationTransactions(
            int page = 1,
            int pageSize = 20,
            string? filter = "all",
            string? groupFilter = "all",
            string? classificationFilter = "all")
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var safePageSize = Math.Clamp(pageSize, 1, 100);
            var safePage = Math.Max(page, 1);

            try
            {
                var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
                var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
                var invoicesResult = user is null
                    ? (Invoices: new List<InvoiceItem>(), ErrorMessage: "User session is missing.")
                    : await LoadBankReconciliationUnpaidInvoicesAsync(source.IsDemoMode, user, HttpContext.RequestAborted, demoScenarioKey: source.DemoScenarioKey);
                var pageResult = _bankReconciliationTransactionPageService.BuildPage(
                    source.Transactions,
                    invoicesResult.Invoices,
                    safePage,
                    safePageSize,
                    filter,
                    groupFilter,
                    classificationFilter);

                return Json(pageResult);
            }
            catch (Exception ex)
            {
                var supportId = GetOrCreateSupportId();
                _logger.LogError(
                    "BankReconciliationTransactions failed. SupportId={SupportId} {Diagnostic}",
                    supportId,
                    IntegrationLogSanitizer.Diagnostic(ex.Message));

                return Json(_bankReconciliationTransactionPageService.BuildEmptyPage(
                    safePage,
                    safePageSize,
                    $"Bankavstämningen kunde inte läsas just nu. Referens: {supportId}."));
            }
        }

        [HttpGet]
        public async Task<IActionResult> BankReconciliationInvoices(int page = 1, int pageSize = 20, string? classificationFilter = "all", string? groupFilter = "all")
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationInvoicePageQueryService.BuildPageAsync(
                user,
                page,
                pageSize,
                classificationFilter,
                groupFilter,
                HttpContext.RequestAborted);

            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationUpload(IFormFile file)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            _bankReconciliationPageTempDataService.ApplyUploadResult(
                TempData,
                await _bankReconciliationUploadFlowService.UploadAsync(file, HttpContext.RequestAborted));
            return RedirectToAction(nameof(BankReconciliation));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationSaveCodingRules([FromBody] BankReconciliationCodingRuleSaveRequest request)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationCodingRuleCommandService.SaveAsync(user, request, HttpContext.RequestAborted);
            return CodingRuleCommandResponse(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationClearUpload()
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            _bankReconciliationPageTempDataService.ApplyUploadResult(TempData, _bankReconciliationUploadFlowService.ClearUpload());
            return RedirectToAction(nameof(BankReconciliation));
        }

        [HttpGet]
        public async Task<IActionResult> BankReconciliationInvoice(string? id)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var model = await _bankReconciliationInvoiceDetailPageQueryService.BuildPageAsync(user, source, HttpContext.RequestAborted);
            model.InvoiceId = id ?? string.Empty;

            return View("~/Views/Integration/BankReconciliation/BankReconciliationInvoice.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationManualMatch([FromBody] BankReconciliationManualMatchRequest request)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationMatchCommandService.SaveManualMatchAsync(source, user, request, HttpContext.RequestAborted);
            return MatchCommandResponse(result, success => new { success = true, match = success.Match, version = success.Version });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationSaveMatches([FromBody] BankReconciliationSaveMatchesRequest request)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationMatchCommandService.SaveMatchesAsync(source, user, request, HttpContext.RequestAborted);
            return MatchCommandResponse(result, success => new { success = true, count = success.Count, version = success.Version });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationReverseMatch([FromBody] BankReconciliationReverseMatchRequest request)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationMatchCommandService.ReverseMatchAsync(source, user, request, HttpContext.RequestAborted);
            return MatchCommandResponse(result, success => new { success = true, version = success.Version });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationAutoMatch()
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var expectedVersionHeader = Request.Headers["X-BankRec-State-Version"].FirstOrDefault();
            var expectedVersion = int.TryParse(expectedVersionHeader, out var parsedVersion)
                ? parsedVersion
                : (int?)null;

            var result = await _bankReconciliationMatchCommandService.AutoMatchAsync(source, user, expectedVersion, HttpContext.RequestAborted);
            return MatchCommandResponse(result, success => new
            {
                success = true,
                count = success.Count,
                version = success.Version,
                paymentBundleSuggestions = success.PaymentBundleSuggestions,
                matches = success.Matches.Select(match => new
                {
                    allocationId = match.AllocationId,
                    transactionId = match.TransactionId,
                    invoiceId = match.InvoiceId,
                    matchType = match.MatchType,
                    matchRule = match.MatchRule,
                    matchedAmount = match.MatchedAmount,
                    currency = match.Currency
                }).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationResetMatches([FromBody] BankReconciliationSaveMatchesRequest request)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationMatchCommandService.ResetMatchesAsync(source, user, request.ExpectedVersion, HttpContext.RequestAborted);
            return MatchCommandResponse(result, success => new { success = true, count = success.Count, version = success.Version });
        }

        [HttpGet]
        public async Task<IActionResult> BankReconciliationState()
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationStateQueryService.BuildStateAsync(source, user, HttpContext.RequestAborted);
            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationClose(
            [FromBody] BankReconciliationCloseRequest request)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationLifecycleCommandService.CloseAsync(
                source,
                user,
                request.ExpectedVersion,
                HttpContext.RequestAborted);

            return LifecycleCommandResponse(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationReopen(
            [FromBody] BankReconciliationReopenRequest request)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationLifecycleCommandService.ReopenAsync(
                source,
                user,
                request.ExpectedVersion,
                request.Reason,
                HttpContext.RequestAborted);

            return LifecycleCommandResponse(result);
        }

        [HttpGet]
        public async Task<IActionResult> BankReconciliationRecommendations(string transactionId)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationRecommendationQueryService.BuildRecommendationsAsync(
                source,
                user,
                transactionId,
                HttpContext.RequestAborted);

            return Json(new
            {
                success = result.Success,
                errorMessage = result.ErrorMessage,
                items = result.Items
            });
        }

        [HttpGet]
        public async Task<IActionResult> BankReconciliationAiSuggestions(string transactionId)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationRecommendationQueryService.BuildAiSuggestionsAsync(
                source,
                user,
                transactionId,
                HttpContext.RequestAborted);

            return Json(new
            {
                success = result.Success,
                errorMessage = result.ErrorMessage,
                result = result.Result
            });
        }

        [HttpGet]
        public async Task<IActionResult> BankReconciliationPaymentBundles()
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationPaymentBundleService.BuildSuggestionsAsync(
                source,
                user,
                HttpContext.RequestAborted);

            return result.Success
                ? Json(result)
                : BadRequest(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationConfirmPaymentBundle(
            [FromBody] BankReconciliationConfirmPaymentBundleRequest request)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationPaymentBundleService.ConfirmAsync(
                source,
                user,
                request,
                HttpContext.RequestAborted);

            if (!result.Success)
            {
                return result.Conflict
                    ? Conflict(result)
                    : BadRequest(result);
            }

            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BankReconciliationConfirmManualPaymentBundle(
            [FromBody] BankReconciliationConfirmManualPaymentBundleRequest request)
        {
            if (!await HasBankReconciliationAccessAsync())
                return Forbid();

            var source = await ResolveBankReconciliationSourceContextAsync(HttpContext.RequestAborted);
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var result = await _bankReconciliationPaymentBundleService.ConfirmManualAsync(
                source,
                user,
                request,
                HttpContext.RequestAborted);

            if (!result.Success)
            {
                return result.Conflict
                    ? Conflict(result)
                    : BadRequest(result);
            }

            return Json(result);
        }

        private async Task<BankReconciliationSourceContext> ResolveBankReconciliationSourceContextAsync(CancellationToken cancellationToken)
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var companyId = user?.CompanyId ?? Guid.Empty;
            var source = await _bankReconciliationWorkspaceService.ResolveSourceAsync(
                user,
                _bankReconciliationUploadFlowService.ResolveLatestCamtFile(),
                companyId != Guid.Empty && _bankReconciliationDemoSessionService.IsDemoMode(companyId),
                _bankReconciliationDemoSessionService.ResolveScenarioKey(companyId),
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(source.ErrorMessage))
            {
                _bankReconciliationPageTempDataService.ApplySourceError(TempData, source.ErrorMessage);
            }
            return source;
        }

        private async Task<(List<InvoiceItem> Invoices, string? ErrorMessage)> LoadBankReconciliationUnpaidInvoicesAsync(
            bool isDemoMode,
            UserSession user,
            CancellationToken cancellationToken,
            BankReconciliationParsedTransaction? transaction = null,
            string? demoScenarioKey = null)
        {
            var result = await _bankReconciliationInvoiceCandidateService.LoadAsync(
                isDemoMode,
                user,
                cancellationToken,
                transaction,
                demoScenarioKey: demoScenarioKey);
            return (result.Invoices, result.ErrorMessage);
        }

        private IActionResult MatchCommandResponse(BankReconciliationMatchCommandResult result, Func<BankReconciliationMatchCommandResult, object> successPayload)
        {
            if (result.Success)
            {
                return Json(successPayload(result));
            }

            var errorPayload = new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Matchning kunde inte sparas.",
                currentVersion = result.CurrentVersion
            };

            return result.Conflict ? Conflict(errorPayload) : BadRequest(errorPayload);
        }

        private IActionResult CodingRuleCommandResponse(BankReconciliationCodingRuleCommandResult result)
        {
            if (result.Success)
            {
                return Json(new
                {
                    success = true,
                    version = result.Version,
                    rows = result.Rows,
                    bankAccountKey = result.BankAccountKey,
                    bankAccountLabel = result.BankAccountLabel
                });
            }

            var errorPayload = new
            {
                success = false,
                errorMessage = result.ErrorMessage,
                currentVersion = result.CurrentVersion
            };

            return result.Conflict ? Conflict(errorPayload) : Json(errorPayload);
        }

        private IActionResult LifecycleCommandResponse(
            BankReconciliationLifecycleCommandResult result)
        {
            if (result.Success)
            {
                return Json(new
                {
                    success = true,
                    version = result.Version,
                    isClosed = result.IsClosed,
                    closedAtUtc = result.ClosedAtUtc,
                    closedByName = result.ClosedByName
                });
            }

            var errorPayload = new
            {
                success = false,
                errorMessage = result.ErrorMessage,
                currentVersion = result.Version,
                reviewCount = result.ReviewCount,
                unmatchedCount = result.UnmatchedCount
            };
            return result.Conflict ? Conflict(errorPayload) : BadRequest(errorPayload);
        }

        private string GetOrCreateSupportId()
        {
            var supportId = HttpContext?.Items[PortalObservability.SupportIdItemKey]?.ToString();
            if (!string.IsNullOrWhiteSpace(supportId))
            {
                return supportId!;
            }

            supportId = Guid.NewGuid().ToString("N")[..8];
            if (HttpContext is { } httpContext)
            {
                httpContext.Items[PortalObservability.SupportIdItemKey] = supportId;
            }

            return supportId;
        }

        private async Task<bool> HasBankReconciliationAccessAsync()
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is null)
                return false;

            if (await _companyPermissionGuard.HasAccessAsync(user.CompanyId.Value, PortalModuleIds.BankReconciliationModule))
                return true;

            return await _companyPermissionGuard.HasAccessAsync(user.CompanyId.Value, PortalModuleIds.BankReconciliationSubModule);
        }

    }
}
