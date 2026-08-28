using Entities.Application;
using Entities.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NotificationService;
using Repository.Contracts;
using WebApp.Controllers;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Models.Identity;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.NotifyMe;
using WebApp.Services;
using WebApp.ViewModels.NotifyMe;
using WebApp.ViewModels.Shared;

namespace WebApp.Tests;

// NotifyMe controller tests cover customer-facing error text on the editor flow.
public sealed class NotifyMeControllerSafetyTests
{
    [Fact]
    public async Task SaveEditor_Returns_Generic_Message_When_Save_Fails()
    {
        var controller = CreateController(new ThrowingNotifyMeService());

        var result = await controller.SaveEditor(
            new NotifyMeDraftVm
            {
                Description = "Test",
                WarningText = "Test",
                TypeCode = "TYPE",
                PriorityCode = "P1",
                SchemaCode = "SCHEMA",
                ScheduleCode = "SCHED",
                StartDate = DateTime.UtcNow,
                SqlPreview = "select 1",
                IsActive = false
            },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<NotifyMeCreatePrototypeVm>(view.Model);
        Assert.Contains("ett tekniskt fel", model.StatusMessage);
        Assert.DoesNotContain("authorization=secret-value", model.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static NotifyMeController CreateController(INotifyMeService notifyMeService)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        httpContext.Session.Set("UserObject", new UserSession
        {
            UserId = "user-1",
            Email = "user@example.com",
            CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            JeevesActiveCompany = 1000,
            CompanyName = "Acme",
            PersSign = "APS"
        });

        var services = new ServiceCollection()
            .AddSingleton<IJeevesRuntimeContextService>(new FakeJeevesRuntimeContextService())
            .BuildServiceProvider();
        httpContext.RequestServices = services;

        var controller = new NotifyMeController(
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeApplicationUserRepository(),
            new FakeNotificationManager(),
            notifyMeService,
            new FakeNotifyMeDemoService(),
            new DummyStringLocalizer(),
            new FakeApplicationHelper(),
            null!);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());

        return controller;
    }

    private sealed class ThrowingNotifyMeService : INotifyMeService
    {
        public Task<NotifyMeOverviewVm> GetOverviewAsync(string? connectionString, int? companyCode, string? search = null, string? status = null, string? type = null, string? priority = null, int page = 1, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<NotifyMeStatisticsVm> GetStatisticsAsync(string? connectionString, int? companyCode, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<NotifyMeHistoryPageVm> GetHistoryAsync(string? connectionString, int? companyCode, int? historyNotificationId = null, string? historySearch = null, int page = 1, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<NotifyMeTemplateLibraryVm> GetTemplateLibraryAsync(string? connectionString, int? companyCode, string? search = null, string? category = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<NotifyMeDetailsPageVm> GetDetailsAsync(string? connectionString, int? companyCode, int notificationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<NotifyMeCreatePrototypeVm> GetCreatePrototypeAsync(string? connectionString, int? companyCode, int? notificationId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new NotifyMeCreatePrototypeVm());

        public Task<NotifyMeTestRunResultVm> RunTestNotificationAsync(string? connectionString, int? companyCode, int notificationId, string overrideRecipient, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> SaveNotificationAsync(string? connectionString, int? companyCode, NotifyMeDraftVm draft, string updatedBy, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("authorization=secret-value");
    }

    private sealed class FakeNotifyMeDemoService : INotifyMeDemoService
    {
        public Task<NotifyMeTemplateLibraryVm> GetTemplateLibraryAsync(int? companyCode, string? search = null, string? category = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new NotifyMeTemplateLibraryVm());

        public Task<NotifyMeStatisticsVm> GetStatisticsAsync(int? companyCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new NotifyMeStatisticsVm());

        public NotifyMeTemplateVm? GetTemplate(string? templateKey) => null;
    }

    private sealed class FakeApplicationUserRepository : IApplicationUserRepository
    {
        public Task<IUser> GetUserAsync(string userId)
            => Task.FromResult<IUser>(new User { Id = userId, Email = "user@example.com" });
    }

    private sealed class FakeApplicationHelper : IApplicationHelper
    {
        public Task<bool> AddUserToSession(string email) => Task.FromResult(true);
    }

    private sealed class FakeNotificationManager : INotificationManager
    {
        public Task Success(string message) => Task.CompletedTask;
        public Task Error(string message) => Task.CompletedTask;
        public Task Warning(string message) => Task.CompletedTask;
        public Task Information(string message) => Task.CompletedTask;
        public Task HubStatus(string message) => Task.CompletedTask;
        public Task TemporaryPassword(string email, string temporaryPassword) => Task.CompletedTask;
    }

    private sealed class FakeJeevesRuntimeContextService : IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Ok(new JeevesRuntimeContext
            {
                UserId = sessionUser?.UserId ?? "user-1",
                CompanyId = sessionUser?.CompanyId ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                CompanyCode = sessionUser?.JeevesActiveCompany ?? 1000,
                ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
                CompanyName = sessionUser?.CompanyName ?? "Acme",
                Email = sessionUser?.Email,
                PersSign = sessionUser?.PersSign
            }));
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, $"{name}: {string.Join(", ", arguments)}");
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value.ToArray();
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, object> LoadTempData(HttpContext context) => _values;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _values.Clear();
            foreach (var value in values)
            {
                _values[value.Key] = value.Value;
            }
        }
    }
}
