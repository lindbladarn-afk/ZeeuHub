using Entities.Application;
using Entities.Contracts;
using Entities.Purchase;
using LoggerService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using NotificationService;
using Repository.Contracts;
using WebApp.Controllers;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Models;
using WebApp.Models.Purchase.Demo;
using WebApp.Services.Purchase.Demo;
using WebApp.Services.Purchase.Lookup;
using WebApp.Services.Purchase.Orders;

namespace WebApp.Tests;

public sealed class PurchaseControllerOrderAcknowledgementTests
{
    [Fact]
    public void OrderAcknowledgement_Returns_View_And_Shows_Error_When_No_Rows_Are_Selected()
    {
        var fixtures = CreateController();
        var model = CreateOrderModel(selectedRows: Array.Empty<int>());

        var result = fixtures.Controller.OrderAcknowledgement(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("PurchaseOrder", viewResult.ViewName);
        Assert.Same(model, viewResult.Model);
        Assert.Equal("Markera minst en rad innan du skickar till Jeeves.", fixtures.Notifications.LastError);
    }

    [Fact]
    public void OrderAcknowledgement_Redirects_After_Selected_Rows_Are_Submitted()
    {
        var fixtures = CreateController();
        var model = CreateOrderModel(selectedRows: new[] { 10, 30 });

        var result = fixtures.Controller.OrderAcknowledgement(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PurchaseController.PurchaseOrders), redirectResult.ActionName);
        Assert.Equal("2 rader från order 12345 skickades till Jeeves.", fixtures.Notifications.LastSuccess);
        Assert.Contains("submitted with 2 selected rows", fixtures.Logger.LastInfo ?? string.Empty);
    }

    [Fact]
    public async Task PurchaseOrders_Returns_ModuleUnavailable_And_Logs_SupportId_On_Failure()
    {
        var fixtures = CreateController(new ThrowingPurchaseOrderService());

        var result = await fixtures.Controller.PurchaseOrders();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("ModuleUnavailable", viewResult.ViewName);
        Assert.Contains("SupportId=", fixtures.Logger.LastError ?? string.Empty);
        Assert.DoesNotContain("authorization=secret-value", fixtures.Logger.LastError ?? string.Empty);
    }

    [Fact]
    public async Task CreatePurchaseOrder_Returns_View_Logs_SupportId_And_Shows_Inline_Error_When_Command_Fails()
    {
        var fixtures = CreateController(new FailingCreatePurchaseOrderService("Account  does not exist [ko]; authorization=secret-value"));
        var model = CreateOrderModel(selectedRows: Array.Empty<int>());

        var result = await fixtures.Controller.CreatePurchaseOrder(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.Contains("CreatePurchaseOrder rejected by Jeeves", fixtures.Logger.LastError ?? string.Empty);
        Assert.Contains("SupportId=", fixtures.Logger.LastError ?? string.Empty);
        Assert.DoesNotContain("authorization=secret-value", fixtures.Logger.LastError ?? string.Empty);
        Assert.Contains("Jeeves nekade inköpsordern", fixtures.Notifications.LastError);
        Assert.Contains("Referens:", fixtures.Notifications.LastError);
        Assert.Contains("Jeeves nekade inköpsordern", fixtures.Controller.TempData[Alert.DANGER]?.ToString());
    }

    private static ControllerFixtures CreateController(IPurchaseOrderService? purchaseOrderService = null)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = CreateHttpContext()
        };

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var dbContext = new ApplicationDbContext(dbOptions);

