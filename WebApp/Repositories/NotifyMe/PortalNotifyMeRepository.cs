using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Net;
using WebApp.Data;
using WebApp.Services.NotifyMe;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Repositories.NotifyMe;

public sealed class PortalNotifyMeRepository : INotifyMeRepository
{
    private readonly ApplicationDbContext _dbContext;

    public PortalNotifyMeRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IReadOnlyList<NotifyMeListItemVm>> GetNotificationsAsync(string connectionString, int companyCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    n.q_zu_notcenter_nr                AS [NotificationId],
    ISNULL(n.q_zu_notcenter_beskrivning, '') AS [Description],
    ISNULL(n.q_zu_notcenter_varntext, '')    AS [WarningText],
    ISNULL(vt.q_zu_notcenter_typbeskr, ISNULL(n.q_zu_notcenter_typ, '')) AS [TypeLabel],
    ISNULL(vk.q_zu_notcenter_typbeskr, ISNULL(n.q_zu_notcenter_prio, '')) AS [PriorityLabel],
    NULLIF(LTRIM(RTRIM(n.q_zu_notcenter_schema)), '') AS [SchemaCode],
    NULLIF(LTRIM(RTRIM(n.q_zu_notcenter_schedule)), '') AS [ScheduleCode],
    CONCAT(ISNULL(n.q_zu_notcenter_schema, '-'), ' / ', ISNULL(n.q_zu_notcenter_schedule, '-')) AS [ScheduleLabel],
    CASE WHEN n.q_zu_notcenter_in_use IN ('1','J','Y') THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [IsActive],
    n.q_zu_notcenter_execdat           AS [NextExecutionAt],
    n.q_zu_notcenter_varndat           AS [LastWarningAt],
    ISNULL(n.q_zu_notcenter_antvarning, 0) AS [WarningCount],
    n.q_zu_notcenter_antal_eskalera    AS [EscalateAfterCount],
    CASE
        WHEN n.q_zu_notcenter_in_use IN ('1','J','Y')
         AND n.q_zu_notcenter_execdat IS NOT NULL
         AND n.q_zu_notcenter_execdat <= SYSUTCDATETIME() THEN CAST(1 AS bit)
        ELSE CAST(0 AS bit)
    END AS [IsDueNow]
FROM dbo.q_zu_notcenter n
LEFT JOIN dbo.q_zu_notcenter_varningstyp vt ON vt.q_zu_notcenter_typ = n.q_zu_notcenter_typ
LEFT JOIN dbo.q_zu_notcenter_varningskat vk ON vk.q_zu_notcenter_prio = n.q_zu_notcenter_prio
WHERE n.foretagkod = @CompanyCode
ORDER BY n.q_zu_notcenter_in_use DESC, n.q_zu_notcenter_execdat ASC, n.q_zu_notcenter_nr ASC";

        return WithPortalConnectionAsync(async connection =>
        {
            var rows = (await connection.QueryAsync<NotifyMeListItemVm>(
                new CommandDefinition(sql, new { CompanyCode = companyCode }, cancellationToken: cancellationToken)));
            var items = rows.AsList();
            var stockholmNow = NotifyMeTimeZoneHelper.StockholmNow;

            foreach (var item in items)
            {
                item.NextExecutionAt = NotifyMeTimeZoneHelper.ToStockholmTime(item.NextExecutionAt);
                item.LastWarningAt = NotifyMeTimeZoneHelper.ToStockholmTime(item.LastWarningAt);
                item.IsDueNow = item.IsActive
                    && item.NextExecutionAt.HasValue
                    && item.NextExecutionAt.Value <= stockholmNow;
                item.HasAutomation = HasAutomation(item.SchemaCode, item.ScheduleCode);
                item.AutomationHint = BuildAutomationHint(item.SchemaCode, item.ScheduleCode);
            }

            return (IReadOnlyList<NotifyMeListItemVm>)items;
        }, cancellationToken);
    }

    public Task<NotifyMeDetailsVm?> GetNotificationAsync(string connectionString, int companyCode, int notificationId, CancellationToken cancellationToken = default)
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
    CASE WHEN ISNULL(n.q_zu_notcenter_dyn_adress, '0') = '1' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [UsesDynamicRecipients],
    n.q_zu_notcenter_select2           AS [SqlPreview],
    n.q_zu_notcenter_sysl              AS [SysChangeSource]
FROM dbo.q_zu_notcenter n
LEFT JOIN dbo.q_zu_notcenter_varningstyp vt ON vt.q_zu_notcenter_typ = n.q_zu_notcenter_typ
LEFT JOIN dbo.q_zu_notcenter_varningskat vk ON vk.q_zu_notcenter_prio = n.q_zu_notcenter_prio
WHERE n.foretagkod = @CompanyCode
  AND n.q_zu_notcenter_nr = @NotificationId";

