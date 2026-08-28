using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using WebApp.Models.AI;
using WebApp.Services.Application;          // IOpenAiChatService + OpenAiChatMessage
using WebApp.Services.Application.AI;       // resolver + sql executor
using WebApp.Services.Invoices;

namespace WebApp.Services.Application.AI;

/// <summary>
/// Orchestrates the end-to-end AI database flow.
/// This class controls routing (portal vs DB), schema loading/focusing, SQL generation calls,
/// execution retries/fallbacks, conversation memory, and final response shaping.
/// </summary>
public sealed partial class AiDbChatOrchestrator : IAiDbChatOrchestrator
{
    private const string AiConversationUserKeySessionKey = "AI_CONV_USER_KEY";
    private const string PendingMetricQuestionSessionPrefix = "AI_PENDING_METRIC_";

    private readonly IAiDataSourceResolver _resolver;
    private readonly IAiSqlExecutor _sql;
    private readonly IOpenAiChatService _chat;
    private readonly IHostEnvironment _env;
    private readonly IHttpContextAccessor _http;
    private readonly IAiConversationMemory _memory;
    private readonly IAiInvoiceQuestionService _invoiceQuestionService;
    private readonly IAiSemanticCatalog _semanticCatalog;
    private readonly IAiSqlSecurityPolicy _sqlSecurityPolicy;
    private readonly IAiResultVerifier _resultVerifier;
    private readonly IAiPromptDataPolicy _promptDataPolicy;

    private static readonly ConcurrentDictionary<string, (string SchemaText, DateTime CacheTime)> _schemaCache = new();
    private const int CacheDurationMinutes = 60;

    // ✅ Pinna tabeller/vyer vi vet används för AI-frågor.
    private static readonly string[] PinnedTables =
    {
        "dbo.oh",   // Order header
        "dbo.orp",  // Order lines
        "dbo.fr",   // Customers
        "dbo.kus",  // Customer credit limits
        "dbo.ar",   // Items
        "dbo.ft",   // Invoices
        "dbo.salesfact",   // Sales fact
        "dbo.dim_product",  // Product dimension
        // BI facts
        "dbo.q_zu_bi_fsg",                 // Fakturering
        "dbo.q_zu_bi_fsg_ord",             // Orderingang
        "dbo.q_zu_bi_brd",                 // Budget
        "dbo.q_zu_bi_vr",                  // Verifikationsrader
        "dbo.q_zu_bi_hr",                  // Planerade aktiviteter
        "dbo.q_zu_bi_hrp",                 // Rapporterade aktiviteter
        "dbo.q_zu_bi_ao",                  // Arbetsoperationer
        "dbo.q_zu_bi_mr",                  // Materialreservationer
        "dbo.q_zu_bi_ti",                  // Delorder, tillverkning
        "dbo.q_zu_bi_arla",                // Artikeluppgifter/lagerstalle
        "dbo.q_zu_bi_bpi",                 // Inleveranser, bestallningsrad
        "dbo.q_zu_bi_fin_arsm",            // Manuella lagertransaktioner
        "dbo.q_zu_bi_fin_kpi",             // Nyckeltal
        "dbo.q_zu_bi_fin_likvprognos",     // Likviditetsprognos
        "dbo.q_zu_bi_fin_prisdiff",        // Prisdifferenser inkop
        "dbo.q_zu_bi_fin_thvt",            // Kalkyldifferenser produktion
        "dbo.q_zu_bi_ork",                 // Kundreklamationer
        "dbo.q_zu_bi_oru",                 // Utplock mot kundorder

        // BI dimensions
        "dbo.q_zu_bi_company",             // Bolag
        "dbo.q_zu_bi_customer",            // Kund
        "dbo.q_zu_bi_item",                // Artikel
        "dbo.q_zu_bi_salesperson",         // Saljare
        "dbo.q_zu_bi_supplier",            // Leverantor
        "dbo.q_zu_bi_kb",                  // Kostnadsbarare
        "dbo.q_zu_bi_ko",                  // Konton
        "dbo.q_zu_bi_kt",                  // Kostnadsstalle
        "dbo.q_zu_bi_prj"                  // Projekt
    };

