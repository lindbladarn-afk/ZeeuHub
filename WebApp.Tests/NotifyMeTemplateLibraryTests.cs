// Verifies the read-only NotifyMe presentation and its safe database-error handling.
using LoggerService;
using Microsoft.Data.SqlClient;
using System.Reflection;
using WebApp.Repositories.NotifyMe;
using WebApp.Services.NotifyMe;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Tests;

public sealed class NotifyMeTemplateLibraryTests
{
    [Fact]
    public async Task GetOverviewAsync_AppliesExistingFiltersAndCounts()
    {
        var repository = new FakeNotifyMeRepository(new[]
        {
            new NotifyMeListItemVm
            {
                NotificationId = 1,
                Description = "Order saknar leveransdatum",
                WarningText = "Kontrollera order",
                TypeLabel = "Order",
                PriorityLabel = "Hög",
                IsActive = true,
                IsDueNow = true,
                EscalateAfterCount = 2
            },
            new NotifyMeListItemVm
            {
                NotificationId = 2,
                Description = "Faktura saknar referens",
                WarningText = "Kontrollera faktura",
                TypeLabel = "Ekonomi",
                PriorityLabel = "Normal",
                IsActive = false
            }
        });
        var service = CreateService(repository);

        var result = await service.GetOverviewAsync(
            "Server=.;Database=Tenant;",
            100,
            search: "order",
            status: "due",
            type: "Order",
            priority: "Hög");

        var notification = Assert.Single(result.Notifications);
        Assert.True(result.IsInstalled);
        Assert.Equal(1, notification.NotificationId);
        Assert.Equal(2, result.TotalNotifications);
        Assert.Equal(1, result.ActiveNotifications);
        Assert.Equal(1, result.DueNowCount);
        Assert.Equal(1, result.EscalationConfiguredCount);
        Assert.Equal(1, result.FilteredNotificationsCount);
        Assert.Equal("due", result.Filters.Status);
    }

    [Fact]
    public async Task GetHistoryAsync_AppliesNotificationAndTextFilters()
    {
        var repository = new FakeNotifyMeRepository(
            new[]
            {
                new NotifyMeListItemVm { NotificationId = 1, Description = "Order" },
                new NotifyMeListItemVm { NotificationId = 2, Description = "Faktura" }
            },
            new[]
            {
                new NotifyMeLogItemVm
                {
                    LogId = 10,
                    NotificationId = 1,
                    Subject = "Order saknar leveransdatum",
                    Recipients = "order@example.com",
                    NotificationDescription = "Order",
                    HtmlPreviewText = "leverans"
                },
                new NotifyMeLogItemVm
                {
                    LogId = 11,
                    NotificationId = 2,
                    Subject = "Faktura saknar referens",
                    Recipients = "finance@example.com",
                    NotificationDescription = "Faktura",
                    HtmlPreviewText = "referens"
                }
            });
        var service = CreateService(repository);

        var result = await service.GetHistoryAsync(
            "Server=.;Database=Tenant;",
            100,
            historyNotificationId: 1,
            historySearch: "leverans");

        var logEntry = Assert.Single(result.HistoryEntries);
        Assert.True(result.IsInstalled);
        Assert.Equal(10, logEntry.LogId);
        Assert.Equal(1, result.TotalHistoryEntries);
        Assert.Equal(2, result.Filters.HistoryNotificationOptions.Count);
    }

    [Fact]
    public async Task GetTemplateLibraryAsync_MapsActiveCompanyNotifications()
    {
        var repository = new FakeNotifyMeRepository(new[]
        {
            new NotifyMeListItemVm
            {
                NotificationId = 17,
                Description = "Order utan checklista",
                WarningText = "Kontrollera order innan leverans",
                TypeLabel = "Order",
                PriorityLabel = "Hög",
                ScheduleLabel = "Dag / Dagligen",
                IsActive = true,
                HasAutomation = true,
                WarningCount = 3
            }
        });
        var service = CreateService(repository);

        var result = await service.GetTemplateLibraryAsync("Server=.;Database=Tenant;", 100);

        var template = Assert.Single(result.Templates);
        Assert.True(result.IsInstalled);
        Assert.Equal(17, template.SourceNotificationId);
        Assert.Equal("Order utan checklista", template.Title);
        Assert.Equal("Order", template.Category);
        Assert.Equal("Hög", template.SuggestedPriority);
        Assert.Contains("Aktiv", template.ParameterHints);
        Assert.Contains("3 varningar", template.ParameterHints);
    }

    [Fact]
    public async Task GetTemplateLibraryAsync_FiltersByCategory()
    {
        var repository = new FakeNotifyMeRepository(new[]
        {
            new NotifyMeListItemVm { NotificationId = 1, Description = "Order", TypeLabel = "Order" },
            new NotifyMeListItemVm { NotificationId = 2, Description = "Faktura", TypeLabel = "Ekonomi" }
        });
        var service = CreateService(repository);

        var result = await service.GetTemplateLibraryAsync("Server=.;Database=Tenant;", 100, category: "Ekonomi");

        var template = Assert.Single(result.Templates);
        Assert.Equal(2, template.SourceNotificationId);
        Assert.Equal(2, result.CategoryOptions.Count);
    }

    [Fact]
    public async Task GetOverviewAsync_Logs_Sanitized_Sql_Error()
    {
        var logger = new CapturingLoggerManager();
        var repository = new ThrowingNotifyMeRepository(CreateSqlException(208, "authorization=secret-value"));
        var service = new NotifyMePageQueryService(repository, logger);

        var result = await service.GetOverviewAsync("Server=.;Database=Tenant;", 100);

        Assert.False(result.IsInstalled);
        Assert.Contains("NotifyMe unavailable for company 100", logger.LastWarning ?? string.Empty);
        Assert.Contains("authorization", logger.LastWarning ?? string.Empty);
        Assert.DoesNotContain("secret-value", logger.LastWarning ?? string.Empty);
    }

