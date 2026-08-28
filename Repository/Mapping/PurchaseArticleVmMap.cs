// Maps Jeeves query columns to portal models for Dapper.
namespace Repository.Mapping;

internal class PurchaseArticleVmMap : EntityMap<PurchaseArticleVM>
{
    public PurchaseArticleVmMap()
    {
        Map(u => u.ArticleNumber).ToColumn("ArtNr");
        Map(u => u.ArticleDescription).ToColumn("ArtBeskr");
        Map(u => u.Unit).ToColumn("EnhetsKod");
        Map(u => u.ProductGroupCode).ToColumn("VaruGruppKod");
        Map(u => u.DefaultAccount).ToColumn("q_zu_default_acccount");
        Map(u => u.DefaultCostCenter).ToColumn("q_zu_default_costcenter");
        Map(u => u.ExpenceArticle).ToColumn("q_zu_expence_item");
    }
}
