using Entities.Purchase;
using WebApp.ViewModels.Purchase;

using WebApp.Services.Purchase.Context;

namespace WebApp.Services.Purchase.Lookup;

// Keeps supplier and article autocomplete mapping outside the Purchase controller.
public sealed class PurchaseLookupService : IPurchaseLookupService
{
    private readonly IPurchaseContextService _contextService;

    public PurchaseLookupService(IPurchaseContextService contextService)
    {
        _contextService = contextService;
    }

    public async Task<IReadOnlyList<PurchaseSupplierLookupItem>> SearchSuppliersAsync(
        string? searchString,
        CancellationToken cancellationToken = default)
    {
        var context = await _contextService.BuildAsync(cancellationToken);
        var normalizedSearch = NormalizeSearch(searchString);

        return context.Suppliers
            .Where(supplier => MatchesSupplier(supplier, normalizedSearch))
            .Select(supplier => new PurchaseSupplierLookupItem
            {
                Label = BuildSupplierLabel(supplier),
                Value = supplier.SupplierName,
                SupplierNumber = supplier.SupplierNumber,
                SupplierName = supplier.SupplierName,
                SupplierCo = supplier.Co,
                SupplierStreet = supplier.Street,
                SupplierZipCode = supplier.ZipCode,
                SupplierCity = supplier.City,
                SupplierCountry = supplier.Country,
                Currency = supplier.Currency,
                DeliveryCompany = supplier.DeliveryCompany,
                DeliveryCo = supplier.DeliveryCo,
                DeliveryStreet = supplier.DeliveryStreet,
                DeliveryZipCode = supplier.DeliveryZip,
                DeliveryCity = supplier.DeliveryCity,
                DeliveryCountry = supplier.DeliveryCountry,
                Contacts = context.Contacts
                    .Where(contact => contact.SupplierNumber == supplier.SupplierNumber)
                    .ToArray()
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PurchaseArticleLookupItem>> SearchArticlesAsync(
        string? searchString,
        CancellationToken cancellationToken = default)
    {
        var context = await _contextService.BuildAsync(cancellationToken);
        var normalizedSearch = NormalizeSearch(searchString);

        return context.Articles
            .Where(article => MatchesArticle(article, normalizedSearch))
            .Select(article => new PurchaseArticleLookupItem
            {
                Label = BuildArticleLabel(article),
                Value = article.ArticleDescription,
                ArticleNumber = article.ArticleNumber,
                ArticleDescription = article.ArticleDescription,
                Unit = article.Unit,
                ProductGroupCode = article.ProductGroupCode,
                DefaultAccount = article.DefaultAccount,
                DefaultCostCenter = article.DefaultCostCenter,
                ExpenceArticle = article.ExpenceArticle
            })
            .ToList();
    }

    private static string NormalizeSearch(string? searchString)
        => searchString?.Trim() ?? string.Empty;

    private static bool MatchesSupplier(IPurchaseOrderVM supplier, string searchString)
        => ContainsSearchText(supplier.SupplierName, searchString)
            || ContainsSearchText(supplier.SupplierNumber, searchString);

    private static bool MatchesArticle(IPurchaseArticleVM article, string searchString)
        => ContainsSearchText(article.ArticleDescription, searchString)
            || ContainsSearchText(article.ArticleNumber, searchString);

    private static bool ContainsSearchText(string? value, string searchString)
        => string.IsNullOrEmpty(searchString)
            || (value?.Contains(searchString, StringComparison.OrdinalIgnoreCase) ?? false);

    private static string BuildSupplierLabel(IPurchaseOrderVM supplier)
        => string.IsNullOrWhiteSpace(supplier.SupplierNumber)
            ? supplier.SupplierName
            : $"{supplier.SupplierNumber} - {supplier.SupplierName}";

    private static string BuildArticleLabel(IPurchaseArticleVM article)
        => string.IsNullOrWhiteSpace(article.ArticleNumber)
            ? article.ArticleDescription
            : $"{article.ArticleNumber} - {article.ArticleDescription}";
}
