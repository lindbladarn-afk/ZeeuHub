// Maps Jeeves query columns to portal models for Dapper.
namespace Repository.Mapping;

internal class WebApprovalSaleOrderRowVmMap : EntityMap<WebApprovalSaleOrderRowVM>
{
    internal WebApprovalSaleOrderRowVmMap()
    {
        Map(u => u.OrderRowNumber).ToColumn("OrdRadNr");
        Map(u => u.ArticleNumber).ToColumn("ArtNr");
        Map(u => u.ArticleDescription).ToColumn("ArtBeskr");
        Map(u => u.Quantity).ToColumn("OrdAntal");
        Map(u => u.Price).ToColumn("Vb_Pris");
        Map(u => u.Discount).ToColumn("OrdRadRab");
        Map(u => u.DeliveryDate).ToColumn("OrdBerLevDat");
    }
}
