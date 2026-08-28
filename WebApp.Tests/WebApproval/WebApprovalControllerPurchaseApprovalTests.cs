using Entities.Application;
using Entities.Dto;
using Entities.Contracts;
using Entities.User;
using Entities.ViewModels.WebApproval;
using LoggerService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using NotificationService;
using Repository.Contracts;
using WebApp.Controllers;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Models.Application;
using WebApp.Models.Identity;
using WebApp.Services.Application;
using WebApp.Services.Admin.ApprovalChains;
using WebApp.Services;
using WebApp.ViewModels.Admin.ApprovalChains;
using WebApp.ViewModels.Shared;

namespace WebApp.Tests;

public sealed class WebApprovalControllerPurchaseApprovalTests
{
    [Fact]
    public async Task PurchaseApproval_Returns_ModuleUnavailable_And_Logs_SupportId_On_Failure()
    {
        var fixtures = CreateController(new ThrowingPurchaseRepository());

        var result = await fixtures.Controller.PurchaseApproval();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("ModuleUnavailable", viewResult.ViewName);
        Assert.Contains("SupportId=", fixtures.Logger.LastError ?? string.Empty);
        Assert.DoesNotContain("authorization=secret-value", fixtures.Logger.LastError ?? string.Empty);
        Assert.Single(fixtures.TechnicalNotifications.Requests);
    }

    private static ControllerFixtures CreateController(IWebApprovalPurchaseRepository purchaseRepository)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        httpContext.Session.Set("UserObject", new UserSession
        {
            UserId = "user-1",
            Email = "user@example.com",
            PersSign = "ABC123",
            CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            JeevesActiveCompany = 1000,
            CompanyName = "Acme"
        });

        var services = new ServiceCollection();
        services.AddSingleton<IJeevesRuntimeContextService>(new FakeJeevesRuntimeContextService());
        services.AddSingleton<IJeevesCompanyAccessService>(new FakeJeevesCompanyAccessService());
        httpContext.RequestServices = services.BuildServiceProvider();

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var dbContext = new ApplicationDbContext(dbOptions);
        var logger = new CapturingLoggerManager();
        var technicalNotifications = new RecordingTechnicalErrorNotificationService();

        var controller = new WebApprovalController(
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeApplicationUserRepository(),
            new FakeOrderRepository(),
            purchaseRepository,
            new FakePriceListRepository(),
            logger,
            new FakeNotificationManager(),
            technicalNotifications,
            new DummyStringLocalizer(),
            new FakeApprovalChainDesignerService(),
            new FakeApplicationHelper(),
            dbContext);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());

        return new ControllerFixtures(
            controller,
            logger,
            technicalNotifications);
    }

    private sealed record ControllerFixtures(
        WebApprovalController Controller,
        CapturingLoggerManager Logger,
        RecordingTechnicalErrorNotificationService TechnicalNotifications);

    private sealed class ThrowingPurchaseRepository : IWebApprovalPurchaseRepository
    {
        public Task<IEnumerable<WebApprovalPurchaseOrderVM>> GetAllPurchaseAttestOrdersAsync(string connectionString, int? foretagKod, string emailAddress, int? status = null)
            => throw new InvalidOperationException("authorization=secret-value");

        public Task<WebApprovalPurchaseOrderVM> GetAttestPurchaseOrderWithRowsAsync(string connectionString, Guid id)
            => throw new NotSupportedException();

        public Task UpdateOrderStatusAsync(string connectionString, Guid orderId, string attestStatus, string approvedBy, string? message = null)
            => throw new NotSupportedException();
    }

    private sealed class FakeOrderRepository : IWebApprovalOrderRepository
    {
        public Task<IEnumerable<WebApprovalSaleOrderVM>> GetAllSalesAttestOrdersAsync(string connectionString, int? foretagKod, string emailAddress, int? status = null)
            => Task.FromResult<IEnumerable<WebApprovalSaleOrderVM>>(Array.Empty<WebApprovalSaleOrderVM>());

        public Task<WebApprovalSaleOrderVM> GetAttestOrderWithRowsAsync(string connectionString, Guid id)
            => Task.FromResult(new WebApprovalSaleOrderVM());

        public Task UpdateAttestOrderStatusAsync(string connectionString, Guid id, string action, string? message, string approvedBy)
            => Task.CompletedTask;
    }

    private sealed class FakePriceListRepository : IWebApprovalPriceListRepository
    {
        public Task<IEnumerable<WebApprovalPriceListDto>> GetPriceListWithRowsAsync(string connectionString, int? foretagKod, string? persSign, int? priceListId = null)
            => Task.FromResult<IEnumerable<WebApprovalPriceListDto>>(Array.Empty<WebApprovalPriceListDto>());

        public Task UpdatePriceListStatusAsync(string connectionString, Guid id, string attestStatus, string? message, string approvedBy)
            => Task.CompletedTask;
    }

    private sealed class FakeApplicationUserRepository : IApplicationUserRepository
    {
        public Task<IUser> GetUserAsync(string userId)
            => Task.FromResult<IUser>(new Entities.Application.User { Id = userId, Email = "user@example.com" });
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

    private sealed class CapturingLoggerManager : ILoggerManager
    {
        public string? LastError { get; private set; }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogDebug(string message) { }
        public void LogError(string message) => LastError = message;
    }

    private sealed class RecordingTechnicalErrorNotificationService : ITechnicalErrorNotificationService
    {
        public List<TechnicalErrorNotificationRequest> Requests { get; } = new();

        public Task NotifyAsync(TechnicalErrorNotificationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
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

    private sealed class FakeJeevesCompanyAccessService : IJeevesCompanyAccessService
    {
        public Task<IReadOnlyList<JeevesCompanyVM>> GetCompaniesAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<JeevesCompanyVM>>(Array.Empty<JeevesCompanyVM>());

        public Task<bool> HasCompanyAccessAsync(UserSession? sessionUser, int companyCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<int?> ResolveCompanyCodeAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(sessionUser?.JeevesActiveCompany);

        public Task<string> ResolveCompanyNameAsync(UserSession? sessionUser, int? companyCode, CancellationToken cancellationToken = default)
            => Task.FromResult(sessionUser?.CompanyName ?? string.Empty);

        public void Store(UserSession? sessionUser, IReadOnlyList<JeevesCompanyVM> companies)
        {
        }
    }

    private sealed class FakeApprovalChainDesignerService : IApprovalChainDesignerService
    {
        public Task<ApprovalChainDesignerViewModel> BuildAsync(short companyCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new ApprovalChainDesignerViewModel());
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
