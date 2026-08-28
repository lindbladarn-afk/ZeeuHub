using Entities.Application;
using Entities.Purchase;
using Microsoft.AspNetCore.Http;
using Repository.Contracts;
using WebApp.Helpers;
using WebApp.Services;
using WebApp.Services.Application;
using WebApp.Services.Purchase.Context;

namespace WebApp.Tests;

// Verifies that Purchase builds its request context from the active portal session.
public sealed class PurchaseContextServiceTests
{
    [Fact]
    public async Task BuildAsync_Returns_Runtime_Context_And_Lookups()
    {
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        httpContext.Session.Set("UserObject", new UserSession
        {
            UserId = "user-1",
            FirstName = "Session",
            LastName = "User",
            PersSign = "SESSION"
        });
        var repository = new FakePurchaseRepository();
        var service = new PurchaseContextService(
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeApplicationHelper(),
            new FakeRuntimeContextService(OperationResult<JeevesRuntimeContext>.Ok(new JeevesRuntimeContext
            {
                UserId = "user-1",
                CompanyId = Guid.NewGuid(),
                CompanyCode = 100,
                ConnectionString = "Server=jeeves;",
                FirstName = "Runtime",
                LastName = "User",
                PersSign = "RUNTIME"
            })),
            repository);

        var context = await service.BuildAsync();

        Assert.Equal("Server=jeeves;", context.ConnectionString);
        Assert.Equal(100, context.CompanyCode);
        Assert.Equal("RUNTIME", context.PersSign);
        Assert.Equal("Runtime User", context.FullName);
        Assert.Single(context.Suppliers);
        Assert.Single(context.Articles);
        Assert.Single(context.Contacts);
        Assert.Equal(100, repository.LastCompanyCode);
    }

    [Fact]
    public async Task BuildAsync_Throws_When_Session_Is_Missing()
    {
        var service = new PurchaseContextService(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = new TestSession() } },
            new FakeApplicationHelper(),
            new FakeRuntimeContextService(OperationResult<JeevesRuntimeContext>.Fail("missing")),
            new FakePurchaseRepository());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildAsync());

        Assert.Equal("The user could not be loaded", exception.Message);
    }

    [Fact]
    public async Task BuildAsync_Throws_When_Runtime_Context_Fails()
    {
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        httpContext.Session.Set("UserObject", new UserSession { UserId = "user-1", PersSign = "AA" });
        var service = new PurchaseContextService(
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeApplicationHelper(),
            new FakeRuntimeContextService(OperationResult<JeevesRuntimeContext>.Fail("failed")),
            new FakePurchaseRepository());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildAsync());

        Assert.Equal("Company context could not be resolved", exception.Message);
    }

    private sealed class FakeRuntimeContextService : IJeevesRuntimeContextService
    {
        private readonly OperationResult<JeevesRuntimeContext> _result;

        public FakeRuntimeContextService(OperationResult<JeevesRuntimeContext> result)
        {
            _result = result;
        }

        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class FakeApplicationHelper : IApplicationHelper
    {
        public Task<bool> AddUserToSession(string email) => Task.FromResult(false);
    }

    private sealed class FakePurchaseRepository : IPurchaseRepository
    {
        public int? LastCompanyCode { get; private set; }

        public Task<IEnumerable<IPurchaseOrderVM>> GetAllSuppliersAsync(string connectionString, int? companyCode)
        {
            LastCompanyCode = companyCode;
            return Task.FromResult<IEnumerable<IPurchaseOrderVM>>(new[]
            {
                new PurchaseOrderVM
                {
                    SupplierNumber = "S1",
                    SupplierName = "Supplier",
                    Currency = "SEK",
                    PurchaseOrderType = "Expense",
                    OrderRows = new List<PurchaseOrderRowVM>(),
                    Contacts = new List<PurchaseSupplierContactVM>()
                }
            });
        }

        public Task<IEnumerable<IPurchaseSuppliersAutoCompleteDto>> GetAutocompleteSuppliersAsync(string connectionString, int companyCode)
            => Task.FromResult<IEnumerable<IPurchaseSuppliersAutoCompleteDto>>(Array.Empty<IPurchaseSuppliersAutoCompleteDto>());

        public Task<IEnumerable<IPurchaseSupplierContactVM>> GetAllContactsAsync(string connectionString, int? companyCode)
            => Task.FromResult<IEnumerable<IPurchaseSupplierContactVM>>(new[]
            {
                new PurchaseSupplierContactVM { ContactNumber = "C1", SupplierNumber = "S1" }
            });

        public Task<IEnumerable<IPurchaseArticleVM>> GetPurchaseArticlesAsync(string connectionString, int? companyCode)
            => Task.FromResult<IEnumerable<IPurchaseArticleVM>>(new[]
            {
                new PurchaseArticleVM
                {
                    ArticleNumber = "A1",
                    ArticleDescription = "Article",
                    Unit = "PCS",
                    DefaultAccount = "4000",
                    DefaultCostCenter = "100"
                }
            });

        public IPurchaseOrderResultDto CreateStockDelivery(string connectionString, string perssign, int? companyCode, IPurchaseOrderVM purchaseOrder)
            => new PurchaseOrderResultDto();

        public Task<IEnumerable<IPurchaseOrderVM>> GetMyPurchaseOrdersAsync(string connectionString, int? companyCode, string perssign)
            => Task.FromResult<IEnumerable<IPurchaseOrderVM>>(Array.Empty<IPurchaseOrderVM>());

        public Task<IPurchaseOrderVM> GetPurchaseOrderAsync(string connectionString, int? companyCode, string perssign, int orderNumber)
            => Task.FromResult<IPurchaseOrderVM>(new PurchaseOrderVM());

        public IPurchaseOrderResultDto CreatePurchaseOrder(string connectionString, string perssign, string? userFullName, int? companyCode, IPurchaseOrderVM purchaseOrder)
            => new PurchaseOrderResultDto();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new();

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _values.Keys;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Clear() => _values.Clear();
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
