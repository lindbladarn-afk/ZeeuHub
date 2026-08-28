// Handles authorized Intelligence pages, queries, feedback, quota, and manual SQL requests.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Entities.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using WebApp.Filters;
using WebApp.Models.Application;
using WebApp.Models.AI;
using WebApp.Models.Telemetry;
using WebApp.Services;
using WebApp.Services.Application;
using WebApp.Services.Application.AI;
using WebApp.Services.Application.AI.Quota;
using WebApp.Services.Telemetry;

namespace WebApp.Controllers
{
    // Keep roles aligned with pages that surface AI features (e.g. MemberController/MainDashboard).
    [Authorize(Roles = "Administrator, User, SuperUser, Dashboard")]
    [ServiceFilter(typeof(TenantValidationFilter))]
    public class AIController : Controller
    {
        private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

        private static readonly Guid SubModuleIntelligenceNewId =
            Guid.Parse("c1a47e0e-8f4f-4db0-8d53-72b0fb4d8a6c");

        private readonly IHttpContextAccessor _http;
        private readonly IFeatureAccessService _featureAccessService;
        private readonly ICompanyPermissionGuard _companyPermissionGuard;
        private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;
        private readonly IAiInvoiceQuestionService _invoiceQuestionService;

        private readonly IAiDataSourceResolver _dataSourceResolver;
        private readonly IAiRequestContextPolicy _requestContextPolicy;
        private readonly IAiDbChatOrchestrator _dbChatOrchestrator;
        private readonly IAiSqlExecutor _sql;
        private readonly IAiQuotaService _aiQuotaService;
        private readonly ITelemetryService _telemetryService;
        private readonly IPortalEventLogService _portalEventLogService;

        public AIController(
            IHttpContextAccessor http,
            IFeatureAccessService featureAccessService,
            ICompanyPermissionGuard companyPermissionGuard,
            IJeevesRuntimeContextService jeevesRuntimeContextService,
            IAiInvoiceQuestionService invoiceQuestionService,
            IAiDataSourceResolver dataSourceResolver,
            IAiRequestContextPolicy requestContextPolicy,
            IAiDbChatOrchestrator dbChatOrchestrator,
            IAiSqlExecutor sql,
            IAiQuotaService aiQuotaService,
            ITelemetryService telemetryService,
            IPortalEventLogService portalEventLogService)
        {
            _http = http;
            _featureAccessService = featureAccessService;
            _companyPermissionGuard = companyPermissionGuard;
            _jeevesRuntimeContextService = jeevesRuntimeContextService;
            _invoiceQuestionService = invoiceQuestionService;
            _dataSourceResolver = dataSourceResolver;
            _requestContextPolicy = requestContextPolicy;
            _dbChatOrchestrator = dbChatOrchestrator;
            _sql = sql;
            _aiQuotaService = aiQuotaService;
            _telemetryService = telemetryService;
            _portalEventLogService = portalEventLogService;
        }

        // =========================
        // UI
        // =========================
        [HttpGet]
        public async Task<IActionResult> Intelligence()
        {
            var runtimeContext = await GetJeevesRuntimeContextAsync();
            var companyCode = runtimeContext?.CompanyCode;

            if (!IsFeatureAllowed(companyCode))
                return Forbid();

            if (!await HasCompanyPermissionAsync())
                return Forbid();

            var model = await BuildAiViewModelAsync(HttpContext.RequestAborted);

            return View(model);
        }

        [HttpGet]
        [Route("AI/assistant-widget")]
        public async Task<IActionResult> AssistantWidget()
        {
            var runtimeContext = await GetJeevesRuntimeContextAsync();
            var companyCode = runtimeContext?.CompanyCode;

            if (!IsFeatureAllowed(companyCode))
                return Forbid();

            if (!await HasCompanyPermissionAsync())
                return Forbid();

            ViewData["AiQuerySource"] = "assistant";
            var model = await BuildAiViewModelAsync(HttpContext.RequestAborted, preferTenantDataSource: true);
            return View(model);
        }