        return WithPortalConnectionAsync(async connection =>
        {
            var item = await connection.QueryFirstOrDefaultAsync<NotifyMeDetailsVm>(
                new CommandDefinition(sql, new { CompanyCode = companyCode, NotificationId = notificationId }, cancellationToken: cancellationToken));

            if (item == null)
                return null;

            item.NextExecutionAt = NotifyMeTimeZoneHelper.ToStockholmTime(item.NextExecutionAt);
            item.LastWarningAt = NotifyMeTimeZoneHelper.ToStockholmTime(item.LastWarningAt);

            return item;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<NotifyMeLogItemVm>> GetRecentLogEntriesAsync(string connectionString, int companyCode, int? notificationId = null, int take = 15, CancellationToken cancellationToken = default)
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

        return WithPortalConnectionAsync(async connection =>
        {
            var rows = (await connection.QueryAsync<NotifyMeLogItemVm>(
                new CommandDefinition(sql, new { CompanyCode = companyCode, NotificationId = notificationId, Take = take }, cancellationToken: cancellationToken))).AsList();

            foreach (var row in rows)
            {
                row.SentAt = NotifyMeTimeZoneHelper.ToStockholmTime(row.SentAt);
                NormalizeLogStatus(row);
                row.HtmlPreviewText = NotifyMePreviewText.ToPreviewText(row.HtmlPreviewText);
            }

            return (IReadOnlyList<NotifyMeLogItemVm>)rows;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetTypeOptionsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    CONVERT(nvarchar(50), q_zu_notcenter_typ) AS [Value],
    ISNULL(q_zu_notcenter_typbeskr, CONVERT(nvarchar(50), q_zu_notcenter_typ)) AS [Label]
FROM dbo.q_zu_notcenter_varningstyp
ORDER BY q_zu_notcenter_typ";

        return WithPortalConnectionAsync(async connection =>
        {
            var rows = await connection.QueryAsync<NotifyMeLookupOptionVm>(new CommandDefinition(sql, cancellationToken: cancellationToken));
            return (IReadOnlyList<NotifyMeLookupOptionVm>)rows.AsList();
        }, cancellationToken);
    }

    public Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetPriorityOptionsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    CONVERT(nvarchar(50), q_zu_notcenter_prio) AS [Value],
    ISNULL(q_zu_notcenter_typbeskr, CONVERT(nvarchar(50), q_zu_notcenter_prio)) AS [Label]
FROM dbo.q_zu_notcenter_varningskat
ORDER BY q_zu_notcenter_prio";

        return WithPortalConnectionAsync(async connection =>
        {
            var rows = await connection.QueryAsync<NotifyMeLookupOptionVm>(new CommandDefinition(sql, cancellationToken: cancellationToken));
            return (IReadOnlyList<NotifyMeLookupOptionVm>)rows.AsList();
        }, cancellationToken);
    }

    public Task<int> SaveNotificationAsync(string connectionString, int companyCode, NotifyMeDraftVm draft, string updatedBy, CancellationToken cancellationToken = default)
    {
        return WithPortalConnectionAsync(async connection =>
        {
            var now = DateTime.UtcNow;
            var actor = NormalizeUser(updatedBy);
            var nextExecutionAt = NotifyMeScheduleCalculator.CalculateNextExecution(
                now,
                draft.StartDate,
                draft.SchemaCode,
                draft.ScheduleCode);

            if (draft.NotificationId.HasValue)
            {
                const string updateSql = @"
UPDATE dbo.q_zu_notcenter
SET q_zu_notcenter_beskrivning = @Description,
    q_zu_notcenter_varntext = @WarningText,
    q_zu_notcenter_kommentar = @Comment,
    q_zu_notcenter_typ = @TypeCode,
    q_zu_notcenter_prio = @PriorityCode,
    q_zu_notcenter_mailadress1 = @PrimaryEmail,
    q_zu_notcenter_mailadress2 = @SecondaryEmail,
    q_zu_notcenter_cc = @Cc,
    q_zu_notcenter_bcc = @Bcc,
    q_zu_notcenter_schema = @SchemaCode,
    q_zu_notcenter_schedule = @ScheduleCode,
    q_zu_notcenter_startdat = @StartDate,
    q_zu_notcenter_execdat = @NextExecutionAt,
    q_zu_notcenter_antal_eskalera = @EscalateAfterCount,
    q_zu_notcenter_email_eskalera = @EscalationEmail,
    q_zu_notcenter_select2 = @SqlPreview,
    q_zu_notcenter_dyn_adress = @DynamicAddress,
    q_zu_notcenter_in_use = @InUseCode,
    rowupdatedby = 'TRG',
    rowupdateddt = @Now
WHERE foretagkod = @CompanyCode
  AND q_zu_notcenter_nr = @NotificationId";

                await connection.ExecuteAsync(new CommandDefinition(
                    updateSql,
                    new
                    {
                        NotificationId = draft.NotificationId.Value,
                        CompanyCode = companyCode,
                        Description = draft.Description,
                        WarningText = draft.WarningText,
                        Comment = draft.Comment,
                        TypeCode = draft.TypeCode,
                        PriorityCode = draft.PriorityCode,
                        PrimaryEmail = draft.PrimaryEmail,
                        SecondaryEmail = draft.SecondaryEmail,
                        Cc = draft.Cc,
                        Bcc = draft.Bcc,
                        SchemaCode = draft.SchemaCode,
                        ScheduleCode = draft.ScheduleCode,
                        StartDate = draft.StartDate,
                        NextExecutionAt = nextExecutionAt,
                        EscalateAfterCount = draft.EscalateAfterCount,
                        EscalationEmail = draft.EscalationEmail,
                        SqlPreview = draft.SqlPreview,
                        DynamicAddress = draft.UsesDynamicRecipients ? "1" : "0",
                        InUseCode = draft.IsActive ? "1" : "0",
                        Actor = actor,
                        Now = now
                    },
                    cancellationToken: cancellationToken));

                return draft.NotificationId.Value;
            }

            const string nextIdSql = @"
SELECT ISNULL(MAX(q_zu_notcenter_nr), 0) + 1
FROM dbo.q_zu_notcenter
WHERE foretagkod = @CompanyCode";

            var nextId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                nextIdSql,
                new { CompanyCode = companyCode },
                cancellationToken: cancellationToken));

            const string insertSql = @"
INSERT INTO dbo.q_zu_notcenter (
    q_zu_notcenter_nr,
    perssign,
    regdat,
    rowcreatedby,
    rowcreateddt,
    rowupdatedby,
    rowupdateddt,
    foretagkod,
    q_zu_notcenter_beskrivning,
    q_zu_notcenter_typ,
    q_zu_notcenter_prio,
    q_zu_notcenter_varntext,
    q_zu_notcenter_kommentar,
    q_zu_notcenter_mailadress1,
    q_zu_notcenter_mailadress2,
    q_zu_notcenter_schema,
    q_zu_notcenter_in_use,
    q_zu_notcenter_antvarning,
    q_zu_notcenter_execdat,
    q_zu_notcenter_varndat,
    q_zu_notcenter_select2,
    q_zu_notcenter_sysl,
    q_zu_notcenter_startdat,
    q_zu_notcenter_schedule,
    q_zu_notcenter_antal_eskalera,
    q_zu_notcenter_email_eskalera,
    q_zu_notcenter_bcc,
    q_zu_notcenter_cc,
    q_zu_notcenter_dyn_adress,
    q_zu_notcenter_language
)
VALUES (
    @NotificationId,
    @Actor,
    @Now,
    @Actor,
    @Now,
    'TRG',
    @Now,
    @CompanyCode,
    @Description,
    @TypeCode,
    @PriorityCode,
    @WarningText,
    @Comment,
    @PrimaryEmail,
    @SecondaryEmail,
    @SchemaCode,
    @InUseCode,
    0,
    @NextExecutionAt,
    NULL,
    @SqlPreview,
    NULL,
    @StartDate,
    @ScheduleCode,
    @EscalateAfterCount,
    @EscalationEmail,
    @Bcc,
    @Cc,
    @DynamicAddress,
    0
)";

            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new
                {
                    NotificationId = nextId,
                    Actor = actor,
                    Now = now,
                    CompanyCode = companyCode,
                    Description = draft.Description,
                    WarningText = draft.WarningText,
                    Comment = draft.Comment,
                    TypeCode = draft.TypeCode,
                    PriorityCode = draft.PriorityCode,
                    PrimaryEmail = draft.PrimaryEmail,
                    SecondaryEmail = draft.SecondaryEmail,
                    SchemaCode = draft.SchemaCode,
                    InUseCode = draft.IsActive ? "1" : "0",
                    NextExecutionAt = nextExecutionAt,
                    SqlPreview = draft.SqlPreview,
                    DynamicAddress = draft.UsesDynamicRecipients ? "1" : "0",
                    StartDate = draft.StartDate,
                    ScheduleCode = draft.ScheduleCode,
                    EscalateAfterCount = draft.EscalateAfterCount,
                    EscalationEmail = draft.EscalationEmail,
                    Bcc = draft.Bcc,
                    Cc = draft.Cc
                },
                cancellationToken: cancellationToken));

            return nextId;
        }, cancellationToken);
    }

    private async Task<T> WithPortalConnectionAsync<T>(Func<SqlConnection, Task<T>> action, CancellationToken cancellationToken)
    {
        var connection = (SqlConnection)_dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            return await action(connection);
        }
        finally
        {
            if (shouldClose)
                await _dbContext.Database.CloseConnectionAsync();
        }
    }

    private static string NormalizeUser(string? user)
    {
        if (string.IsNullOrWhiteSpace(user))
            return "PORTAL";

        var trimmed = user.Trim();
        return trimmed.Length <= 30 ? trimmed : trimmed[..30];
    }

    private static void NormalizeLogStatus(NotifyMeLogItemVm row)
    {
        if (row.Subject.StartsWith("[Misslyckad][Retry ", StringComparison.OrdinalIgnoreCase))
        {
            row.ExecutionStatus = "Retry pågår";
            row.ExecutionStatusTone = "warning";
            row.Subject = StripFailurePrefix(row.Subject);
            return;
        }

        if (row.Subject.StartsWith("[Misslyckad][Manuell åtgärd] ", StringComparison.OrdinalIgnoreCase))
        {
            row.ExecutionStatus = "Manuell åtgärd";
            row.ExecutionStatusTone = "danger";
            row.Subject = StripFailurePrefix(row.Subject);
            return;
        }

        if (row.Subject.StartsWith("[Misslyckad] ", StringComparison.OrdinalIgnoreCase))
        {
            row.ExecutionStatus = "Misslyckad";
            row.ExecutionStatusTone = "danger";
            row.Subject = StripFailurePrefix(row.Subject);
            return;
        }

        if (row.Subject.StartsWith("[Ingen träff] ", StringComparison.OrdinalIgnoreCase))
        {
            row.ExecutionStatus = "Ingen träff";
            row.ExecutionStatusTone = "warning";
            row.Subject = row.Subject["[Ingen träff] ".Length..];
            return;
        }

        row.ExecutionStatus = "Skickad";
        row.ExecutionStatusTone = "success";
    }

    private static string StripFailurePrefix(string subject)
    {
        var trimmed = subject;
        while (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            var closing = trimmed.IndexOf(']');
            if (closing < 0 || closing == trimmed.Length - 1)
                break;

            trimmed = trimmed[(closing + 1)..].TrimStart();
        }

        return trimmed;
    }

    private static bool HasAutomation(string? schemaCode, string? scheduleCode)
    {
        return !string.IsNullOrWhiteSpace(schemaCode) && !string.IsNullOrWhiteSpace(scheduleCode);
    }

    private static string? BuildAutomationHint(string? schemaCode, string? scheduleCode)
    {
        if (!HasAutomation(schemaCode, scheduleCode))
            return null;

        var normalizedSchema = schemaCode!.Trim();
        var normalizedSchedule = scheduleCode!.Trim();

        if (normalizedSchema == "40")
            return "Automatisk körning varje timme.";

        var scheduleLabel = normalizedSchedule switch
        {
            "10" => "dagligen",
            "20" => "veckovis",
            "30" => "månadsvis",
            _ => $"med frekvens {normalizedSchedule}"
        };

        var schemaLabel = normalizedSchema switch
        {
            "10" => "dagtid",
            "20" => "nattetid",
            "30" => "dag- och nattkörning",
            _ => $"schema {normalizedSchema}"
        };

        return $"Automatisk körning {scheduleLabel} ({schemaLabel}).";
    }
}

internal static class NotifyMePreviewText
{
    public static string ToPreviewText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var decoded = WebUtility.HtmlDecode(html);
        var withoutTags = System.Text.RegularExpressions.Regex.Replace(decoded, "<[^>]+>", " ", System.Text.RegularExpressions.RegexOptions.Compiled);
        var normalized = System.Text.RegularExpressions.Regex.Replace(withoutTags, @"\s+", " ", System.Text.RegularExpressions.RegexOptions.Compiled).Trim();
        if (normalized.Length <= 260)
            return normalized;

        return normalized[..260] + "...";
    }
}
