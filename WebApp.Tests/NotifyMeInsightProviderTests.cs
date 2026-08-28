// Tests the NotifyMe signals that feed ZeeU Action Center.
using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Repositories.NotifyMe;
using WebApp.Services.ActionCenter;
using WebApp.Services.Application;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Tests;

// Covers the customer-facing NotifyMe signals that feed ZeeU Action Center.
public sealed class NotifyMeInsightProviderTests
{
    [Fact]
    public async Task GetInsightsAsync_Returns_Individual_DueNow_Cards_For_Small_Rule_Set()
    {
        var repository = new FakeNotifyMeRepository
        {
            Notifications =
            [
                BuildNotification(101, "Order utan checklista"),
                BuildNotification(102, "Låg lagernivå")
            ]
        };
        var provider = new NotifyMeInsightProvider(repository);

        var insights = (await provider.GetInsightsAsync(BuildUser(), BuildRuntimeContext(), CancellationToken.None)).ToList();

        Assert.Equal(2, insights.Count);
        Assert.Contains(insights, item => item.Key == "notifyme-due-now-101" && item.LinkUrl == "/NotifyMe/Detail/101");
        Assert.Contains(insights, item => item.Key == "notifyme-due-now-102" && item.LinkUrl == "/NotifyMe/Detail/102");
        Assert.All(insights, item => Assert.Equal(ActionCenterPriority.Medium, item.Priority));
    }

    [Fact]
    public async Task GetInsightsAsync_Returns_Aggregated_DueNow_Card_For_Larger_Rule_Set()
    {
        var repository = new FakeNotifyMeRepository
        {
            Notifications =
            [
                BuildNotification(101, "Regel 101"),
                BuildNotification(102, "Regel 102"),
                BuildNotification(103, "Regel 103"),
                BuildNotification(104, "Regel 104")
            ]
        };
        var provider = new NotifyMeInsightProvider(repository);

        var insights = (await provider.GetInsightsAsync(BuildUser(), BuildRuntimeContext(), CancellationToken.None)).ToList();

        var insight = Assert.Single(insights);
        Assert.Equal("notifyme-due-now", insight.Key);
        Assert.Equal("/NotifyMe", insight.LinkUrl);
        Assert.Equal(ActionCenterPriority.High, insight.Priority);
    }

    [Fact]
    public async Task GetInsightsAsync_Returns_Individual_ManualAction_Card_With_Latest_Status()
    {
        var repository = new FakeNotifyMeRepository
        {
            Notifications =
            [
                BuildNotification(201, "Felande SQL-regel", isDueNow: false)
            ],
            LogEntries =
            [
                new NotifyMeLogItemVm
                {
                    NotificationId = 201,
                    SentAt = new DateTime(2026, 7, 1, 8, 30, 0, DateTimeKind.Utc),
                    ExecutionStatus = "Manuell åtgärd"
                }
            ]
        };
        var provider = new NotifyMeInsightProvider(repository);

        var insights = (await provider.GetInsightsAsync(BuildUser(), BuildRuntimeContext(), CancellationToken.None)).ToList();

        var insight = Assert.Single(insights);
        Assert.Equal("notifyme-manual-action-201", insight.Key);
        Assert.Equal(ActionCenterPriority.High, insight.Priority);
        Assert.Equal("/NotifyMe/Detail/201", insight.LinkUrl);
        Assert.Contains("Manuell åtgärd", insight.Description);
    }

    private static NotifyMeListItemVm BuildNotification(int id, string description, bool isDueNow = true)
        => new()
        {
            NotificationId = id,
            Description = description,
            TypeLabel = "Order",
            ScheduleLabel = "Varje timme",
            IsActive = true,
            IsDueNow = isDueNow,
            NextExecutionAt = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            WarningCount = 2
        };

    private static UserSession BuildUser()
        => new()
        {
            UserId = "user-1",
            JeevesActiveCompany = 100
        };

    private static JeevesRuntimeContext BuildRuntimeContext()
        => new()
        {
            UserId = "user-1",
            CompanyCode = 100,
            CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ConnectionString = "Server=test;Database=tenant;"
        };

    private sealed class FakeNotifyMeRepository : INotifyMeRepository
    {
        public IReadOnlyList<NotifyMeListItemVm> Notifications { get; init; } = Array.Empty<NotifyMeListItemVm>();
        public IReadOnlyList<NotifyMeLogItemVm> LogEntries { get; init; } = Array.Empty<NotifyMeLogItemVm>();

        public Task<IReadOnlyList<NotifyMeListItemVm>> GetNotificationsAsync(string connectionString, int companyCode, CancellationToken cancellationToken = default)
            => Task.FromResult(Notifications);

        public Task<NotifyMeDetailsVm?> GetNotificationAsync(string connectionString, int companyCode, int notificationId, CancellationToken cancellationToken = default)
            => Task.FromResult<NotifyMeDetailsVm?>(null);

        public Task<IReadOnlyList<NotifyMeLogItemVm>> GetRecentLogEntriesAsync(string connectionString, int companyCode, int? notificationId = null, int take = 15, CancellationToken cancellationToken = default)
            => Task.FromResult(LogEntries);

        public Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetTypeOptionsAsync(string connectionString, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NotifyMeLookupOptionVm>>(Array.Empty<NotifyMeLookupOptionVm>());

        public Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetPriorityOptionsAsync(string connectionString, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NotifyMeLookupOptionVm>>(Array.Empty<NotifyMeLookupOptionVm>());

        public Task<int> SaveNotificationAsync(string connectionString, int companyCode, NotifyMeDraftVm draft, string updatedBy, CancellationToken cancellationToken = default)
            => Task.FromResult(draft.NotificationId ?? 0);
    }
}