    public AiDbChatOrchestrator(
        IAiDataSourceResolver resolver,
        IAiSqlExecutor sql,
        IOpenAiChatService chat,
        IHostEnvironment env,
        IHttpContextAccessor http,
        IAiConversationMemory memory,
        IAiInvoiceQuestionService invoiceQuestionService,
        IAiSemanticCatalog semanticCatalog,
        IAiSqlSecurityPolicy sqlSecurityPolicy,
        IAiResultVerifier resultVerifier,
        IAiPromptDataPolicy promptDataPolicy)
    {
        _resolver = resolver;
        _sql = sql;
        _chat = chat;
        _env = env;
        _http = http;
        _memory = memory;
        _invoiceQuestionService = invoiceQuestionService;
        _semanticCatalog = semanticCatalog;
        _sqlSecurityPolicy = sqlSecurityPolicy;
        _resultVerifier = resultVerifier;
        _promptDataPolicy = promptDataPolicy;
    }

    public async Task<AiQueryResponse> AskDatabaseAsync(
        AiQueryRequest request,
        AiProgressCallback? progress = null,
        CancellationToken ct = default)
    {
        request ??= new AiQueryRequest();
        var tokenUsage = new TokenUsageTotals();
        var totalTimer = Stopwatch.StartNew();
        var diagnostics = new AiExecutionDiagnostics();
        AiQueryResponse WithUsage(AiQueryResponse response)
        {
            response.PromptTokens = tokenUsage.PromptTokens > 0 ? tokenUsage.PromptTokens : null;
            response.CompletionTokens = tokenUsage.CompletionTokens > 0 ? tokenUsage.CompletionTokens : null;
            response.TotalTokens = tokenUsage.TotalTokens > 0 ? tokenUsage.TotalTokens : null;
            response.InputCostSek = AiTokenPricing.CalculateInputCostSek(response.PromptTokens);
            response.OutputCostSek = AiTokenPricing.CalculateOutputCostSek(response.CompletionTokens);
            response.TotalCostSek = AiTokenPricing.CalculateTotalCostSek(response.PromptTokens, response.CompletionTokens, response.TotalTokens);
            diagnostics.TotalDurationMs = totalTimer.ElapsedMilliseconds;
            diagnostics.ModelDeployment = tokenUsage.ModelDeployment;
            diagnostics.ModelRetryCount = tokenUsage.RetryCount;
            response.Diagnostics = diagnostics;
            return response;
        }

        var question = (request.Question ?? string.Empty).Trim();
        var source = (request.Source ?? string.Empty).Trim().ToLowerInvariant();
        var isDashboard = source == "dashboard";
        var isAssistant = source == "assistant";
        var isIntelligence = source == "intelligence";
        var includeSqlInResponse = !(isDashboard || isAssistant);

        if (string.IsNullOrWhiteSpace(question))
            return WithUsage(new AiQueryResponse
            {
                Success = false,
                Answer = "Skriv en fråga först.",
                ErrorMessage = "Tom fråga."
            });

        var requestedKey = request.DataSourceKey;
        var dsKeyForPortal = string.IsNullOrWhiteSpace(requestedKey) ? "default" : requestedKey.Trim();
        await ReportProgressAsync(progress, "routing", "Tolkar frågan och väljer analysväg", 34, ct);

        if (!string.IsNullOrWhiteSpace(request.RuntimeConnectionString))
        {
            var invoiceResponse = await _invoiceQuestionService.TryAnswerAsync(
                question,
                request.RuntimeConnectionString,
                request.CompanyCode,
                ct);

            if (invoiceResponse is not null)
            {
                return WithUsage(invoiceResponse);
            }
        }

        // -------------------------
        // Router: portal/help questions (no DB needed)
        // -------------------------
        // - ZeeU Intelligence: always treat as data/SQL (no portal-help routing).
        // - Dashboard/Assistant: default to portal-help, unless it clearly looks like a data question.
        var looksLikeFact = LooksLikeFactQuestion(question);
        if (!isIntelligence && ((isDashboard || isAssistant) ? !looksLikeFact : (LooksLikePortalHelpQuestion(question) && !looksLikeFact)))
        {
            await ReportProgressAsync(progress, "answering", "Söker i ZeeU-kunskapen", 68, ct);
            var portalMemoryKey = GetConversationKey("portal", dsKeyForPortal, request.CompanyCode);
            var portalAnswer = await AnswerPortalQuestionAsync(question, portalMemoryKey, tokenUsage, ct);
            _memory.AppendTurn(portalMemoryKey, question, portalAnswer);
            return WithUsage(new AiQueryResponse
            {
                Answer = portalAnswer,
                Sql = "",
                Warning = null
            });
        }

        var (conn, info) = await _resolver.ResolveAsync(string.IsNullOrWhiteSpace(requestedKey) ? null : requestedKey.Trim(), ct);
        var dbMemoryKey = GetConversationKey("db", info.Key, request.CompanyCode);
        var resolvedQuestion = ExpandClarificationQuestion(question, info.Key, request.CompanyCode);
        resolvedQuestion = ExpandBreakdownFollowUpQuestion(resolvedQuestion, dbMemoryKey);

        if (string.IsNullOrWhiteSpace(conn))
        {
            return WithUsage(new AiQueryResponse
            {
                Success = false,
                Answer = "Jag hittar ingen anslutningssträng för vald datakälla. Kolla Ai:DataSources i docker/appsettings.",
                Sql = "",
                Warning = "Ingen anslutningssträng för vald datakälla.",
                ErrorMessage = "Ingen anslutningssträng för vald datakälla."
            });
        }

        await ReportProgressAsync(progress, "schema", "Läser tillgängliga tabeller och fält", 44, ct);
        var cacheKey = $"{info.Key}:company:{request.CompanyCode?.ToString() ?? "none"}";
        var schema = await LoadSchemaAsync(conn, cacheKey, ct);

        if (!schema.Success)
        {
            return WithUsage(new AiQueryResponse
            {
                Success = false,
                Answer = $"Kunde inte läsa schema från databasen: {schema.Error}",
                Sql = "",
                Warning = schema.Error,
                ErrorMessage = schema.Error
            });
        }

        Task<AiQueryResponse?> TryDeterministicFallbackAsync(string? excludedSql = null)
        {
            return TryExecuteDeterministicFallbackAsync(
                resolvedQuestion,
                question,
                schema.SchemaText,
                conn,
                info,
                dbMemoryKey,
                includeSqlInResponse,
                isDashboard || isIntelligence,
                excludedSql,
                progress,
                diagnostics,
                tokenUsage,
                ct);
        }

        // Run the small set of known, schema-compatible business questions before the
        // generative planner. This keeps common portal questions inexpensive while the
        // normal plan/repair flow remains available whenever a template cannot answer.
        if (ShouldUseDeterministicFastPath(resolvedQuestion))
        {
            await ReportProgressAsync(progress, "planning", "Matchar frågan mot en verifierad analysmall", 58, ct);
            var fastPathResponse = await TryDeterministicFallbackAsync();
            if (fastPathResponse is not null)
                return WithUsage(fastPathResponse);
        }

        await ReportProgressAsync(progress, "planning", "Skapar och validerar frågeplanen", 64, ct);
        var planningTimer = Stopwatch.StartNew();
        var sqlDraftResult = await GenerateSqlAsync(resolvedQuestion, schema.SchemaText!, request.CompanyCode, info.Key, info.DataProfile, dbMemoryKey, tokenUsage, ct);
        diagnostics.PlanningDurationMs += planningTimer.ElapsedMilliseconds;
        if (sqlDraftResult.RequiresClarification)
        {
            var clarificationReason = string.IsNullOrWhiteSpace(sqlDraftResult.Reason)
                ? "Jag behöver ett förtydligande för att kunna skapa en korrekt SQL-fråga."
                : sqlDraftResult.Reason!.Trim();
            if (ShouldAskMetricClarification(resolvedQuestion, schema.SchemaText))
            {
                SetPendingMetricQuestion(info.Key, request.CompanyCode, question);
            }

            return WithUsage(new AiQueryResponse
            {
                Success = false,
                Answer = clarificationReason,
                Sql = "",
                Warning = clarificationReason,
                ErrorMessage = "Frågan kräver förtydligande.",
                Plan = sqlDraftResult.Plan
            });
        }

        var sqlDraft = sqlDraftResult.Sql ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sqlDraft))
        {
            var fallbackResponse = await TryDeterministicFallbackAsync();
            if (fallbackResponse is not null)
            {
                return WithUsage(fallbackResponse);
            }

            diagnostics.ErrorCode = "planning_failed";
            return WithUsage(new AiQueryResponse
            {
                Success = false,
                Answer = "Jag kunde inte skapa en databasfråga från analysplanen.",
                Sql = "",
                Warning = "Kunde inte generera SQL.",
                ErrorMessage = "Kunde inte generera SQL.",
                Plan = sqlDraftResult.Plan,
                Error = new AiQueryError
                {
                    Code = "planning_failed",
                    Title = "Frågan kunde inte planeras",
                    Message = "Jag kunde inte skapa en databasfråga för den här formuleringen. Försök igen eller ange vilka uppgifter du vill visa.",
                    CanRetry = true,
                    Tone = "warning"
                }
            });
        }

        var sqlForExec = SanitizeGeneratedSql(sqlDraft, resolvedQuestion);
        var topN = ExtractTopNFromQuestion(resolvedQuestion);
        if (LooksLikeTopCustomersQuestion(resolvedQuestion) && !HasExplicitTopNumber(resolvedQuestion))
        {
            topN = 3;
        }

        var policy = _sqlSecurityPolicy.Validate(sqlForExec);
        SqlQueryResult query;
        string? recoveryWarning = null;
        if (!policy.Success)
        {
            var repaired = await TryRepairAndExecuteSqlAsync(
                resolvedQuestion,
                schema.SchemaText,
                request.CompanyCode,
                sqlForExec,
                policy.Error ?? "Frågan bröt mot datakällegränsen.",
                conn,
                dbMemoryKey,
                progress,
                diagnostics,
                tokenUsage,
                ct);
            if (repaired is not null)
            {
                query = repaired.Query;
                sqlForExec = repaired.Sql;
                sqlDraftResult.Plan = repaired.Plan ?? sqlDraftResult.Plan;
                recoveryWarning = "Frågan justerades automatiskt till den valda databasen.";
            }
            else
            {
                var fallbackResponse = await TryDeterministicFallbackAsync(sqlForExec);
                if (fallbackResponse is not null)
                {
                    return WithUsage(fallbackResponse);
                }

                diagnostics.ErrorCode = policy.ErrorCode;
                return WithUsage(new AiQueryResponse
                {
                    Success = false,
                    Answer = "Analysen försökte läsa utanför den valda databasen.",
                    ErrorMessage = $"{policy.ErrorCode}: {policy.Error}",
                    Plan = sqlDraftResult.Plan,
                    Error = new AiQueryError
                    {
                        Code = "security_policy_blocked",
                        Title = "Frågan gick utanför den valda databasen",
                        Message = "ZeeU Intelligence kan läsa fritt i din valda databas, men inte från andra databaser eller externa datakällor.",
                        CanRetry = true,
                        Tone = "warning"
                    }
                });
            }
        }
        else
        {
            sqlForExec = policy.Sql;
            await ReportProgressAsync(progress, "querying", "Kör den validerade databasfrågan", 78, ct);
            var sqlTimer = Stopwatch.StartNew();
            query = await _sql.ExecuteSelectAsync(conn, sqlForExec, maxRows: 200, ct: ct);
            diagnostics.SqlDurationMs += sqlTimer.ElapsedMilliseconds;
        }

        if (!query.Success)
        {
            if (!string.IsNullOrWhiteSpace(query.Error) &&
                query.Error.IndexOf("Incorrect syntax near the keyword 'TOP'", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var n = topN ?? 1;
                var retrySql = AiSqlSyntaxSanitizer.ForceSqlServerTopAtSelect(sqlDraft, n);
                retrySql = AiSqlSyntaxSanitizer.FixDanglingTopParentheses(retrySql);
                if (!string.Equals(retrySql, sqlForExec, StringComparison.Ordinal))
                {
                    var retryPolicy = _sqlSecurityPolicy.Validate(retrySql);
                    if (retryPolicy.Success)
                    {
                        var retryTimer = Stopwatch.StartNew();
                        query = await _sql.ExecuteSelectAsync(conn, retryPolicy.Sql, maxRows: 200, ct: ct);
                        diagnostics.SqlDurationMs += retryTimer.ElapsedMilliseconds;
                        sqlForExec = query.ExecutedSql ?? retryPolicy.Sql;
                        if (query.Success)
                        {
                            recoveryWarning = "Frågan syntaxkorrigerades automatiskt före körning.";
                        }
                    }
                }
            }

            if (!query.Success)
            {
                var repaired = await TryRepairAndExecuteSqlAsync(
                    resolvedQuestion,
                    schema.SchemaText,
                    request.CompanyCode,
                    query.ExecutedSql ?? sqlForExec,
                    query.Error ?? "Databasen kunde inte köra frågan.",
                    conn,
                    dbMemoryKey,
                    progress,
                    diagnostics,
                    tokenUsage,
                    ct);
                if (repaired is not null)
                {
                    query = repaired.Query;
                    sqlForExec = repaired.Sql;
                    sqlDraftResult.Plan = repaired.Plan ?? sqlDraftResult.Plan;
                    recoveryWarning = "Frågan reparerades automatiskt efter databasens återkoppling.";
                }
            }

            if (!query.Success)
            {
                var fallbackResponse = await TryDeterministicFallbackAsync(sqlForExec);
                if (fallbackResponse is not null)
                {
                    return WithUsage(fallbackResponse);
                }

                diagnostics.ErrorCode = "execution_failed";
                return WithUsage(new AiQueryResponse
                {
                    Success = false,
                    Answer = "Jag kunde inte köra databasfrågan efter ett automatiskt reparationsförsök.",
                    Sql = includeSqlInResponse ? (query.ExecutedSql ?? sqlForExec) : "",
                    Warning = query.Error,
                    ErrorMessage = query.Error,
                    Plan = sqlDraftResult.Plan,
                    Error = new AiQueryError
                    {
                        Code = "execution_failed",
                        Title = "Databasfrågan kunde inte köras",
                        Message = "Jag skapade och försökte reparera databasfrågan, men databasen kunde fortfarande inte köra den.",
                        CanRetry = true,
                        Tone = "warning"
                    }
                });
            }
        }

        var resultContractPlan = sqlDraftResult.Plan ?? _semanticCatalog.CreateFallbackPlan(resolvedQuestion);
        var contractValidation = AiQueryResultContractValidator.Validate(query, resultContractPlan);
        if (!contractValidation.Success)
        {
            await ReportProgressAsync(
                progress,
                "verifying",
                "Kontrollerar att resultatet svarar på hela frågan",
                87,
                ct);
            var repaired = await TryRepairAndExecuteSqlAsync(
                resolvedQuestion,
                schema.SchemaText,
                request.CompanyCode,
                query.ExecutedSql ?? sqlForExec,
                $"RESULT CONTRACT VALIDATION FAILED: {contractValidation.Error}",
                conn,
                dbMemoryKey,
                progress,
                diagnostics,
                tokenUsage,
                ct);

            if (repaired is not null)
            {
                var repairedValidation = AiQueryResultContractValidator.Validate(
                    repaired.Query,
                    resultContractPlan);
                if (repairedValidation.Success)
                {
                    query = repaired.Query;
                    sqlForExec = repaired.Sql;
                    recoveryWarning = "Frågan reparerades eftersom det första resultatet inte uppfyllde analysplanen.";
                }
            }

            contractValidation = AiQueryResultContractValidator.Validate(query, resultContractPlan);
            if (!contractValidation.Success)
            {
                var fallbackResponse = await TryDeterministicFallbackAsync(sqlForExec);
                if (fallbackResponse is not null)
                    return WithUsage(fallbackResponse);

                diagnostics.ErrorCode = "result_contract_failed";
                return WithUsage(new AiQueryResponse
                {
                    Success = false,
                    Answer = "Jag kunde inte ta fram ett resultat som innehåller alla delar som frågan kräver.",
                    Sql = includeSqlInResponse ? (query.ExecutedSql ?? sqlForExec) : "",
                    Warning = contractValidation.Error,
                    ErrorMessage = contractValidation.Error,
                    Plan = resultContractPlan,
                    Error = new AiQueryError
                    {
                        Code = "result_contract_failed",
                        Title = "Resultatet uppfyllde inte analysplanen",
                        Message = "Jag försökte reparera frågan, men resultatet saknade fortfarande obligatoriska mått, perioder eller dimensioner.",
                        CanRetry = true,
                        Tone = "warning"
                    }
                });
            }
        }

        if (query.RowCount == 0)
        {
            var fallbackResponse = await TryDeterministicFallbackAsync(sqlForExec);
            if (fallbackResponse is not null)
            {
                return WithUsage(fallbackResponse);
            }
        }

        await ReportProgressAsync(progress, "summarizing", "Sammanfattar och kvalitetssäkrar svaret", 90, ct);
        var summaryTimer = Stopwatch.StartNew();
        var answer = await SummarizeAsync(resolvedQuestion, query, info, tokenUsage, ct);
        diagnostics.SummaryDurationMs += summaryTimer.ElapsedMilliseconds;
        answer = BuildDeterministicYearToDateComparisonAnswer(query) ?? answer;
        answer = AppendCustomerFollowUpIfNeeded(answer, resolvedQuestion, isDashboard || isIntelligence);
        _memory.AppendTurn(dbMemoryKey, question, answer);
        RememberDatabaseResult(dbMemoryKey, query, sqlDraftResult.Plan);
        var evidence = _resultVerifier.Verify(
            answer,
            query,
            sqlDraftResult.Plan,
            info.Name,
            _semanticCatalog.GetMetricLabel(sqlDraftResult.Plan?.Metric),
            sqlForExec);

        var warning = query.Truncated
            ? "Resultatet trunkerades (max 200 rader). Förfina frågan för att få mer specifikt resultat."
            : null;
        if (!string.IsNullOrWhiteSpace(recoveryWarning))
        {
            warning = string.IsNullOrWhiteSpace(warning)
                ? recoveryWarning
                : $"{recoveryWarning} {warning}";
        }

        // Dashboard wants a clean, short answer; no need to return table/SQL.
        if (isDashboard)
        {
            return WithUsage(new AiQueryResponse
            {
                Answer = answer,
                Sql = "",
                Warning = warning,
                Plan = sqlDraftResult.Plan,
                Evidence = evidence
            });
        }

        return WithUsage(new AiQueryResponse
        {
            Answer = answer,
            Sql = includeSqlInResponse ? (query.ExecutedSql ?? sqlForExec) : "",
            Warning = warning,
            Columns = query.Columns,
            Rows = query.Rows,
            RowCount = query.RowCount,
            Truncated = query.Truncated,
            Plan = sqlDraftResult.Plan,
            Evidence = evidence
        });
    }

    private AiQueryResponse BuildNoDataRankingResponse(
        string answer,
        string question,
        string memoryKey,
        SqlQueryResult query,
        AiQueryPlan plan,
        AiDataSourceInfo dataSource,
        string validatedSql,
        bool includeSqlInResponse)
    {
        _memory.AppendTurn(memoryKey, question, answer);
        RememberDatabaseResult(memoryKey, query, plan);
        var evidence = _resultVerifier.Verify(
            answer,
            query,
            plan,
            dataSource.Name,
            _semanticCatalog.GetMetricLabel(plan.Metric),
            validatedSql);

        return new AiQueryResponse
        {
            Answer = answer,
            Sql = includeSqlInResponse ? (query.ExecutedSql ?? validatedSql) : "",
            Columns = query.Columns,
            Rows = query.Rows,
            RowCount = 0,
            Truncated = false,
            Plan = plan,
            Evidence = evidence
        };
    }

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

    private sealed class TokenUsageTotals
    {
        public int PromptTokens { get; private set; }
        public int CompletionTokens { get; private set; }
        public int TotalTokens { get; private set; }
        public string? ModelDeployment { get; private set; }
        public int RetryCount { get; private set; }

        public void Add(OpenAiChatResult? result)
        {
            if (result == null)
                return;

            PromptTokens += result.PromptTokens ?? 0;
            CompletionTokens += result.CompletionTokens ?? 0;
            TotalTokens += result.TotalTokens ?? 0;
            ModelDeployment ??= result.ModelDeployment;
            RetryCount += result.RetryCount;
        }
    }
}
