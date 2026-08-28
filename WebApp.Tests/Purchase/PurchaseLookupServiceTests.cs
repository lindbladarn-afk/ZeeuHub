using Entities.Purchase;
using WebApp.Services.Purchase.Context;
using WebApp.Services.Purchase.Lookup;

namespace WebApp.Tests;

// Verifies Purchase autocomplete filtering and response mapping.
public sealed class PurchaseLookupServiceTests
{
    [Fact]
    public async Task SearchSuppliersAsync_Filters_Case_Insensitive_And_Attaches_Contacts()
    {
        var service = new PurchaseLookupService(new FakePurchaseContextService(new PurchaseRequestContext
        {
            ConnectionString = "Server=jeeves;",
            CompanyCode = 100,
            PersSign = "AA",
            FullName = "Test User",
            Suppliers = new[]
            {
                new PurchaseOrderVM
                {
                    SupplierNumber = "S1",
                    SupplierName = "Acme Tools",
                    Co = "Att Finance",
                    Street = "Main Street",
                    ZipCode = "12345",
                    City = "Gothenburg",
                    Country = "SE",
                    Currency = "SEK",
                    DeliveryZip = "54321",
                    PurchaseOrderType = "Expense",
                    OrderRows = new List<PurchaseOrderRowVM>(),
                    Contacts = new List<PurchaseSupplierContactVM>()
                },
                new PurchaseOrderVM
                {
                    SupplierNumber = "S2",
                    SupplierName = "Nordic Parts",
                    Currency = "EUR",
                    PurchaseOrderType = "Expense",
                    OrderRows = new List<PurchaseOrderRowVM>(),
                    Contacts = new List<PurchaseSupplierContactVM>()
                }
            },
            Articles = Array.Empty<IPurchaseArticleVM>(),
            Contacts = new[]
            {
                new PurchaseSupplierContactVM { SupplierNumber = "S1", ContactNumber = "C1", ContactName = "Anna" },
                new PurchaseSupplierContactVM { SupplierNumber = "S2", ContactNumber = "C2", ContactName = "Bo" }
            }
        }));

        var result = await service.SearchSuppliersAsync("acme");

        var supplier = Assert.Single(result);
        Assert.Equal("S1 - Acme Tools", supplier.Label);
        Assert.Equal("Acme Tools", supplier.Value);
        Assert.Equal("S1", supplier.SupplierNumber);
        Assert.Equal("Gothenburg", supplier.SupplierCity);
        Assert.Equal("54321", supplier.DeliveryZipCode);
        Assert.Single(supplier.Contacts);
        Assert.Equal("C1", supplier.Contacts[0].ContactNumber);
    }

    [Fact]
    public async Task SearchArticlesAsync_Filters_Case_Insensitive_And_Maps_Defaults()
    {
        var service = new PurchaseLookupService(new FakePurchaseContextService(new PurchaseRequestContext
        {
            ConnectionString = "Server=jeeves;",
            CompanyCode = 100,
            PersSign = "AA",
            FullName = "Test User",
            Suppliers = Array.Empty<IPurchaseOrderVM>(),
            Contacts = Array.Empty<IPurchaseSupplierContactVM>(),
            Articles = new[]
            {
                new PurchaseArticleVM
                {
                    ArticleNumber = "A1",
                    ArticleDescription = "Office Chair",
                    Unit = "PCS",
                    ProductGroupCode = 10,
                    DefaultAccount = "5410",
                    DefaultCostCenter = "100",
                    ExpenceArticle = true
                },
                new PurchaseArticleVM
                {
                    ArticleNumber = "A2",
                    ArticleDescription = "Laptop",
                    Unit = "PCS",
                    DefaultAccount = "5411",
                    DefaultCostCenter = "200"
                }
            }
        }));

        var result = await service.SearchArticlesAsync("chair");

        var article = Assert.Single(result);
        Assert.Equal("A1 - Office Chair", article.Label);
        Assert.Equal("Office Chair", article.Value);
        Assert.Equal("A1", article.ArticleNumber);
        Assert.Equal("5410", article.DefaultAccount);
        Assert.Equal("100", article.DefaultCostCenter);
        Assert.True(article.ExpenceArticle);
    }

    [Fact]
    public async Task SearchSuppliersAsync_Filters_By_SupplierNumber()
    {
        var service = new PurchaseLookupService(new FakePurchaseContextService(new PurchaseRequestContext
        {
            ConnectionString = "Server=jeeves;",
            CompanyCode = 100,
            PersSign = "AA",
            FullName = "Test User",
            Suppliers = new[]
            {
                new PurchaseOrderVM
                {
                    SupplierNumber = "20000",
                    SupplierName = "Office Depot Svenska AB",
                    Currency = "SEK",
                    PurchaseOrderType = "Expense",
                    OrderRows = new List<PurchaseOrderRowVM>(),
                    Contacts = new List<PurchaseSupplierContactVM>()
                }
            },
            Contacts = Array.Empty<IPurchaseSupplierContactVM>(),
            Articles = Array.Empty<IPurchaseArticleVM>()
        }));

        var result = await service.SearchSuppliersAsync("20000");

        var supplier = Assert.Single(result);
        Assert.Equal("Office Depot Svenska AB", supplier.Value);
        Assert.Equal("20000", supplier.SupplierNumber);
    }

    [Fact]
    public async Task SearchArticlesAsync_Filters_By_ArticleNumber()
    {
        var service = new PurchaseLookupService(new FakePurchaseContextService(new PurchaseRequestContext
        {
            ConnectionString = "Server=jeeves;",
            CompanyCode = 100,
            PersSign = "AA",
            FullName = "Test User",
            Suppliers = Array.Empty<IPurchaseOrderVM>(),
            Contacts = Array.Empty<IPurchaseSupplierContactVM>(),
            Articles = new[]
            {
                new PurchaseArticleVM
                {
                    ArticleNumber = "exp1",
                    ArticleDescription = "Consulting",
                    Unit = "pcs",
                    DefaultAccount = "4000",
                    DefaultCostCenter = "100",
                    ExpenceArticle = true
                }
            }
        }));

        var result = await service.SearchArticlesAsync("exp1");

        var article = Assert.Single(result);
        Assert.Equal("Consulting", article.Value);
        Assert.Equal("exp1", article.ArticleNumber);
    }

    private sealed class FakePurchaseContextService : IPurchaseContextService
    {
        private readonly PurchaseRequestContext _context;

        public FakePurchaseContextService(PurchaseRequestContext context)
        {
            _context = context;
        }

        public Task<PurchaseRequestContext> BuildAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_context);
    }
}
