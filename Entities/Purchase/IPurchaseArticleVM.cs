namespace Entities.Purchase;

public interface IPurchaseArticleVM
{
    string ArticleNumber { get; set; }
    string ArticleDescription { get; set; }
    string Unit { get; set; }
    int ProductGroupCode { get; set; }
    bool ExpenceArticle { get; set; }
    string DefaultAccount { get; set; }
    string DefaultCostCenter { get; set; }
}