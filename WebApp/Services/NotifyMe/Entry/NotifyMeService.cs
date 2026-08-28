using LoggerService;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using WebApp.Repositories.NotifyMe;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.NotifyMe;

// Coordinates NotifyMe editor, save and execution flows while read-only pages live in query services.
public sealed class NotifyMeService : INotifyMeService
{
    private readonly INotifyMePageQueryService _pageQueryService;
    private readonly INotifyMeRepository _repository;
    private readonly INotifyMeExecutionService _executionService;
    private readonly ILoggerManager _logger;

    public NotifyMeService(
        INotifyMePageQueryService pageQueryService,
        INotifyMeRepository repository,
        INotifyMeExecutionService executionService,
        ILoggerManager logger)
    {
        _pageQueryService = pageQueryService;
        _repository = repository;
        _executionService = executionService;
        _logger = logger;
    }

    public Task<NotifyMeOverviewVm> GetOverviewAsync(
        string? connectionString,
        int? companyCode,
        string? search = null,
        string? status = null,
        string? type = null,
        string? priority = null,
        int page = 1,
        CancellationToken cancellationToken = default)
        => _pageQueryService.GetOverviewAsync(connectionString, companyCode, search, status, type, priority, page, cancellationToken);

    public Task<NotifyMeHistoryPageVm> GetHistoryAsync(
        string? connectionString,
        int? companyCode,
        int? historyNotificationId = null,
        string? historySearch = null,
        int page = 1,
        CancellationToken cancellationToken = default)
        => _pageQueryService.GetHistoryAsync(connectionString, companyCode, historyNotificationId, historySearch, page, cancellationToken);

    public Task<NotifyMeTemplateLibraryVm> GetTemplateLibraryAsync(
        string? connectionString,
        int? companyCode,
        string? search = null,
        string? category = null,
        CancellationToken cancellationToken = default)
        => _pageQueryService.GetTemplateLibraryAsync(connectionString, companyCode, search, category, cancellationToken);

    public Task<NotifyMeStatisticsVm> GetStatisticsAsync(
        string? connectionString,
        int? companyCode,
        CancellationToken cancellationToken = default)
        => _pageQueryService.GetStatisticsAsync(connectionString, companyCode, cancellationToken);

    public Task<NotifyMeDetailsPageVm> GetDetailsAsync(string? connectionString, int? companyCode, int notificationId, CancellationToken cancellationToken = default)
        => _pageQueryService.GetDetailsAsync(connectionString, companyCode, notificationId, cancellationToken);

    public async Task<NotifyMeCreatePrototypeVm> GetCreatePrototypeAsync(string? connectionString, int? companyCode, int? notificationId = null, CancellationToken cancellationToken = default)
    {
        if (!NotifyMeConnectionContext.HasConnectionContext(connectionString, companyCode, out var message))
            return UnavailableCreatePrototype(companyCode, message);

        try
        {
            var types = await _repository.GetTypeOptionsAsync(connectionString!, cancellationToken);
            var priorities = await _repository.GetPriorityOptionsAsync(connectionString!, cancellationToken);
            var existingNotification = notificationId.HasValue
                ? await _repository.GetNotificationAsync(connectionString!, companyCode!.Value, notificationId.Value, cancellationToken)
                : null;

            return new NotifyMeCreatePrototypeVm
            {
                CompanyCode = companyCode,
                IsInstalled = true,
                NotificationId = existingNotification?.NotificationId,
                IsEditMode = existingNotification != null,
                StatusMessage = notificationId.HasValue && existingNotification == null
                    ? "Vald notifiering hittades inte. Editorn öppnades i nytt läge."
                    : null,
                Types = types,
                Priorities = priorities,
                Schemas = BuildSchemaOptions(),
                Schedules = BuildScheduleOptions(),
                Draft = existingNotification != null
                    ? MapDraft(existingNotification)
                    : new NotifyMeDraftVm
                    {
                        StartDate = DateTime.Today,
                        SchemaCode = "10",
                        ScheduleCode = "10"
                    }
            };
        }
        catch (SqlException ex) when (ex.Number is 208 or 207 or 2812)
        {
            _logger.LogWarning($"NotifyMe create prototype unavailable for company {companyCode}: {ex.Message}");
            return UnavailableCreatePrototype(companyCode, "NotifyMe-tabellerna finns inte i den här Jeeves-databasen ännu.");
        }
    }