        var notifications = new FakeNotificationManager();
        var logger = new FakeLoggerManager();
        var controller = new PurchaseController(
            httpContextAccessor,
            new FakeApplicationUserRepository(),
            notifications,
            new FakePurchaseLookupService(),
            purchaseOrderService ?? new FakePurchaseOrderService(),
            new FakePurchaseDemoModeService(false),
            new FakePurchaseDemoDataService(),
            new DummyStringLocalizer(),
            logger,
            new FakeApplicationHelper(),
            dbContext);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContextAccessor.HttpContext
        };
        controller.TempData = new TempDataDictionary(httpContextAccessor.HttpContext, new TestTempDataProvider());

        return new ControllerFixtures(controller, notifications, logger);
    }

    private static PurchaseOrderVM CreateOrderModel(IReadOnlyCollection<int> selectedRows)
        => new()
        {
            OrderNumber = 12345,
            SupplierNumber = "1000",
            SupplierName = "Best",
            Currency = "SEK",
            OrderRows = new List<PurchaseOrderRowVM>
            {
                new()
                {
                    RowNumber = 10,
                    ArticleNumber = "10",
                    ArticleDescription = "Stol",
                    Quantity = 20m,
                    RecievedQuantity = 0m,
                    DeliveryDate = DateTime.Parse("2026-05-15"),
                    ConfirmedDeliveryDate = DateTime.Parse("2026-05-15"),
                    Unit = "st",
                    Price = 100m,
                    Discount = 0m,
                    Account = "6550",
                    CostCenter = "100",
                    AddToStock = selectedRows.Contains(10)
                },
                new()
                {
                    RowNumber = 20,
                    ArticleNumber = "20",
                    ArticleDescription = "Bord",
                    Quantity = 20m,
                    RecievedQuantity = 0m,
                    DeliveryDate = DateTime.Parse("2026-05-20"),
                    ConfirmedDeliveryDate = null,
                    Unit = "st",
                    Price = 100m,
                    Discount = 0m,
                    Account = "6550",
                    CostCenter = "100",
                    AddToStock = selectedRows.Contains(20)
                },
                new()
                {
                    RowNumber = 30,
                    ArticleNumber = "30",
                    ArticleDescription = "Pall",
                    Quantity = 20m,
                    RecievedQuantity = 0m,
                    DeliveryDate = DateTime.Parse("2026-05-25"),
                    ConfirmedDeliveryDate = DateTime.Parse("2026-05-27"),
                    Unit = "st",
                    Price = 100m,
                    Discount = 0m,
                    Account = "6550",
                    CostCenter = "100",
                    AddToStock = selectedRows.Contains(30)
                }
            }
        };

    private sealed record ControllerFixtures(
        PurchaseController Controller,
        FakeNotificationManager Notifications,
        FakeLoggerManager Logger);

    private sealed class FakeNotificationManager : INotificationManager
    {
        public string? LastSuccess { get; private set; }
        public string? LastError { get; private set; }

        public Task Success(string message)
        {
            LastSuccess = message;
            return Task.CompletedTask;
        }

        public Task Error(string message)
        {
            LastError = message;
            return Task.CompletedTask;
        }

        public Task Warning(string message) => Task.CompletedTask;
        public Task Information(string message) => Task.CompletedTask;
        public Task HubStatus(string message) => Task.CompletedTask;
        public Task TemporaryPassword(string email, string temporaryPassword) => Task.CompletedTask;
    }

    private sealed class FakeLoggerManager : ILoggerManager
    {
        public string? LastInfo { get; private set; }
        public string? LastError { get; private set; }

        public void LogInfo(string message) => LastInfo = message;
        public void LogWarning(string message) { }
        public void LogDebug(string message) { }
        public void LogError(string message) => LastError = message;
    }

    private sealed class FakeApplicationUserRepository : IApplicationUserRepository
    {
        public Task<IUser> GetUserAsync(string userId)
            => Task.FromResult<IUser>(new User { Id = userId });
    }

    private sealed class FakeApplicationHelper : IApplicationHelper
    {
        public Task<bool> AddUserToSession(string email) => Task.FromResult(true);
    }

    private sealed class FakePurchaseLookupService : IPurchaseLookupService
    {
        public Task<IReadOnlyList<WebApp.ViewModels.Purchase.PurchaseSupplierLookupItem>> SearchSuppliersAsync(string? searchString, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WebApp.ViewModels.Purchase.PurchaseSupplierLookupItem>>(Array.Empty<WebApp.ViewModels.Purchase.PurchaseSupplierLookupItem>());

        public Task<IReadOnlyList<WebApp.ViewModels.Purchase.PurchaseArticleLookupItem>> SearchArticlesAsync(string? searchString, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WebApp.ViewModels.Purchase.PurchaseArticleLookupItem>>(Array.Empty<WebApp.ViewModels.Purchase.PurchaseArticleLookupItem>());
    }

    private sealed class FakePurchaseOrderService : IPurchaseOrderService
    {
        public Task<IEnumerable<IPurchaseOrderVM>> GetMyPurchaseOrdersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<IPurchaseOrderVM>>(Array.Empty<IPurchaseOrderVM>());

        public Task<IPurchaseOrderVM> GetPurchaseOrderAsync(int orderNumber, CancellationToken cancellationToken = default)
            => Task.FromResult<IPurchaseOrderVM>(new PurchaseOrderVM());

        public Task<PurchaseOrderVM> CreateEmptyPurchaseOrderAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new PurchaseOrderVM());

        public Task<PurchaseOrderCommandResult> CreatePurchaseOrderAsync(PurchaseOrderVM purchaseOrder, CancellationToken cancellationToken = default)
            => Task.FromResult(new PurchaseOrderCommandResult());

        public Task<IPurchaseOrderResultDto> CreateStockDeliveryAsync(PurchaseOrderVM purchaseOrder, CancellationToken cancellationToken = default)
            => Task.FromResult<IPurchaseOrderResultDto>(new PurchaseOrderResultDto());
    }

    private sealed class ThrowingPurchaseOrderService : IPurchaseOrderService
    {
        public Task<IEnumerable<IPurchaseOrderVM>> GetMyPurchaseOrdersAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("authorization=secret-value");

        public Task<IPurchaseOrderVM> GetPurchaseOrderAsync(int orderNumber, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PurchaseOrderVM> CreateEmptyPurchaseOrderAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PurchaseOrderCommandResult> CreatePurchaseOrderAsync(PurchaseOrderVM purchaseOrder, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IPurchaseOrderResultDto> CreateStockDeliveryAsync(PurchaseOrderVM purchaseOrder, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FailingCreatePurchaseOrderService : IPurchaseOrderService
    {
        private readonly string _message;

        public FailingCreatePurchaseOrderService(string message)
        {
            _message = message;
        }

        public Task<IEnumerable<IPurchaseOrderVM>> GetMyPurchaseOrdersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<IPurchaseOrderVM>>(Array.Empty<IPurchaseOrderVM>());

        public Task<IPurchaseOrderVM> GetPurchaseOrderAsync(int orderNumber, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PurchaseOrderVM> CreateEmptyPurchaseOrderAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PurchaseOrderCommandResult> CreatePurchaseOrderAsync(PurchaseOrderVM purchaseOrder, CancellationToken cancellationToken = default)
            => Task.FromResult(new PurchaseOrderCommandResult
            {
                Success = false,
                Message = _message
            });

        public Task<IPurchaseOrderResultDto> CreateStockDeliveryAsync(PurchaseOrderVM purchaseOrder, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakePurchaseDemoModeService : IPurchaseDemoModeService
    {
        private bool _enabled;

        public FakePurchaseDemoModeService(bool enabled)
        {
            _enabled = enabled;
        }

        public bool IsEnabled() => _enabled;
        public void SetEnabled(bool enabled) => _enabled = enabled;
    }

    private sealed class FakePurchaseDemoDataService : IPurchaseDemoDataService
    {
        public Task<PurchaseDemoData> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new PurchaseDemoData { Orders = new List<PurchaseOrderVM>() });

        public Task<PurchaseOrderVM?> FindOrderAsync(int orderNumber, CancellationToken cancellationToken = default)
            => Task.FromResult<PurchaseOrderVM?>(null);
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<ISessionFeature>(new TestSessionFeature());
        return context;
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
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
}
