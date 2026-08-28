using Entities.Purchase;
using Repository.Contracts;
using WebApp.Models.Purchase.Demo;
using WebApp.Services.Purchase.Demo;
using WebApp.Services.Purchase.Context;
using WebApp.Services.Purchase.Orders;

namespace WebApp.Tests;

// Verifies Purchase order service orchestration without MVC controller dependencies.
public sealed class PurchaseOrderServiceTests
{
    [Fact]
    public async Task CreateEmptyPurchaseOrderAsync_Validates_Context_And_Adds_First_Row()
    {
        var contextService = new FakePurchaseContextService(CreateContext());
        var service = new PurchaseOrderService(contextService, new FakePurchaseDemoDataService(Array.Empty<PurchaseOrderVM>()), new FakePurchaseDemoModeService(false), new FakePurchaseRepository());

        var result = await service.CreateEmptyPurchaseOrderAsync();

        Assert.True(contextService.BuildWasCalled);
        Assert.Single(result.OrderRows);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_Returns_Validation_Error_For_Unknown_Article()
    {
        var repository = new FakePurchaseRepository();
        var service = new PurchaseOrderService(new FakePurchaseContextService(CreateContext()), new FakePurchaseDemoDataService(Array.Empty<PurchaseOrderVM>()), new FakePurchaseDemoModeService(false), repository);
        var order = new PurchaseOrderVM
        {
            SupplierNumber = "S1",
            SupplierName = "Supplier",
            Currency = "SEK",
            OrderRows = new List<PurchaseOrderRowVM>
            {
                new() { ArticleNumber = "UNKNOWN" }
            }
        };

        var result = await service.CreatePurchaseOrderAsync(order);

        Assert.True(result.ValidationFailed);
        Assert.False(repository.CreatePurchaseOrderWasCalled);
        Assert.Equal("The article UNKNOWN is not flagged as an expence article in Jeeves", result.Message);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_Returns_Validation_Error_When_Row_CostCenter_Is_Missing()
    {
        var repository = new FakePurchaseRepository();
        var service = new PurchaseOrderService(new FakePurchaseContextService(CreateContext()), new FakePurchaseDemoDataService(Array.Empty<PurchaseOrderVM>()), new FakePurchaseDemoModeService(false), repository);
        var order = new PurchaseOrderVM
        {
            SupplierNumber = "S1",
            SupplierName = "Supplier",
            Currency = "SEK",
            OrderRows = new List<PurchaseOrderRowVM>
            {
                new() { ArticleNumber = "A1", Account = "5410" }
            }
        };

        var result = await service.CreatePurchaseOrderAsync(order);

        Assert.True(result.ValidationFailed);
        Assert.False(repository.CreatePurchaseOrderWasCalled);
        Assert.Equal("Orderrad 1 saknar kostnadsställe. Välj artikeln från listan så fylls kostnadsställe från Jeeves.", result.Message);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_Returns_Validation_Error_When_Row_Account_Is_Missing()
    {
        var repository = new FakePurchaseRepository();
        var service = new PurchaseOrderService(new FakePurchaseContextService(CreateContext()), new FakePurchaseDemoDataService(Array.Empty<PurchaseOrderVM>()), new FakePurchaseDemoModeService(false), repository);
        var order = new PurchaseOrderVM
        {
            SupplierNumber = "S1",
            SupplierName = "Supplier",
            Currency = "SEK",
            OrderRows = new List<PurchaseOrderRowVM>
            {
                new() { ArticleNumber = "A1", CostCenter = "100" }
            }
        };

        var result = await service.CreatePurchaseOrderAsync(order);

        Assert.True(result.ValidationFailed);
        Assert.False(repository.CreatePurchaseOrderWasCalled);
        Assert.Equal("Orderrad 1 saknar konto. Välj artikeln från listan så fylls konto från Jeeves.", result.Message);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_Calls_Repository_With_Runtime_Context()
    {
        var repository = new FakePurchaseRepository();
        var service = new PurchaseOrderService(new FakePurchaseContextService(CreateContext()), new FakePurchaseDemoDataService(Array.Empty<PurchaseOrderVM>()), new FakePurchaseDemoModeService(false), repository);
        var order = new PurchaseOrderVM
        {
            SupplierNumber = "S1",
            SupplierName = "Supplier",
            Currency = "SEK",
            OrderRows = new List<PurchaseOrderRowVM>
            {
                new() { ArticleNumber = "A1", Account = "5410", CostCenter = "100" }
            }
        };

        var result = await service.CreatePurchaseOrderAsync(order);

        Assert.True(result.Success);
        Assert.True(repository.CreatePurchaseOrderWasCalled);
        Assert.Equal("Server=jeeves;", repository.LastConnectionString);
        Assert.Equal("PS", repository.LastPersSign);
        Assert.Equal("Portal User", repository.LastFullName);
        Assert.Equal(100, repository.LastCompanyCode);
    }

    [Fact]
    public async Task GetMyPurchaseOrdersAsync_ReturnsDemoOrders_WhenDemoModeIsEnabled()
    {
        var demoOrders = new[]
        {
            new PurchaseOrderVM { OrderNumber = 1, SupplierNumber = "S1", SupplierName = "Demo", Currency = "SEK", OrderRows = new List<PurchaseOrderRowVM>(), Contacts = new List<PurchaseSupplierContactVM>() }
        };
        var repository = new FakePurchaseRepository();
        var service = new PurchaseOrderService(
            new FakePurchaseContextService(CreateContext()),
            new FakePurchaseDemoDataService(demoOrders),
            new FakePurchaseDemoModeService(true),
            repository);

        var result = await service.GetMyPurchaseOrdersAsync();

        Assert.Single(result);
        Assert.Equal(1, result.First().OrderNumber);
        Assert.False(repository.GetMyPurchaseOrdersWasCalled);
    }

    [Fact]
    public async Task GetPurchaseOrderAsync_ReturnsDemoOrder_WhenDemoModeIsEnabled()
    {
        var demoOrders = new[]
        {
            new PurchaseOrderVM { OrderNumber = 900080, SupplierNumber = "S1", SupplierName = "Demo", Currency = "SEK", OrderRows = new List<PurchaseOrderRowVM>(), Contacts = new List<PurchaseSupplierContactVM>() }
        };
        var repository = new FakePurchaseRepository();
        var service = new PurchaseOrderService(
            new FakePurchaseContextService(CreateContext()),
            new FakePurchaseDemoDataService(demoOrders),
            new FakePurchaseDemoModeService(true),
            repository);

        var result = await service.GetPurchaseOrderAsync(900080);

        Assert.Equal(900080, result.OrderNumber);
        Assert.False(repository.GetPurchaseOrderWasCalled);
    }

    private static PurchaseRequestContext CreateContext()
        => new()
        {
            ConnectionString = "Server=jeeves;",
            CompanyCode = 100,
            PersSign = "PS",
            FullName = "Portal User",
            Suppliers = Array.Empty<IPurchaseOrderVM>(),
            Contacts = Array.Empty<IPurchaseSupplierContactVM>(),
            Articles = new[]
            {
                new PurchaseArticleVM
                {
                    ArticleNumber = "A1",
                    ArticleDescription = "Article",
                    Unit = "PCS",
                    DefaultAccount = "4000",
                    DefaultCostCenter = "100"
                }
            }
        };

    private sealed class FakePurchaseContextService : IPurchaseContextService
    {
        private readonly PurchaseRequestContext _context;

        public FakePurchaseContextService(PurchaseRequestContext context)
        {
            _context = context;
        }

        public bool BuildWasCalled { get; private set; }

        public Task<PurchaseRequestContext> BuildAsync(CancellationToken cancellationToken = default)
        {
            BuildWasCalled = true;
            return Task.FromResult(_context);
        }
    }

    private sealed class FakePurchaseRepository : IPurchaseRepository
    {
        public bool GetMyPurchaseOrdersWasCalled { get; private set; }
        public bool GetPurchaseOrderWasCalled { get; private set; }
        public bool CreatePurchaseOrderWasCalled { get; private set; }
        public string? LastConnectionString { get; private set; }
        public string? LastPersSign { get; private set; }
        public string? LastFullName { get; private set; }
        public int? LastCompanyCode { get; private set; }

        public Task<IEnumerable<IPurchaseOrderVM>> GetAllSuppliersAsync(string connectionString, int? companyCode)
            => Task.FromResult<IEnumerable<IPurchaseOrderVM>>(Array.Empty<IPurchaseOrderVM>());

        public Task<IEnumerable<IPurchaseSuppliersAutoCompleteDto>> GetAutocompleteSuppliersAsync(string connectionString, int companyCode)
            => Task.FromResult<IEnumerable<IPurchaseSuppliersAutoCompleteDto>>(Array.Empty<IPurchaseSuppliersAutoCompleteDto>());

        public Task<IEnumerable<IPurchaseSupplierContactVM>> GetAllContactsAsync(string connectionString, int? companyCode)
            => Task.FromResult<IEnumerable<IPurchaseSupplierContactVM>>(Array.Empty<IPurchaseSupplierContactVM>());

        public Task<IEnumerable<IPurchaseArticleVM>> GetPurchaseArticlesAsync(string connectionString, int? companyCode)
            => Task.FromResult<IEnumerable<IPurchaseArticleVM>>(Array.Empty<IPurchaseArticleVM>());

        public IPurchaseOrderResultDto CreateStockDelivery(string connectionString, string perssign, int? companyCode, IPurchaseOrderVM purchaseOrder)
            => new PurchaseOrderResultDto { Success = true, OrderNumber = 12 };

        public Task<IEnumerable<IPurchaseOrderVM>> GetMyPurchaseOrdersAsync(string connectionString, int? companyCode, string perssign)
        {
            GetMyPurchaseOrdersWasCalled = true;
            return Task.FromResult<IEnumerable<IPurchaseOrderVM>>(Array.Empty<IPurchaseOrderVM>());
        }

        public Task<IPurchaseOrderVM> GetPurchaseOrderAsync(string connectionString, int? companyCode, string perssign, int orderNumber)
        {
            GetPurchaseOrderWasCalled = true;
            return Task.FromResult<IPurchaseOrderVM>(new PurchaseOrderVM());
        }

        public IPurchaseOrderResultDto CreatePurchaseOrder(
            string connectionString,
            string perssign,
            string? userFullName,
            int? companyCode,
            IPurchaseOrderVM purchaseOrder)
        {
            CreatePurchaseOrderWasCalled = true;
            LastConnectionString = connectionString;
            LastPersSign = perssign;
            LastFullName = userFullName;
            LastCompanyCode = companyCode;

            return new PurchaseOrderResultDto
            {
                Success = true,
                OrderNumber = 99
            };
        }
    }

    private sealed class FakePurchaseDemoModeService : IPurchaseDemoModeService
    {
        private readonly bool _enabled;

        public FakePurchaseDemoModeService(bool enabled)
        {
            _enabled = enabled;
        }

        public bool IsEnabled() => _enabled;

        public void SetEnabled(bool enabled) { }
    }

    private sealed class FakePurchaseDemoDataService : IPurchaseDemoDataService
    {
        private readonly IReadOnlyList<PurchaseOrderVM> _orders;

        public FakePurchaseDemoDataService(IReadOnlyList<PurchaseOrderVM> orders)
        {
            _orders = orders;
        }

        public Task<PurchaseDemoData> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new PurchaseDemoData { Orders = _orders.ToList() });

        public Task<PurchaseOrderVM?> FindOrderAsync(int orderNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(_orders.FirstOrDefault(x => x.OrderNumber == orderNumber));
    }
}