    public async Task<NotifyMeTestRunResultVm> RunTestNotificationAsync(
        string? connectionString,
        int? companyCode,
        int notificationId,
        string overrideRecipient,
        CancellationToken cancellationToken = default)
    {
        if (!NotifyMeConnectionContext.HasConnectionContext(connectionString, companyCode, out var message))
            throw new InvalidOperationException(message);

        if (string.IsNullOrWhiteSpace(overrideRecipient))
            throw new InvalidOperationException("Testmottagare måste anges.");

        var emailValidator = new EmailAddressAttribute();
        if (!emailValidator.IsValid(overrideRecipient))
            throw new InvalidOperationException("Testmottagaren måste vara en giltig e-postadress.");

        try
        {
            return await _executionService.RunTestNotificationAsync(
                connectionString!,
                companyCode!.Value,
                notificationId,
                overrideRecipient.Trim(),
                cancellationToken);
        }
        catch (SqlException ex) when (IsDatabaseMailDisabled(ex))
        {
            throw new InvalidOperationException(
                "Testkörningen nådde SQL-motorn, men Database Mail är inte aktiverat på den här SQL Server-instansen.");
        }
        catch (SqlException ex) when (IsDatabaseMailProfileIssue(ex))
        {
            throw new InvalidOperationException(
                "Testkörningen nådde SQL-motorn, men Database Mail-profilen för NotifyMe saknas eller är felkonfigurerad i den här miljön.");
        }
    }

    public async Task<int> SaveNotificationAsync(
        string? connectionString,
        int? companyCode,
        NotifyMeDraftVm draft,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (!NotifyMeConnectionContext.HasConnectionContext(connectionString, companyCode, out var message))
            throw new InvalidOperationException(message);

        if (string.IsNullOrWhiteSpace(draft.Description))
            throw new InvalidOperationException("Beskrivning måste anges.");

        if (string.IsNullOrWhiteSpace(draft.WarningText))
            throw new InvalidOperationException("Varningstext måste anges.");

        if (string.IsNullOrWhiteSpace(draft.TypeCode))
            throw new InvalidOperationException("Typ måste väljas.");

        if (string.IsNullOrWhiteSpace(draft.PriorityCode))
            throw new InvalidOperationException("Prioritet måste väljas.");

        if (string.IsNullOrWhiteSpace(draft.SchemaCode))
            throw new InvalidOperationException("Schema måste väljas.");

        if (string.IsNullOrWhiteSpace(draft.ScheduleCode))
            throw new InvalidOperationException("Frekvens måste väljas.");

        return await _repository.SaveNotificationAsync(connectionString ?? string.Empty, companyCode!.Value, draft, updatedBy, cancellationToken);
    }

    private static NotifyMeCreatePrototypeVm UnavailableCreatePrototype(int? companyCode, string message)
    {
        return new NotifyMeCreatePrototypeVm
        {
            CompanyCode = companyCode,
            IsInstalled = false,
            IsEditMode = false,
            StatusMessage = message,
            Schemas = BuildSchemaOptions(),
            Schedules = BuildScheduleOptions(),
            Draft = new NotifyMeDraftVm { StartDate = DateTime.Today, SchemaCode = "10", ScheduleCode = "10" }
        };
    }

    private static IReadOnlyList<NotifyMeLookupOptionVm> BuildSchemaOptions()
    {
        return new[]
        {
            new NotifyMeLookupOptionVm { Value = "10", Label = "Dag" },
            new NotifyMeLookupOptionVm { Value = "20", Label = "Natt" },
            new NotifyMeLookupOptionVm { Value = "30", Label = "Dag och natt" },
            new NotifyMeLookupOptionVm { Value = "40", Label = "Varje timma" }
        };
    }

    private static IReadOnlyList<NotifyMeLookupOptionVm> BuildScheduleOptions()
    {
        return new[]
        {
            new NotifyMeLookupOptionVm { Value = "10", Label = "Dagligen" },
            new NotifyMeLookupOptionVm { Value = "20", Label = "Veckovis" },
            new NotifyMeLookupOptionVm { Value = "30", Label = "Månadsvis" }
        };
    }

    private static NotifyMeDraftVm MapDraft(NotifyMeDetailsVm notification)
    {
        return new NotifyMeDraftVm
        {
            Description = notification.Description,
            WarningText = notification.WarningText,
            Comment = notification.Comment,
            TypeCode = notification.TypeCode,
            PriorityCode = notification.PriorityCode,
            PrimaryEmail = notification.PrimaryEmail,
            SecondaryEmail = notification.SecondaryEmail,
            Cc = notification.Cc,
            Bcc = notification.Bcc,
            SchemaCode = notification.SchemaCode,
            ScheduleCode = notification.ScheduleCode,
            StartDate = notification.StartDate,
            EscalateAfterCount = notification.EscalateAfterCount,
            EscalationEmail = notification.EscalationEmail,
            SqlPreview = notification.SqlPreview,
            UsesDynamicRecipients = notification.UsesDynamicRecipients
        };
    }

    private static bool IsDatabaseMailDisabled(SqlException ex)
    {
        return ex.Message.Contains("Database Mail XPs", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("sp_send_dbmail", StringComparison.OrdinalIgnoreCase)
                  && ex.Message.Contains("turned off", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDatabaseMailProfileIssue(SqlException ex)
    {
        return ex.Message.Contains("profile name is not valid", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("mail profile", StringComparison.OrdinalIgnoreCase)
                  && ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);
    }
}