    private static NotifyMePageQueryService CreateService(INotifyMeRepository repository)
    {
        return new NotifyMePageQueryService(repository, new NoopLoggerManager());
    }

    private sealed class FakeNotifyMeRepository : INotifyMeRepository
    {
        private readonly IReadOnlyList<NotifyMeListItemVm> _notifications;
        private readonly IReadOnlyList<NotifyMeLogItemVm> _logs;

        public FakeNotifyMeRepository(IReadOnlyList<NotifyMeListItemVm> notifications, IReadOnlyList<NotifyMeLogItemVm>? logs = null)
        {
            _notifications = notifications;
            _logs = logs ?? Array.Empty<NotifyMeLogItemVm>();
        }

        public Task<IReadOnlyList<NotifyMeListItemVm>> GetNotificationsAsync(string connectionString, int companyCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_notifications);
        }

        public Task<NotifyMeDetailsVm?> GetNotificationAsync(string connectionString, int companyCode, int notificationId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<NotifyMeLogItemVm>> GetRecentLogEntriesAsync(string connectionString, int companyCode, int? notificationId = null, int take = 15, CancellationToken cancellationToken = default)
        {
            var logs = _logs
                .Where(x => !notificationId.HasValue || x.NotificationId == notificationId.Value)
                .Take(take)
                .ToArray();

            return Task.FromResult<IReadOnlyList<NotifyMeLogItemVm>>(logs);
        }

        public Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetTypeOptionsAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetPriorityOptionsAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> SaveNotificationAsync(string connectionString, int companyCode, NotifyMeDraftVm draft, string updatedBy, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingNotifyMeRepository : INotifyMeRepository
    {
        private readonly SqlException _exception;

        public ThrowingNotifyMeRepository(SqlException exception)
        {
            _exception = exception;
        }

        public Task<IReadOnlyList<NotifyMeListItemVm>> GetNotificationsAsync(string connectionString, int companyCode, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<NotifyMeListItemVm>>(_exception);

        public Task<NotifyMeDetailsVm?> GetNotificationAsync(string connectionString, int companyCode, int notificationId, CancellationToken cancellationToken = default)
            => Task.FromException<NotifyMeDetailsVm?>(_exception);

        public Task<IReadOnlyList<NotifyMeLogItemVm>> GetRecentLogEntriesAsync(string connectionString, int companyCode, int? notificationId = null, int take = 15, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<NotifyMeLogItemVm>>(_exception);

        public Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetTypeOptionsAsync(string connectionString, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<NotifyMeLookupOptionVm>>(_exception);

        public Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetPriorityOptionsAsync(string connectionString, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<NotifyMeLookupOptionVm>>(_exception);

        public Task<int> SaveNotificationAsync(string connectionString, int companyCode, NotifyMeDraftVm draft, string updatedBy, CancellationToken cancellationToken = default)
            => Task.FromException<int>(_exception);
    }

    private sealed class CapturingLoggerManager : ILoggerManager
    {
        public string? LastWarning { get; private set; }

        public void LogInfo(string message) { }
        public void LogWarning(string message) => LastWarning = message;
        public void LogDebug(string message) { }
        public void LogError(string message) { }
    }

    private static SqlException CreateSqlException(int number, string message)
    {
        var errorCollection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        var error = CreateSqlError(number, message);

        typeof(SqlErrorCollection)
            .GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(errorCollection, new[] { error });

        var constructor = typeof(SqlException)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(candidate => candidate.GetParameters().Any(parameter => parameter.ParameterType == typeof(SqlErrorCollection)))
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();

        var arguments = constructor
            .GetParameters()
            .Select(parameter => CreateSqlClientConstructorArgument(parameter, number, message, errorCollection))
            .ToArray();

        return (SqlException)constructor.Invoke(arguments);
    }

    private static SqlError CreateSqlError(int number, string message)
    {
        var constructor = typeof(SqlError)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();

        var arguments = constructor
            .GetParameters()
            .Select(parameter => CreateSqlClientConstructorArgument(parameter, number, message))
            .ToArray();

        return (SqlError)constructor.Invoke(arguments);
    }

    private static object? CreateSqlClientConstructorArgument(
        ParameterInfo parameter,
        int number,
        string message,
        SqlErrorCollection? errorCollection = null)
    {
        if (parameter.ParameterType == typeof(SqlErrorCollection))
            return errorCollection;

        if (parameter.ParameterType == typeof(Guid))
            return Guid.NewGuid();

        if (parameter.Name is "infoNumber" or "number")
            return number;

        if (parameter.Name is "errorMessage" or "message")
            return message;

        if (parameter.Name == "server")
            return "server";

        if (parameter.Name == "procedure")
            return "procedure";

        if (parameter.Name == "batchIndex")
            return -1;

        if (parameter.HasDefaultValue)
            return parameter.DefaultValue;

        if (parameter.ParameterType == typeof(string))
            return string.Empty;

        return parameter.ParameterType.IsValueType
            ? Activator.CreateInstance(parameter.ParameterType)
            : null;
    }

    private sealed class NoopLoggerManager : ILoggerManager
    {
        public void LogInfo(string message)
        {
        }

        public void LogWarning(string message)
        {
        }

        public void LogDebug(string message)
        {
        }

        public void LogError(string message)
        {
        }
    }
}
