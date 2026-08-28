using System.Data;
using System.Net;
using Dapper;
using Entities.Mail;
using LoggerService;
using MailService;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.NotifyMe;

public sealed class PortalNotifyMeExecutionService : INotifyMeExecutionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMailManager _mailManager;
    private readonly INotifyMeSourceQueryRunner _sourceQueryRunner;
    private readonly INotifyMeMailRenderer _mailRenderer;
    private readonly INotifyMeRetryPolicy _retryPolicy;
    private readonly ILoggerManager _logger;

    public PortalNotifyMeExecutionService(
        ApplicationDbContext dbContext,
        IMailManager mailManager,
        INotifyMeSourceQueryRunner sourceQueryRunner,
        INotifyMeMailRenderer mailRenderer,
        INotifyMeRetryPolicy retryPolicy,
        ILoggerManager logger)
    {
        _dbContext = dbContext;
        _mailManager = mailManager;
        _sourceQueryRunner = sourceQueryRunner;
        _mailRenderer = mailRenderer;
        _retryPolicy = retryPolicy;
        _logger = logger;
    }

    public async Task<NotifyMeTestRunResultVm> RunTestNotificationAsync(
        string connectionString,
        int companyCode,
        int notificationId,
        string overrideRecipient,
        CancellationToken cancellationToken = default)
    {
        var outcome = await RunNotificationCoreAsync(
            connectionString,
            companyCode,
            notificationId,
            SplitRecipients(overrideRecipient),
            overrideRecipient,
            true,
            cancellationToken);

        return new NotifyMeTestRunResultVm
        {
            NotificationId = notificationId,
            CompanyCode = companyCode,
            OverrideRecipient = overrideRecipient,
            Subject = outcome.Subject,
            LoggedAt = outcome.LoggedAt,
            LogCreated = outcome.LogCreated,
            MailQueued = outcome.MailQueued,
            MailStatus = outcome.MailStatus
        };
    }

    public Task RunScheduledNotificationAsync(
        string connectionString,
        int companyCode,
        int notificationId,
        CancellationToken cancellationToken = default)
        => RunNotificationCoreAsync(
            connectionString,
            companyCode,
            notificationId,
            recipientsOverride: null,
            recipientSummaryOverride: null,
            isTestRun: false,
            cancellationToken);

    private async Task<PortalNotifyMeExecutionOutcome> RunNotificationCoreAsync(
        string connectionString,
        int companyCode,
        int notificationId,
        IReadOnlyCollection<string>? recipientsOverride,
        string? recipientSummaryOverride,
        bool isTestRun,
        CancellationToken cancellationToken)
    {
        var portalConnection = (SqlConnection)_dbContext.Database.GetDbConnection();
        var shouldClose = portalConnection.State != ConnectionState.Open;
        PortalNotifyMeState? state = null;
        string? subject = null;
        string recipientSummary = string.Empty;

        if (shouldClose)
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            const string stateSql = @"
SELECT
    q_zu_notcenter_nr                AS [NotificationId],
    foretagkod                       AS [CompanyCode],
    ISNULL(q_zu_notcenter_beskrivning, '') AS [Description],
    ISNULL(q_zu_notcenter_varntext, '')    AS [WarningText],
    q_zu_notcenter_kommentar         AS [Comment],
    q_zu_notcenter_typ               AS [TypeCode],
    q_zu_notcenter_prio              AS [PriorityCode],
    q_zu_notcenter_mailadress1       AS [PrimaryEmail],
    q_zu_notcenter_mailadress2       AS [SecondaryEmail],
    q_zu_notcenter_cc                AS [Cc],
    q_zu_notcenter_bcc               AS [Bcc],
    q_zu_notcenter_schema            AS [SchemaCode],
    q_zu_notcenter_schedule          AS [ScheduleCode],
    q_zu_notcenter_startdat          AS [StartDate],
    q_zu_notcenter_execdat           AS [NextExecutionAt],
    q_zu_notcenter_varndat           AS [LastWarningAt],
    ISNULL(q_zu_notcenter_antvarning, 0) AS [WarningCount],
    q_zu_notcenter_antal_eskalera    AS [EscalateAfterCount],
    q_zu_notcenter_email_eskalera    AS [EscalationEmail],
    q_zu_notcenter_in_use            AS [IsActiveCode],
    q_zu_notcenter_select2           AS [SqlPreview],
    q_zu_notcenter_sysl              AS [SysChangeSource],
    q_zu_notcenter_dyn_adress        AS [DynamicAddress]
FROM dbo.q_zu_notcenter
WHERE foretagkod = @CompanyCode
  AND q_zu_notcenter_nr = @NotificationId";

            state = await portalConnection.QueryFirstOrDefaultAsync<PortalNotifyMeState>(
                new CommandDefinition(stateSql, new { CompanyCode = companyCode, NotificationId = notificationId }, cancellationToken: cancellationToken));

            if (state == null)
                throw new InvalidOperationException("Notifieringen hittades inte i portalens NotifyMe-tabeller.");

            if (string.IsNullOrWhiteSpace(state.SqlPreview))
                throw new InvalidOperationException("Notifieringen saknar SQL-underlag och kan inte testköras.");

            subject = string.IsNullOrWhiteSpace(state.WarningText)
                ? $"NotifyMe {state.NotificationId}"
                : state.WarningText;
            var configuredRecipients = recipientsOverride ?? ResolveConfiguredRecipients(state);
            recipientSummary = string.IsNullOrWhiteSpace(recipientSummaryOverride)
                ? string.Join("; ", configuredRecipients)
                : recipientSummaryOverride;

            var rows = await _sourceQueryRunner.ExecuteAsync(connectionString, state.SqlPreview, cancellationToken);
            if (rows.Count == 0)
            {
                var noHitLoggedAt = DateTime.UtcNow;
                var noHitNextExecutionAt = NotifyMeScheduleCalculator.CalculateNextExecution(
                    noHitLoggedAt,
                    state.StartDate,
                    state.SchemaCode,
                    state.ScheduleCode);
                var noHitMail = NotifyMeNoHitTestMailComposer.Compose(subject);
                var noHitMailQueued = false;

                if (isTestRun)
                {
                    var noHitRecipients = configuredRecipients;
                    if (noHitRecipients.Count == 0)
                        throw new InvalidOperationException("Notifieringen saknar mottagare.");

                    await _mailManager.SendNotificationMailAsync(
                        new MailModel
                        {
                            Subject = noHitMail.Subject,
                            To = noHitRecipients.First(),
                            Header = state.Description,
                            Text = state.Comment,
                            ErrorMessage = string.Empty
                        },
                        htmlOverride: noHitMail.Html,
                        toRecipients: noHitRecipients,
                        ccRecipients: Array.Empty<string>(),
                        bccRecipients: Array.Empty<string>());
                    noHitMailQueued = true;
                }

                await using var noHitTransaction = await portalConnection.BeginTransactionAsync(cancellationToken);
                await InsertExecutionLogAsync(
                    portalConnection,
                    (SqlTransaction)noHitTransaction,
                    companyCode,
                    notificationId,
                    noHitLoggedAt,
                    state,
                    recipientSummary,
                    noHitNextExecutionAt,
                    noHitMail.Subject,
                    isTestRun ? noHitMail.Html : "<p>Ingen träff i källdatan. Ingen varning skickades.</p>",
                    incrementedWarningCount: state.WarningCount);
                if (!isTestRun)
                {
                    await UpdateNotificationRuntimeStateAsync(
                        portalConnection,
                        (SqlTransaction)noHitTransaction,
                        companyCode,
                        notificationId,
                        noHitLoggedAt,
                        noHitNextExecutionAt,
                        incrementWarningCount: false);
                }
                await noHitTransaction.CommitAsync(cancellationToken);

                return new PortalNotifyMeExecutionOutcome
                {
                    Subject = subject,
                    LoggedAt = noHitLoggedAt,
                    LogCreated = true,
                    MailQueued = noHitMailQueued,
                    MailStatus = isTestRun
                        ? "Ingen träff i källdatan. Portalen skickade ett testmail som bekräftelse."
                        : "Ingen träff i källdatan. Ingen varning skickades."
                };
            }

            if (IsDynamicAddress(state) && !isTestRun)
            {
                return await RunDynamicRecipientNotificationAsync(
                    portalConnection,
                    companyCode,
                    notificationId,
                    state,
                    rows,
                    subject,
                    cancellationToken);
            }

            var html = _mailRenderer.BuildHtml(state, rows);
            var loggedAt = DateTime.UtcNow;
            var nextExecutionAt = NotifyMeScheduleCalculator.CalculateNextExecution(
                loggedAt,
                state.StartDate,
                state.SchemaCode,
                state.ScheduleCode);
            var toRecipients = configuredRecipients;
            if (toRecipients.Count == 0)
                throw new InvalidOperationException("Notifieringen saknar mottagare.");

            var ccRecipients = SplitRecipients(state.Cc);
            var bccRecipients = ResolveBccRecipients(state);
            await _mailManager.SendNotificationMailAsync(
                new MailModel
                {
                    Subject = subject,
                    To = toRecipients.First(),
                    Header = state.Description,
                    Text = state.Comment,
                    ErrorMessage = string.Empty
                },
                htmlOverride: html,
                toRecipients: toRecipients,
                ccRecipients: ccRecipients,
                bccRecipients: bccRecipients);

            await using var transaction = await portalConnection.BeginTransactionAsync(cancellationToken);
            await InsertExecutionLogAsync(
                portalConnection,
                (SqlTransaction)transaction,
                companyCode,
                notificationId,
                loggedAt,
                state,
                recipientSummary,
                nextExecutionAt,
                subject,
                html,
                incrementedWarningCount: isTestRun ? state.WarningCount : state.WarningCount + 1);
            if (!isTestRun)
            {
                await UpdateNotificationRuntimeStateAsync(
                    portalConnection,
                    (SqlTransaction)transaction,
                    companyCode,
                    notificationId,
                    loggedAt,
                    nextExecutionAt,
                    incrementWarningCount: true);
            }

            await transaction.CommitAsync(cancellationToken);

            return new PortalNotifyMeExecutionOutcome
            {
                Subject = state.WarningText,
                LoggedAt = loggedAt,
                LogCreated = true,
                MailQueued = true,
                MailStatus = isTestRun
                    ? "Portalen skickade testmailet och sparade körhistoriken."
                    : "Portalen skickade notifieringen och sparade körhistoriken."
            };
        }
        catch (Exception ex) when (state != null)
        {
            var failedOutcome = await HandleFailedExecutionAsync(
                portalConnection,
                companyCode,
                notificationId,
                state,
                subject,
                recipientSummary,
                isTestRun,
                ex,
                cancellationToken);

            if (isTestRun)
                throw new InvalidOperationException(failedOutcome.MailStatus ?? ex.Message, ex);

            return failedOutcome;
        }
        finally
        {
            if (shouldClose)
                await _dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task<PortalNotifyMeExecutionOutcome> HandleFailedExecutionAsync(
        SqlConnection portalConnection,
        int companyCode,
        int notificationId,
        PortalNotifyMeState state,
        string? subject,
        string recipientSummary,
        bool isTestRun,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var failedAt = DateTime.UtcNow;
        var effectiveSubject = string.IsNullOrWhiteSpace(subject)
            ? $"NotifyMe {notificationId}"
            : subject;
        var normalNextExecutionAt = NotifyMeScheduleCalculator.CalculateNextExecution(
            failedAt,
            state.StartDate,
            state.SchemaCode,
            state.ScheduleCode);
        var encodedMessage = WebUtility.HtmlEncode(exception.Message);

        if (isTestRun)
        {
            await using var testFailureTransaction = await portalConnection.BeginTransactionAsync(cancellationToken);
            await InsertExecutionLogAsync(
                portalConnection,
                (SqlTransaction)testFailureTransaction,
                companyCode,
                notificationId,
                failedAt,
                state,
                string.IsNullOrWhiteSpace(recipientSummary) ? "(saknar mottagare)" : recipientSummary,
                normalNextExecutionAt,
                $"[Misslyckad] {effectiveSubject}",
                $"<p>{encodedMessage}</p>",
                incrementedWarningCount: state.WarningCount);
            await testFailureTransaction.CommitAsync(cancellationToken);

            return new PortalNotifyMeExecutionOutcome
            {
                Subject = effectiveSubject,
                LoggedAt = failedAt,
                LogCreated = true,
                MailQueued = false,
                MailStatus = $"Testkörningen misslyckades: {exception.Message}"
            };
        }

        var retryable = _retryPolicy.IsRetryable(exception);
        var existingConsecutiveFailures = await GetConsecutiveFailureCountAsync(
            portalConnection,
            companyCode,
            notificationId,
            cancellationToken);
        var retryAttempt = existingConsecutiveFailures + 1;
        var hasRetriesLeft = retryable && retryAttempt <= _retryPolicy.MaxAttempts;

        if (hasRetriesLeft)
        {
            var retryAt = _retryPolicy.CalculateRetryAt(failedAt, retryAttempt);
            var retryAtLocal = NotifyMeTimeZoneHelper.ToStockholmTime(retryAt).ToString("yyyy-MM-dd HH:mm");

            await using var retryTransaction = await portalConnection.BeginTransactionAsync(cancellationToken);
            await InsertExecutionLogAsync(
                portalConnection,
                (SqlTransaction)retryTransaction,
                companyCode,
                notificationId,
                failedAt,
                state,
                string.IsNullOrWhiteSpace(recipientSummary) ? "(saknar mottagare)" : recipientSummary,
                retryAt,
                $"[Misslyckad][Retry {retryAttempt}/{_retryPolicy.MaxAttempts}] {effectiveSubject}",
                $"<p>{encodedMessage}</p><p>Nytt automatiskt försök är planerat till {retryAtLocal}.</p>",
                incrementedWarningCount: state.WarningCount);
            await UpdateNotificationRuntimeStateAsync(
                portalConnection,
                (SqlTransaction)retryTransaction,
                companyCode,
                notificationId,
                failedAt,
                retryAt,
                incrementWarningCount: false,
                cancellationToken: cancellationToken);
            await retryTransaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                $"NotifyMe scheduled notification {notificationId} for company {companyCode} failed and will retry {retryAttempt}/{_retryPolicy.MaxAttempts} at {retryAt:o}. Error: {exception.Message}");

            return new PortalNotifyMeExecutionOutcome
            {
                Subject = effectiveSubject,
                LoggedAt = failedAt,
                LogCreated = true,
                MailQueued = false,
                MailStatus = $"Tekniskt fel. Nytt försök {retryAttempt}/{_retryPolicy.MaxAttempts} planerat {retryAtLocal}."
            };
        }

        await using var failedTransaction = await portalConnection.BeginTransactionAsync(cancellationToken);
        await InsertExecutionLogAsync(
            portalConnection,
            (SqlTransaction)failedTransaction,
            companyCode,
            notificationId,
            failedAt,
            state,
            string.IsNullOrWhiteSpace(recipientSummary) ? "(saknar mottagare)" : recipientSummary,
            normalNextExecutionAt,
            $"[Misslyckad][Manuell åtgärd] {effectiveSubject}",
            retryable
                ? $"<p>{encodedMessage}</p><p>Automatiska retries är uttömda. Manuell åtgärd krävs innan nästa ordinarie körning.</p>"
                : $"<p>{encodedMessage}</p><p>Felet bedömdes inte som retrybart. Manuell åtgärd krävs.</p>",
            incrementedWarningCount: state.WarningCount);
        await UpdateNotificationRuntimeStateAsync(
            portalConnection,
            (SqlTransaction)failedTransaction,
            companyCode,
            notificationId,
            failedAt,
            normalNextExecutionAt,
            incrementWarningCount: false,
            cancellationToken: cancellationToken);
        await failedTransaction.CommitAsync(cancellationToken);

        _logger.LogError(
            $"NotifyMe scheduled notification {notificationId} for company {companyCode} requires manual action after failed execution. Error: {exception.Message}");

        return new PortalNotifyMeExecutionOutcome
        {
            Subject = effectiveSubject,
            LoggedAt = failedAt,
            LogCreated = true,
            MailQueued = false,
            MailStatus = "Automatiska retries är uttömda. Notifieringen kräver manuell åtgärd."
        };
    }

    private async Task<PortalNotifyMeExecutionOutcome> RunDynamicRecipientNotificationAsync(
        SqlConnection portalConnection,
        int companyCode,
        int notificationId,
        PortalNotifyMeState state,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string subject,
        CancellationToken cancellationToken)
    {
        var batches = NotifyMeDynamicRecipientGrouper.GroupByRecipient(rows);
        if (batches.Count == 0)
            throw new InvalidOperationException("Dynamiska mottagare gav inga mottagargrupper.");

        var loggedAt = DateTime.UtcNow;
        var nextExecutionAt = NotifyMeScheduleCalculator.CalculateNextExecution(
            loggedAt,
            state.StartDate,
            state.SchemaCode,
            state.ScheduleCode);
        var ccRecipients = SplitRecipients(state.Cc);
        var bccRecipients = ResolveBccRecipients(state);
        var sentLogs = new List<(string RecipientSummary, string Html)>();

        foreach (var batch in batches)
        {
            var toRecipients = SplitRecipients(batch.Recipient);
            if (toRecipients.Count == 0)
                throw new InvalidOperationException("Dynamiska mottagare innehåller en tom eller ogiltig mottagare.");

            var html = _mailRenderer.BuildHtml(state, batch.Rows);
            var recipientSummary = string.Join("; ", toRecipients);

            await _mailManager.SendNotificationMailAsync(
                new MailModel
                {
                    Subject = subject,
                    To = toRecipients.First(),
                    Header = state.Description,
                    Text = state.Comment,
                    ErrorMessage = string.Empty
                },
                htmlOverride: html,
                toRecipients: toRecipients,
                ccRecipients: ccRecipients,
                bccRecipients: bccRecipients);

            sentLogs.Add((recipientSummary, html));
        }

        await using var transaction = await portalConnection.BeginTransactionAsync(cancellationToken);
        for (var i = 0; i < sentLogs.Count; i++)
        {
            var sentLog = sentLogs[i];
            await InsertExecutionLogAsync(
                portalConnection,
                (SqlTransaction)transaction,
                companyCode,
                notificationId,
                loggedAt,
                state,
                sentLog.RecipientSummary,
                nextExecutionAt,
                subject,
                sentLog.Html,
                incrementedWarningCount: state.WarningCount + i + 1,
                cancellationToken);
        }

        await UpdateNotificationRuntimeStateAsync(
            portalConnection,
            (SqlTransaction)transaction,
            companyCode,
            notificationId,
            loggedAt,
            nextExecutionAt,
            warningIncrement: sentLogs.Count,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PortalNotifyMeExecutionOutcome
        {
            Subject = subject,
            LoggedAt = loggedAt,
            LogCreated = true,
            MailQueued = true,
            MailStatus = $"Portalen skickade {sentLogs.Count} dynamiska NotifyMe-mail och sparade körhistoriken."
        };
    }

    private static async Task<int> GetConsecutiveFailureCountAsync(
        SqlConnection portalConnection,
        int companyCode,
        int notificationId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT TOP (10)
    ISNULL(q_zu_notcenter_subject, '') AS [Subject]
FROM dbo.q_zu_notcenter_log
WHERE foretagkod = @CompanyCode
  AND q_zu_notcenter_nr = @NotificationId
ORDER BY regdat DESC, q_zu_notcenter_log_id DESC";

        var subjects = await portalConnection.QueryAsync<string>(
            new CommandDefinition(
                sql,
                new { CompanyCode = companyCode, NotificationId = notificationId },
                cancellationToken: cancellationToken));

        var count = 0;
        foreach (var subject in subjects)
        {
            if (!subject.StartsWith("[Misslyckad]", StringComparison.OrdinalIgnoreCase))
                break;

            count++;
        }

        return count;
    }

    private static IReadOnlyCollection<string> SplitRecipients(string? raw)
    {
        return (raw ?? string.Empty)
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyCollection<string> ResolveConfiguredRecipients(PortalNotifyMeState state)
    {
        return SplitRecipients(string.Join(";", new[] { state.PrimaryEmail, state.SecondaryEmail }));
    }

    private static IReadOnlyCollection<string> ResolveBccRecipients(PortalNotifyMeState state)
    {
        var recipients = SplitRecipients(state.Bcc).ToList();
        if (ShouldEscalate(state))
            recipients.AddRange(SplitRecipients(state.EscalationEmail));

        return recipients
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool ShouldEscalate(PortalNotifyMeState state)
    {
        var escalateAfterCount = state.EscalateAfterCount.GetValueOrDefault();
        return escalateAfterCount > 0
            && state.WarningCount >= escalateAfterCount;
    }

    private static bool IsDynamicAddress(PortalNotifyMeState state)
    {
        return string.Equals(state.DynamicAddress, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static Task InsertExecutionLogAsync(
        SqlConnection portalConnection,
        SqlTransaction transaction,
        int companyCode,
        int notificationId,
        DateTime loggedAt,
        PortalNotifyMeState state,
        string recipientSummary,
        DateTime nextExecutionAt,
        string subject,
        string html,
        int incrementedWarningCount,
        CancellationToken cancellationToken = default)
    {
        const string insertLogSql = @"
INSERT INTO dbo.q_zu_notcenter_log (
    foretagkod,
    q_zu_notcenter_nr,
    regdat,
    q_zu_notcenter_beskrivning,
    q_zu_notcenter_typ,
    q_zu_notcenter_prio,
    q_zu_notcenter_varntext,
    q_zu_notcenter_kommentar,
    q_zu_notcenter_mailadress1,
    q_zu_notcenter_mailadress2,
    q_zu_notcenter_cc,
    q_zu_notcenter_bcc,
    q_zu_notcenter_schema,
    q_zu_notcenter_in_use,
    q_zu_notcenter_antvarning,
    q_zu_notcenter_execdat,
    q_zu_notcenter_varndat,
    q_zu_notcenter_select2,
    q_zu_notcenter_sysl,
    q_zu_notcenter_startdat,
    q_zu_notcenter_schedule,
    q_zu_notcenter_recipients,
    q_zu_notcenter_subject,
    q_zu_notcenter_html
)
VALUES (
    @CompanyCode,
    @NotificationId,
    @LoggedAt,
    @Description,
    @TypeCode,
    @PriorityCode,
    @WarningText,
    @Comment,
    @RecipientSummary,
    NULL,
    NULL,
    NULL,
    @SchemaCode,
    @IsActiveCode,
    @WarningCount,
    @NextExecutionAt,
    @LoggedAt,
    @SqlPreview,
    @SysChangeSource,
    @StartDate,
    @ScheduleCode,
    @RecipientSummary,
    @Subject,
    @Html
)";

        return portalConnection.ExecuteAsync(
            new CommandDefinition(
                insertLogSql,
                new
                {
                    CompanyCode = companyCode,
                    NotificationId = notificationId,
                    LoggedAt = loggedAt,
                    state.Description,
                    state.TypeCode,
                    state.PriorityCode,
                    state.WarningText,
                    state.Comment,
                    RecipientSummary = recipientSummary,
                    state.SchemaCode,
                    state.IsActiveCode,
                    WarningCount = incrementedWarningCount,
                    NextExecutionAt = nextExecutionAt,
                    state.SqlPreview,
                    state.SysChangeSource,
                    state.StartDate,
                    state.ScheduleCode,
                    Subject = subject,
                    Html = html
                },
                transaction: transaction,
                cancellationToken: cancellationToken));
    }

    private static Task UpdateNotificationRuntimeStateAsync(
        SqlConnection portalConnection,
        SqlTransaction transaction,
        int companyCode,
        int notificationId,
        DateTime loggedAt,
        DateTime nextExecutionAt,
        bool incrementWarningCount,
        CancellationToken cancellationToken = default)
        => UpdateNotificationRuntimeStateAsync(
            portalConnection,
            transaction,
            companyCode,
            notificationId,
            loggedAt,
            nextExecutionAt,
            incrementWarningCount ? 1 : 0,
            cancellationToken);

    private static Task UpdateNotificationRuntimeStateAsync(
        SqlConnection portalConnection,
        SqlTransaction transaction,
        int companyCode,
        int notificationId,
        DateTime loggedAt,
        DateTime nextExecutionAt,
        int warningIncrement,
        CancellationToken cancellationToken = default)
    {
        var updateStateSql = warningIncrement > 0
            ? @"
UPDATE dbo.q_zu_notcenter
SET q_zu_notcenter_antvarning = ISNULL(q_zu_notcenter_antvarning, 0) + @WarningIncrement,
    q_zu_notcenter_varndat = @LoggedAt,
    q_zu_notcenter_execdat = @NextExecutionAt,
    rowupdatedby = 'TRG',
    rowupdateddt = @LoggedAt
WHERE foretagkod = @CompanyCode
  AND q_zu_notcenter_nr = @NotificationId"
            : @"
UPDATE dbo.q_zu_notcenter
SET q_zu_notcenter_execdat = @NextExecutionAt,
    rowupdatedby = 'TRG',
    rowupdateddt = @LoggedAt
WHERE foretagkod = @CompanyCode
  AND q_zu_notcenter_nr = @NotificationId";

        return portalConnection.ExecuteAsync(
            new CommandDefinition(
                updateStateSql,
                new { CompanyCode = companyCode, NotificationId = notificationId, LoggedAt = loggedAt, NextExecutionAt = nextExecutionAt, WarningIncrement = warningIncrement },
                transaction: transaction,
                cancellationToken: cancellationToken));
    }

}
