// Reads legacy NotifyMe definitions from the active tenant's Jeeves database.
using System.Text.RegularExpressions;
using System.Data.Common;
using Dapper;
using Repository.Execution;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Repositories.NotifyMe;

public sealed class JeevesNotifyMeRepository : INotifyMeRepository
{
    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

    public JeevesNotifyMeRepository(IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public async Task<IReadOnlyList<NotifyMeListItemVm>> GetNotificationsAsync(string connectionString, int companyCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    n.q_zu_notcenter_nr                AS [NotificationId],
    ISNULL(n.q_zu_notcenter_beskrivning, '') AS [Description],
    ISNULL(n.q_zu_notcenter_varntext, '')    AS [WarningText],
    ISNULL(vt.q_zu_notcenter_typbeskr, ISNULL(n.q_zu_notcenter_typ, '')) AS [TypeLabel],
    ISNULL(vk.q_zu_notcenter_typbeskr, ISNULL(n.q_zu_notcenter_prio, '')) AS [PriorityLabel],
    CONCAT(ISNULL(n.q_zu_notcenter_schema, '-'), ' / ', ISNULL(n.q_zu_notcenter_schedule, '-')) AS [ScheduleLabel],
    CASE WHEN n.q_zu_notcenter_in_use IN ('1','J','Y') THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [IsActive],
    n.q_zu_notcenter_execdat           AS [NextExecutionAt],
    n.q_zu_notcenter_varndat           AS [LastWarningAt],
    ISNULL(n.q_zu_notcenter_antvarning, 0) AS [WarningCount],
    n.q_zu_notcenter_antal_eskalera    AS [EscalateAfterCount],
    CASE
        WHEN n.q_zu_notcenter_in_use IN ('1','J','Y')
         AND n.q_zu_notcenter_execdat IS NOT NULL
         AND n.q_zu_notcenter_execdat <= GETDATE() THEN CAST(1 AS bit)
        ELSE CAST(0 AS bit)
    END AS [IsDueNow]
FROM dbo.q_zu_notcenter n
LEFT JOIN dbo.q_zu_notcenter_varningstyp vt ON vt.q_zu_notcenter_typ = n.q_zu_notcenter_typ
LEFT JOIN dbo.q_zu_notcenter_varningskat vk ON vk.q_zu_notcenter_prio = n.q_zu_notcenter_prio
WHERE n.foretagkod = @CompanyCode
ORDER BY n.q_zu_notcenter_in_use DESC, n.q_zu_notcenter_execdat ASC, n.q_zu_notcenter_nr ASC";

        return await _jeevesSqlExecutor.QueryAsync<NotifyMeListItemVm>(
            connectionString,
            sql,
            new { CompanyCode = companyCode },
            operationName: "JeevesNotifyMeRepository.GetNotifications",
            cancellationToken: cancellationToken);
    }

    public async Task<NotifyMeDetailsVm?> GetNotificationAsync(string connectionString, int companyCode, int notificationId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    n.q_zu_notcenter_nr                AS [NotificationId],
    n.foretagkod                       AS [CompanyCode],
    ISNULL(n.q_zu_notcenter_beskrivning, '') AS [Description],
    ISNULL(n.q_zu_notcenter_varntext, '')    AS [WarningText],
    n.q_zu_notcenter_kommentar         AS [Comment],
    ISNULL(n.q_zu_notcenter_typ, '')   AS [TypeCode],
    ISNULL(vt.q_zu_notcenter_typbeskr, ISNULL(n.q_zu_notcenter_typ, '')) AS [TypeLabel],
    ISNULL(n.q_zu_notcenter_prio, '')  AS [PriorityCode],
    ISNULL(vk.q_zu_notcenter_typbeskr, ISNULL(n.q_zu_notcenter_prio, '')) AS [PriorityLabel],
    n.q_zu_notcenter_mailadress1       AS [PrimaryEmail],
    n.q_zu_notcenter_mailadress2       AS [SecondaryEmail],
    n.q_zu_notcenter_cc                AS [Cc],
    n.q_zu_notcenter_bcc               AS [Bcc],
    n.q_zu_notcenter_schema            AS [SchemaCode],
    n.q_zu_notcenter_schedule          AS [ScheduleCode],
    n.q_zu_notcenter_startdat          AS [StartDate],
    n.q_zu_notcenter_execdat           AS [NextExecutionAt],
    n.q_zu_notcenter_varndat           AS [LastWarningAt],
    ISNULL(n.q_zu_notcenter_antvarning, 0) AS [WarningCount],
    n.q_zu_notcenter_antal_eskalera    AS [EscalateAfterCount],
    n.q_zu_notcenter_email_eskalera    AS [EscalationEmail],
    CASE WHEN n.q_zu_notcenter_in_use IN ('1','J','Y') THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [IsActive],
    CASE WHEN ISNULL(n.q_zu_notcenter_sysl, '') <> '' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [UsesSysChangeSource],
    CASE WHEN ISNULL(n.q_zu_notcenter_select2, '') <> '' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [UsesCustomSql],
    n.q_zu_notcenter_select2           AS [SqlPreview],
    n.q_zu_notcenter_sysl              AS [SysChangeSource]
FROM dbo.q_zu_notcenter n
LEFT JOIN dbo.q_zu_notcenter_varningstyp vt ON vt.q_zu_notcenter_typ = n.q_zu_notcenter_typ
LEFT JOIN dbo.q_zu_notcenter_varningskat vk ON vk.q_zu_notcenter_prio = n.q_zu_notcenter_prio
WHERE n.foretagkod = @CompanyCode
  AND n.q_zu_notcenter_nr = @NotificationId";

        return await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<NotifyMeDetailsVm>(
            connectionString,
            sql,
            new { CompanyCode = companyCode, NotificationId = notificationId },
            operationName: "JeevesNotifyMeRepository.GetNotification",
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<NotifyMeLogItemVm>> GetRecentLogEntriesAsync(string connectionString, int companyCode, int? notificationId = null, int take = 15, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP (@Take)
    ROW_NUMBER() OVER (ORDER BY l.regdat DESC) AS [LogId],
    l.q_zu_notcenter_nr                AS [NotificationId],
    ISNULL(l.q_zu_notcenter_beskrivning, '') AS [NotificationDescription],
    l.regdat                           AS [SentAt],
    ISNULL(l.q_zu_notcenter_subject, '') AS [Subject],
    LTRIM(RTRIM(CONCAT(ISNULL(l.q_zu_notcenter_mailadress1, ''),
        CASE WHEN ISNULL(l.q_zu_notcenter_mailadress2, '') = '' THEN '' ELSE '; ' + l.q_zu_notcenter_mailadress2 END,
        CASE WHEN ISNULL(l.q_zu_notcenter_cc, '') = '' THEN '' ELSE ' | CC: ' + l.q_zu_notcenter_cc END,
        CASE WHEN ISNULL(l.q_zu_notcenter_bcc, '') = '' THEN '' ELSE ' | BCC: ' + l.q_zu_notcenter_bcc END))) AS [Recipients],
    ISNULL(l.q_zu_notcenter_schema, '') AS [SchemaCode],
    ISNULL(l.q_zu_notcenter_html, '')  AS [HtmlPreviewText]
FROM dbo.q_zu_notcenter_log l
WHERE l.foretagkod = @CompanyCode
  AND (@NotificationId IS NULL OR l.q_zu_notcenter_nr = @NotificationId)
ORDER BY l.regdat DESC";

        var rows = await _jeevesSqlExecutor.QueryAsync<NotifyMeLogItemVm>(
            connectionString,
            sql,
            new { CompanyCode = companyCode, NotificationId = notificationId, Take = take },
            operationName: "JeevesNotifyMeRepository.GetRecentLogEntries",
            cancellationToken: cancellationToken);

        foreach (var row in rows)
            row.HtmlPreviewText = ToPreviewText(row.HtmlPreviewText);

        return rows;
    }

    public async Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetTypeOptionsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    CONVERT(nvarchar(50), q_zu_notcenter_typ) AS [Value],
    ISNULL(q_zu_notcenter_typbeskr, CONVERT(nvarchar(50), q_zu_notcenter_typ)) AS [Label]
FROM dbo.q_zu_notcenter_varningstyp
ORDER BY q_zu_notcenter_typ";

        return await _jeevesSqlExecutor.QueryAsync<NotifyMeLookupOptionVm>(
            connectionString,
            sql,
            operationName: "JeevesNotifyMeRepository.GetTypeOptions",
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetPriorityOptionsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    CONVERT(nvarchar(50), q_zu_notcenter_prio) AS [Value],
    ISNULL(q_zu_notcenter_typbeskr, CONVERT(nvarchar(50), q_zu_notcenter_prio)) AS [Label]
FROM dbo.q_zu_notcenter_varningskat
ORDER BY q_zu_notcenter_prio";

        return await _jeevesSqlExecutor.QueryAsync<NotifyMeLookupOptionVm>(
            connectionString,
            sql,
            operationName: "JeevesNotifyMeRepository.GetPriorityOptions",
            cancellationToken: cancellationToken);
    }

    public Task<int> SaveNotificationAsync(string connectionString, int companyCode, NotifyMeDraftVm draft, string updatedBy, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Jeeves-backed NotifyMe save is no longer supported. Use portal-backed NotifyMe storage.");
    }

    public async Task<NotifyMeTestRunResultVm> RunTestNotificationAsync(
        string connectionString,
        int companyCode,
        int notificationId,
        string overrideRecipient,
        CancellationToken cancellationToken = default)
    {
        return await _jeevesSqlExecutor.WithConnectionAsync(
            connectionString,
            async connection =>
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

                const string currentSql = @"
SELECT
    q_zu_notcenter_nr              AS [NotificationId],
    foretagkod                     AS [CompanyCode],
    q_zu_notcenter_schema          AS [SchemaCode],
    q_zu_notcenter_mailadress1     AS [PrimaryEmail],
    q_zu_notcenter_mailadress2     AS [SecondaryEmail],
    q_zu_notcenter_cc              AS [Cc],
    q_zu_notcenter_bcc             AS [Bcc],
    q_zu_notcenter_email_eskalera  AS [EscalationEmail],
    q_zu_notcenter_dyn_adress      AS [DynamicAddress],
    q_zu_notcenter_antvarning      AS [WarningCount],
    q_zu_notcenter_execdat         AS [NextExecutionAt],
    q_zu_notcenter_varndat         AS [LastWarningAt]
FROM dbo.q_zu_notcenter
WHERE foretagkod = @CompanyCode
  AND q_zu_notcenter_nr = @NotificationId";

                var state = await connection.QueryFirstOrDefaultAsync<NotifyMeTestState>(
                    new CommandDefinition(
                        currentSql,
                        new { CompanyCode = companyCode, NotificationId = notificationId },
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                if (state == null)
                    throw new InvalidOperationException("Notifieringen hittades inte för valt bolag.");

                if (string.IsNullOrWhiteSpace(state.SchemaCode))
                    throw new InvalidOperationException("Notifieringen saknar schema och kan inte testköras.");

                var previousLoggedAt = await connection.ExecuteScalarAsync<DateTime?>(
                    new CommandDefinition(
                        "SELECT MAX(regdat) FROM dbo.q_zu_notcenter_log WHERE foretagkod = @CompanyCode AND q_zu_notcenter_nr = @NotificationId",
                        new { CompanyCode = companyCode, NotificationId = notificationId },
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                var previousMailItemId = await TryGetLatestMailItemIdAsync(connection, transaction, overrideRecipient, cancellationToken);

                const string prepareSql = @"
UPDATE dbo.q_zu_notcenter
SET q_zu_notcenter_mailadress1 = @OverrideRecipient,
    q_zu_notcenter_mailadress2 = NULL,
    q_zu_notcenter_cc = NULL,
    q_zu_notcenter_bcc = NULL,
    q_zu_notcenter_email_eskalera = NULL,
    q_zu_notcenter_dyn_adress = '0'
WHERE foretagkod = @CompanyCode
  AND q_zu_notcenter_nr = @NotificationId";

                await connection.ExecuteAsync(
                    new CommandDefinition(
                        prepareSql,
                        new { OverrideRecipient = overrideRecipient, CompanyCode = companyCode, NotificationId = notificationId },
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                const string runSql = @"
EXEC dbo.q_zu_notification_center
    @c_IntrnCoNo = @CompanyCode,
    @c_Schema = @SchemaCode,
    @q_zu_notcenter_nr = @NotificationId";

                await connection.ExecuteAsync(
                    new CommandDefinition(
                        runSql,
                        new { CompanyCode = companyCode, SchemaCode = state.SchemaCode, NotificationId = notificationId },
                        transaction: transaction,
                        commandTimeout: 60,
                        cancellationToken: cancellationToken));

                var latestLog = await connection.QueryFirstOrDefaultAsync<NotifyMeLogSnapshot>(
                    new CommandDefinition(
                        @"
SELECT TOP (1)
    ROW_NUMBER() OVER (ORDER BY regdat DESC) AS [LogId],
    regdat        AS [LoggedAt],
    q_zu_notcenter_subject AS [Subject]
FROM dbo.q_zu_notcenter_log
WHERE foretagkod = @CompanyCode
  AND q_zu_notcenter_nr = @NotificationId
  AND (@PreviousLoggedAt IS NULL OR regdat > @PreviousLoggedAt)
ORDER BY regdat DESC",
                        new { CompanyCode = companyCode, NotificationId = notificationId, PreviousLoggedAt = previousLoggedAt },
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                var latestMail = await TryGetLatestMailItemAsync(connection, transaction, overrideRecipient, previousMailItemId, cancellationToken);

                const string restoreSql = @"
UPDATE dbo.q_zu_notcenter
SET q_zu_notcenter_mailadress1 = @PrimaryEmail,
    q_zu_notcenter_mailadress2 = @SecondaryEmail,
    q_zu_notcenter_cc = @Cc,
    q_zu_notcenter_bcc = @Bcc,
    q_zu_notcenter_email_eskalera = @EscalationEmail,
    q_zu_notcenter_dyn_adress = @DynamicAddress,
    q_zu_notcenter_antvarning = @WarningCount,
    q_zu_notcenter_execdat = @NextExecutionAt,
    q_zu_notcenter_varndat = @LastWarningAt,
    rowupdatedby = 'TRG'
WHERE foretagkod = @CompanyCode
  AND q_zu_notcenter_nr = @NotificationId";

                await connection.ExecuteAsync(
                    new CommandDefinition(
                        restoreSql,
                        new
                        {
                            state.PrimaryEmail,
                            state.SecondaryEmail,
                            state.Cc,
                            state.Bcc,
                            state.EscalationEmail,
                            state.DynamicAddress,
                            state.WarningCount,
                            state.NextExecutionAt,
                            state.LastWarningAt,
                            CompanyCode = companyCode,
                            NotificationId = notificationId
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                await transaction.CommitAsync(cancellationToken);

                return new NotifyMeTestRunResultVm
                {
                    NotificationId = notificationId,
                    CompanyCode = companyCode,
                    OverrideRecipient = overrideRecipient,
                    Subject = latestLog?.Subject,
                    LoggedAt = latestLog?.LoggedAt,
                    LogCreated = latestLog != null,
                    MailQueued = latestMail != null,
                    MailItemId = latestMail?.MailItemId,
                    MailStatus = latestMail?.SentStatus
                };
            },
            operationName: "JeevesNotifyMeRepository.RunTestNotification");
    }

    private static string ToPreviewText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withoutTags = Regex.Replace(html, "<[^>]+>", " ", RegexOptions.Compiled);
        var normalized = Regex.Replace(withoutTags, @"\s+", " ", RegexOptions.Compiled).Trim();
        if (normalized.Length <= 260)
            return normalized;

        return normalized[..260] + "...";
    }

    private sealed class NotifyMeTestState
    {
        public int NotificationId { get; set; }
        public int CompanyCode { get; set; }
        public string? SchemaCode { get; set; }
        public string? PrimaryEmail { get; set; }
        public string? SecondaryEmail { get; set; }
        public string? Cc { get; set; }
        public string? Bcc { get; set; }
        public string? EscalationEmail { get; set; }
        public string? DynamicAddress { get; set; }
        public int? WarningCount { get; set; }
        public DateTime? NextExecutionAt { get; set; }
        public DateTime? LastWarningAt { get; set; }
    }

    private sealed class NotifyMeLogSnapshot
    {
        public int LogId { get; set; }
        public DateTime? LoggedAt { get; set; }
        public string? Subject { get; set; }
    }

    private sealed class NotifyMeMailSnapshot
    {
        public long MailItemId { get; set; }
        public string? SentStatus { get; set; }
    }

    private static async Task<long?> TryGetLatestMailItemIdAsync(
        DbConnection connection,
        DbTransaction transaction,
        string overrideRecipient,
        CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ExecuteScalarAsync<long?>(
                new CommandDefinition(
                    @"
SELECT MAX(mailitem_id)
FROM msdb.dbo.sysmail_allitems
WHERE ISNULL(recipients, '') LIKE '%' + @OverrideRecipient + '%'
   OR ISNULL(copy_recipients, '') LIKE '%' + @OverrideRecipient + '%'
   OR ISNULL(blind_copy_recipients, '') LIKE '%' + @OverrideRecipient + '%'",
                    new { OverrideRecipient = overrideRecipient },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
        }
        catch
        {
            return null;
        }
    }

    private static async Task<NotifyMeMailSnapshot?> TryGetLatestMailItemAsync(
        DbConnection connection,
        DbTransaction transaction,
        string overrideRecipient,
        long? previousMailItemId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await connection.QueryFirstOrDefaultAsync<NotifyMeMailSnapshot>(
                new CommandDefinition(
                    @"
SELECT TOP (1)
    mailitem_id AS [MailItemId],
    sent_status AS [SentStatus]
FROM msdb.dbo.sysmail_allitems
WHERE (@PreviousMailItemId IS NULL OR mailitem_id > @PreviousMailItemId)
  AND (
        ISNULL(recipients, '') LIKE '%' + @OverrideRecipient + '%'
     OR ISNULL(copy_recipients, '') LIKE '%' + @OverrideRecipient + '%'
     OR ISNULL(blind_copy_recipients, '') LIKE '%' + @OverrideRecipient + '%'
  )
ORDER BY mailitem_id DESC",
                    new { OverrideRecipient = overrideRecipient, PreviousMailItemId = previousMailItemId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
        }
        catch
        {
            return null;
        }
    }
}