        // =========================
        // Datasource selection (Admin)
        // =========================
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        [Route("AI/set-datasource")]
        public async Task<IActionResult> SetDataSource(string key, string? returnUrl = null)
        {
            var selected = _dataSourceResolver.GetConfiguredDataSources()
                .FirstOrDefault(x => string.Equals(x.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (selected is null)
                return BadRequest("Den valda AI-datakällan är inte konfigurerad.");

            _dataSourceResolver.SetSelected(selected.Key);

            var (_, info) = await _dataSourceResolver.ResolveAsync(selected.Key, HttpContext.RequestAborted);
            TempData["AiDataSourceMessage"] = $"Datakälla: {info.Name}";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction(nameof(Intelligence));
        }

        // =========================
        // Chat/query (AI -> SQL -> Result + table)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("ai-query")]
        [Route("AI/query")]
        public async Task<IActionResult> Query([FromBody] AiQueryRequest request, CancellationToken ct)
        {
            return Json(await ExecuteQueryAsync(request, progress: null, ct));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("ai-query")]
        [Route("AI/query-stream")]
        public async Task QueryStream([FromBody] AiQueryRequest request, CancellationToken ct)
        {
            Response.ContentType = "application/x-ndjson; charset=utf-8";
            Response.Headers.CacheControl = "no-cache, no-store";
            Response.Headers.Append("X-Accel-Buffering", "no");

            async Task WriteEventAsync(AiQueryStreamEvent streamEvent, CancellationToken cancellationToken)
            {
                var json = JsonSerializer.Serialize(streamEvent, StreamJsonOptions);
                await Response.WriteAsync(json + "\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            try
            {
                var result = await ExecuteQueryAsync(
                    request,
                    (update, cancellationToken) =>
                        WriteEventAsync(AiQueryStreamEvent.FromProgress(update), cancellationToken),
                    ct);

                if (result.Success)
                {
                    await WriteEventAsync(
                        AiQueryStreamEvent.FromProgress(
                            new AiProgressUpdate("completed", "Analysen är klar", 100)),
                        ct);
                }

                await WriteEventAsync(AiQueryStreamEvent.FromResult(result), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The browser disconnected or cancelled the request.
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("AI/feedback")]
        public async Task<IActionResult> SubmitFeedback([FromBody] AiFeedbackRequest request, CancellationToken ct)
        {
            var runtimeContext = await GetJeevesRuntimeContextAsync();
            if (!IsFeatureAllowed(runtimeContext?.CompanyCode) || !await HasCompanyPermissionAsync())
                return Json(new { success = false, message = "Feedback kunde inte sparas." });

            var rating = (request?.Rating ?? string.Empty).Trim().ToLowerInvariant();
            if (request is null ||
                request.ResponseId == Guid.Empty ||
                (rating != "helpful" && rating != "not_helpful"))
            {
                return BadRequest(new { success = false, message = "Ogiltig feedback." });
            }

            var sessionUser = _http.HttpContext?.Session.Get<UserSession>("UserObject");
            var comment = TrimToLength(request.Comment, 500);
            var additionalData = JsonSerializer.Serialize(new
            {
                responseId = request.ResponseId,
                rating,
                comment
            }, StreamJsonOptions);

            await _portalEventLogService.RecordAsync(new PortalEventLogEntry
            {
                Module = "ZeeU Intelligence",
                Action = "AnswerFeedback",
                CompanyId = sessionUser?.CompanyId,
                CompanyName = sessionUser?.CompanyName,
                JeevesCompanyCode = runtimeContext?.CompanyCode,
                UserId = sessionUser?.UserId,
                UserEmail = sessionUser?.Email,
                RequestPath = HttpContext.Request.Path,
                CorrelationId = request.ResponseId.ToString("N"),
                Severity = rating == "helpful" ? "Information" : "Warning",
                Message = rating == "helpful"
                    ? "Användaren markerade AI-svaret som hjälpsamt."
                    : "Användaren markerade AI-svaret som inte hjälpsamt.",
                AdditionalData = additionalData
            }, ct);

            return Json(new { success = true, message = "Tack för feedbacken." });
        }

        // =========================
        // Quota decision (continue paid / block until reset)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("AI/quota-decision")]
        public async Task<IActionResult> SetQuotaDecision([FromBody] AiQuotaDecisionRequest request, CancellationToken ct)
        {
            var runtimeContext = await GetJeevesRuntimeContextAsync();
            var sessionCompanyCode = runtimeContext?.CompanyCode;
            var (companyId, userId) = GetCurrentSessionIdentity();

            if (!IsFeatureAllowed(sessionCompanyCode) || !await HasCompanyPermissionAsync())
                return Json(new { success = false, message = "AI är inte tillgängligt." });

            if (companyId is null || string.IsNullOrWhiteSpace(userId))
                return Json(new { success = false, message = "Kunde inte identifiera användare/bolag." });

            var choiceRaw = (request?.Choice ?? string.Empty).Trim().ToLowerInvariant();
            var choice = choiceRaw switch
            {
                "allow_paid" => AiQuotaDecisionChoice.AllowPaid,
                "block_until_reset" => AiQuotaDecisionChoice.BlockUntilReset,
                _ => (AiQuotaDecisionChoice?)null
            };

            if (!choice.HasValue)
                return Json(new { success = false, message = "Ogiltigt val." });

            var state = await _aiQuotaService.SetDecisionAsync(companyId, userId, choice.Value, ct);
            return Json(ToQuotaJson(state));
        }

        [HttpGet]
        [Route("AI/quota-status")]
        public async Task<IActionResult> GetQuotaStatus(CancellationToken ct)
        {
            var runtimeContext = await GetJeevesRuntimeContextAsync();
            var sessionCompanyCode = runtimeContext?.CompanyCode;
            var (companyId, userId) = GetCurrentSessionIdentity();

            if (!IsFeatureAllowed(sessionCompanyCode) || !await HasCompanyPermissionAsync())
                return Json(new { success = false, message = "AI är inte tillgängligt." });

            if (companyId is null || string.IsNullOrWhiteSpace(userId))
                return Json(new { success = false, message = "Kunde inte identifiera användare/bolag." });

            var state = await _aiQuotaService.EvaluateAsync(companyId, userId, additionalTokens: 0, ct);
            return Json(ToQuotaJson(state));
        }

        // =========================
        // Manual SQL (SELECT/CTE only, max 200 rows)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("AI/manual-query")]
        public async Task<IActionResult> ManualQuery([FromBody] ManualSqlRequest request, CancellationToken ct)
        {
            var runtimeContext = await GetJeevesRuntimeContextAsync();
            var companyCode = runtimeContext?.CompanyCode;

            if (!IsFeatureAllowed(companyCode) || !await HasCompanyPermissionAsync())
                return Json(new ManualSqlResponse { Success = false, Error = "Manual SQL är inte tillgängligt." });

            var sql = (request?.Sql ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sql))
                return Json(new ManualSqlResponse { Success = false, Error = "Skriv en SELECT-fråga först." });

            if (runtimeContext is null)
                return Json(new ManualSqlResponse { Success = false, Error = "Kunde inte läsa verifierad bolagskontext." });

            var contextRequest = new AiQueryRequest
            {
                DataSourceKey = _dataSourceResolver.GetSelected()
            };
            var contextResult = _requestContextPolicy.Apply(
                contextRequest,
                runtimeContext,
                User.IsInRole("Administrator"),
                requireTenantDataSource: false);

            if (!contextResult.Success)
                return Json(new ManualSqlResponse { Success = false, Error = contextResult.Error });

            var (conn, info) = await _dataSourceResolver.ResolveAsync(contextRequest.DataSourceKey, ct);

            if (string.IsNullOrWhiteSpace(conn))
            {
                return Json(new ManualSqlResponse
                {
                    Success = false,
                    Error = "Ingen anslutningssträng för vald datakälla."
                });
            }

            try
            {
                var normalized = sql.TrimStart();
                var isSelectLike =
                    normalized.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith("with", StringComparison.OrdinalIgnoreCase);

                if (!isSelectLike)
                {
                    return Json(new ManualSqlResponse
                    {
                        Success = false,
                        Error = "Endast SELECT/CTE (WITH ... SELECT) är tillåtna."
                    });
                }

                var res = await _sql.ExecuteSelectAsync(conn, sql, maxRows: 200, ct: ct);

                if (!res.Success)
                {
                    return Json(new ManualSqlResponse
                    {
                        Success = false,
                        Error = "Databasfrågan kunde inte köras. Kontrollera SQL-syntaxen och försök igen.",
                        ErrorCode = "query_failed",
                        CanRetry = true,
                        ExecutedSql = res.ExecutedSql ?? sql
                    });
                }

                return Json(new ManualSqlResponse
                {
                    Success = true,
                    Columns = res.Columns ?? new List<string>(),
                    Rows = res.Rows ?? new List<List<object?>>(),
                    RowCount = res.Rows?.Count ?? 0,
                    Truncated = res.Truncated,
                    ExecutedSql = res.ExecutedSql ?? sql,
                    Message = $"Datakälla: {info.Name}"
                });
            }
            catch (Exception ex)
            {
                var sessionUser = _http.HttpContext?.Session.Get<UserSession>("UserObject");
                await _portalEventLogService.RecordAsync(new PortalEventLogEntry
                {
                    Module = "ZeeU Intelligence",
                    Action = "ManualQueryFailed",
                    CompanyId = sessionUser?.CompanyId,
                    CompanyName = sessionUser?.CompanyName,
                    JeevesCompanyCode = runtimeContext.CompanyCode,
                    UserId = sessionUser?.UserId,
                    UserEmail = sessionUser?.Email,
                    RequestPath = HttpContext.Request.Path,
                    CorrelationId = HttpContext.TraceIdentifier,
                    Severity = "Error",
                    Message = "En manuell Intelligence-fråga misslyckades.",
                    Exception = ex
                }, ct);
                return Json(new ManualSqlResponse
                {
                    Success = false,
                    Error = "Ett oväntat fel uppstod när databasfrågan kördes.",
                    ErrorCode = "unexpected",
                    CanRetry = true
                });
            }
        }

        // =========================
        // Helpers
        // =========================
        private async Task<AiQueryResponse> ExecuteQueryAsync(
            AiQueryRequest? request,
            AiProgressCallback? progress,
            CancellationToken ct)
        {
            request ??= new AiQueryRequest();
            request.Question = (request.Question ?? string.Empty).Trim();
            var question = request.Question;
            var isAssistantSource = string.Equals(request.Source, "assistant", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(question))
            {
                return Present(new AiQueryResponse
                {
                    Success = false,
                    Answer = "Skriv en fråga först.",
                    Error = new AiQueryError
                    {
                        Code = "empty_question",
                        Title = "Frågan är tom",
                        Message = "Skriv vad du vill analysera innan du skickar frågan.",
                        CanRetry = false,
                        Tone = "info"
                    }
                }, question);
            }

            if (question.Length > 2000)
            {
                return Present(new AiQueryResponse
                {
                    Success = false,
                    Answer = "Frågan är för lång.",
                    Error = new AiQueryError
                    {
                        Code = "question_too_long",
                        Title = "Frågan är för lång",
                        Message = "Förkorta frågan till högst 2 000 tecken och försök igen.",
                        CanRetry = false,
                        Tone = "info"
                    }
                }, question);
            }

            await ReportProgressAsync(progress, "validating", "Verifierar bolag och behörighet", 8, ct);
            var runtimeContext = await GetJeevesRuntimeContextAsync();
            var sessionCompanyCode = runtimeContext?.CompanyCode;
            var (companyId, userId) = GetCurrentSessionIdentity();

            if (!IsFeatureAllowed(sessionCompanyCode) || !await HasCompanyPermissionAsync())
            {
                const string unavailableMessage = "AI är inte tillgängligt.";
                await TryLogAiQueryAsync(
                    companyId,
                    userId,
                    question,
                    allowed: false,
                    wasSuccessful: false,
                    sqlText: null,
                    errorMessage: unavailableMessage,
                    promptTokens: null,
                    completionTokens: null,
                    totalTokens: null);
                return Present(new AiQueryResponse
                {
                    Success = false,
                    Answer = unavailableMessage,
                    ErrorMessage = unavailableMessage
                }, question);
            }

            if (runtimeContext is null)
            {
                return Present(new AiQueryResponse
                {
                    Success = false,
                    Answer = "Kunde inte verifiera aktivt bolag.",
                    ErrorMessage = "Kunde inte verifiera aktivt bolag."
                }, question);
            }

            var contextResult = _requestContextPolicy.Apply(
                request,
                runtimeContext,
                User.IsInRole("Administrator"),
                requireTenantDataSource: isAssistantSource);
            if (!contextResult.Success)
            {
                var message = contextResult.Error ?? "AI-datakällan är inte tillåten.";
                await TryLogAiQueryAsync(
                    companyId,
                    userId,
                    question,
                    allowed: false,
                    wasSuccessful: false,
                    sqlText: null,
                    errorMessage: message,
                    promptTokens: null,
                    completionTokens: null,
                    totalTokens: null);
                return Present(new AiQueryResponse
                {
                    Success = false,
                    Answer = message,
                    ErrorMessage = message
                }, question);
            }

            await ReportProgressAsync(progress, "shortcut", "Kontrollerar om frågan kan besvaras direkt", 18, ct);
            var invoiceResponse = await _invoiceQuestionService.TryAnswerAsync(
                question,
                runtimeContext.ConnectionString,
                request.CompanyCode,
                ct);

            if (invoiceResponse is not null)
            {
                invoiceResponse = Present(invoiceResponse, question);
                await TryLogAiQueryAsync(
                    companyId,
                    userId,
                    question,
                    allowed: true,
                    wasSuccessful: invoiceResponse.Success,
                    sqlText: null,
                    errorMessage: invoiceResponse.ErrorMessage ?? invoiceResponse.Warning,
                    promptTokens: null,
                    completionTokens: null,
                    totalTokens: null,
                    response: invoiceResponse);
                return invoiceResponse;
            }

            await ReportProgressAsync(progress, "quota", "Kontrollerar tillgänglig AI-kvot", 24, ct);
            var preQuota = await _aiQuotaService.EvaluateAsync(companyId, userId, additionalTokens: 0, ct);
            if (preQuota.Status == AiQuotaStatus.NeedsDecision || preQuota.Status == AiQuotaStatus.Blocked)
            {
                await TryLogAiQueryAsync(
                    companyId,
                    userId,
                    question,
                    allowed: false,
                    wasSuccessful: false,
                    sqlText: null,
                    errorMessage: preQuota.Message,
                    promptTokens: null,
                    completionTokens: null,
                    totalTokens: null);

                var quotaBlocked = new AiQueryResponse
                {
                    Success = false,
                    Answer = preQuota.Message,
                    Warning = preQuota.Message,
                    ErrorMessage = preQuota.Message
                };
                ApplyQuota(quotaBlocked, preQuota);
                return Present(quotaBlocked, question);
            }

            await ReportProgressAsync(progress, "datasource", "Ansluter till den verifierade datakällan", 29, ct);
            var (_, info) = await _dataSourceResolver.ResolveAsync(request.DataSourceKey, ct);
            request.DataSourceKey = info.Key;

            try
            {
                var response = await _dbChatOrchestrator.AskDatabaseAsync(request, progress, ct);
                response = Present(response, question);
                var projectedQuota = await _aiQuotaService.EvaluateAsync(
                    companyId,
                    userId,
                    additionalTokens: response.TotalTokens ?? 0,
                    ct: ct);
                ApplyQuota(response, projectedQuota);

                await TryLogAiQueryAsync(
                    companyId,
                    userId,
                    question,
                    allowed: true,
                    wasSuccessful: response.Success,
                    sqlText: string.IsNullOrWhiteSpace(response.Sql) ? null : response.Sql,
                    errorMessage: response.ErrorMessage ?? response.Warning,
                    promptTokens: response.PromptTokens,
                    completionTokens: response.CompletionTokens,
                    totalTokens: response.TotalTokens,
                    response: response);
                return response;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failedResponse = Present(new AiQueryResponse
                {
                    Success = false,
                    Answer = "Ett oväntat fel uppstod.",
                    ErrorMessage = ex.Message
                }, question);
                await TryLogAiQueryAsync(
                    companyId,
                    userId,
                    question,
                    allowed: false,
                    wasSuccessful: false,
                    sqlText: null,
                    errorMessage: ex.Message,
                    promptTokens: null,
                    completionTokens: null,
                    totalTokens: null,
                    response: failedResponse);
                return failedResponse;
            }
        }

        private static AiQueryResponse Present(AiQueryResponse response, string? question) =>
            AiQueryResponsePresenter.Prepare(response, question);

        private static Task ReportProgressAsync(
            AiProgressCallback? progress,
            string step,
            string message,
            int percent,
            CancellationToken cancellationToken)
        {
            return progress is null
                ? Task.CompletedTask
                : progress(new AiProgressUpdate(step, message, percent), cancellationToken);
        }

        private static string? TrimToLength(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private async Task<AiViewModel> BuildAiViewModelAsync(CancellationToken ct, bool preferTenantDataSource = false)
        {
            var dataSources = _dataSourceResolver.GetConfiguredDataSources();
            string? requestedKey = null;
            if (preferTenantDataSource || !User.IsInRole("Administrator"))
            {
                requestedKey = dataSources
                    .FirstOrDefault(x => x.IsTenantConnection)?
                    .Key;
            }

            var (_, info) = await _dataSourceResolver.ResolveAsync(requestedKey, ct);

            return new AiViewModel
            {
                IsAdmin = User.IsInRole("Administrator"),
                SelectedDataSourceKey = info.Key,
                SelectedDataSourceName = info.Name,
                DataSourceInfo = info,
                DataSources = dataSources
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private async Task<JeevesRuntimeContext?> GetJeevesRuntimeContextAsync()
        {
            var user = _http.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user is null)
                return null;

            var runtimeContext = await _jeevesRuntimeContextService.ResolveAsync(user, HttpContext.RequestAborted);
            return runtimeContext.Success ? runtimeContext.Value : null;
        }

        private bool IsFeatureAllowed(int? companyCode)
        {
            if (companyCode is null) return false;
            return _featureAccessService.IsEnabled(HttpContext.Session, companyCode.Value, FeatureFlag.Ai);
        }

        private async Task<bool> HasCompanyPermissionAsync()
        {
            var user = _http.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is null) return false;

            return await _companyPermissionGuard.HasAccessAsync(user.CompanyId.Value, SubModuleIntelligenceNewId);
        }

        private (Guid? CompanyId, string? UserId) GetCurrentSessionIdentity()
        {
            var user = _http.HttpContext?.Session.Get<UserSession>("UserObject");
            return (user?.CompanyId, user?.UserId);
        }

        private async Task TryLogAiQueryAsync(
            Guid? companyId,
            string? userId,
            string? question,
            bool allowed,
            bool? wasSuccessful,
            string? sqlText,
            string? errorMessage,
            int? promptTokens,
            int? completionTokens,
            int? totalTokens,
            AiQueryResponse? response = null)
        {
            try
            {
                var diagnostics = response?.Diagnostics;
                await _telemetryService.LogAiQueryAsync(
                    companyId,
                    userId,
                    question,
                    allowed,
                    wasSuccessful,
                    sqlText,
                    errorMessage,
                    promptTokens,
                    completionTokens,
                    totalTokens,
                    new AiQueryTelemetryDetails
                    {
                        ResponseId = response?.ResponseId,
                        PromptVersion = diagnostics?.PromptVersion,
                        ModelDeployment = diagnostics?.ModelDeployment,
                        ErrorCode = diagnostics?.ErrorCode ?? response?.Error?.Code,
                        VerificationStatus = response?.Evidence?.VerificationStatus,
                        DurationMs = diagnostics?.TotalDurationMs,
                        PlanningDurationMs = diagnostics?.PlanningDurationMs,
                        SqlDurationMs = diagnostics?.SqlDurationMs,
                        SummaryDurationMs = diagnostics?.SummaryDurationMs,
                        ModelRetryCount = diagnostics?.ModelRetryCount,
                        RowCount = response?.RowCount,
                        WasTruncated = response?.Truncated
                    });
            }
            catch
            {
                // Telemetry errors must never block the user flow.
            }
        }

        private static void ApplyQuota(AiQueryResponse response, AiQuotaEvaluation quota)
        {
            if (response == null || quota == null)
                return;

            response.QuotaStatus = quota.Status.ToString().ToLowerInvariant();
            response.QuotaMessage = quota.Message;
            response.QuotaUsedTokens = quota.UsedTokens;
            response.QuotaFreeTokens = quota.FreeTokens;
            response.QuotaUsagePercent = quota.UsagePercent;
            response.QuotaPeriodTotalCostSek = quota.PeriodTotalCostSek;
            response.QuotaPaidExtraTokens = quota.PaidExtraTokens;
            response.QuotaPaidExtraCostSek = quota.PaidExtraCostSek;
            response.QuotaNeedsDecision = quota.RequiresDecision;
            response.QuotaPaidMode = quota.IsPaidMode;
        }

        private static object ToQuotaJson(AiQuotaEvaluation state) => new
        {
            success = true,
            status = state.Status.ToString().ToLowerInvariant(),
            message = state.Message,
            usedTokens = state.UsedTokens,
            freeTokens = state.FreeTokens,
            usagePercent = state.UsagePercent,
            periodTotalCostSek = state.PeriodTotalCostSek,
            paidExtraTokens = state.PaidExtraTokens,
            paidExtraCostSek = state.PaidExtraCostSek,
            needsDecision = state.RequiresDecision,
            paidMode = state.IsPaidMode
        };

        // =========================
        // ViewModel
        // =========================
        public class AiViewModel
        {
            public string SelectedDataSourceKey { get; set; } = "";
            public string SelectedDataSourceName { get; set; } = "";
            public bool IsAdmin { get; set; }
            public AiDataSourceInfo? DataSourceInfo { get; set; }
            public List<AiDataSourceInfo> DataSources { get; set; } = new();
        }

        // =========================
        // Manual SQL DTOs
        // =========================
        public sealed class ManualSqlRequest
        {
            public string? Sql { get; set; }
        }

        public sealed class ManualSqlResponse
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? ErrorCode { get; set; }
            public bool CanRetry { get; set; }
            public List<string> Columns { get; set; } = new();
            public List<List<object?>> Rows { get; set; } = new();
            public int RowCount { get; set; }
            public bool Truncated { get; set; }
            public string? ExecutedSql { get; set; }
            public string? Message { get; set; }
        }
    }
}
