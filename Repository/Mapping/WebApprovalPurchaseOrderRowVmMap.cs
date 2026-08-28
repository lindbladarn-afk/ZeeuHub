// Maps Jeeves query columns to portal models for Dapper.
using Entities.ViewModels.WebApproval;

namespace Repository.Mapping;

internal class WebApprovalPurchaseOrderRowVmMap : EntityMap<WebApprovalPurchaseOrderRowVM>
{
    internal WebApprovalPurchaseOrderRowVmMap()
    {
        Map(u => u.OrderRowNumber).ToColumn("BestRadNr");
        Map(u => u.ArticleNumber).ToColumn("ArtNr");
        Map(u => u.ArticleDescription).ToColumn("ArtBeskr");
        Map(u => u.Price).ToColumn("Vb_Inpris");
        Map(u => u.Quantity).ToColumn("BestAntExtQty");
        Map(u => u.DeliveryDate).ToColumn("BestBerLevDat");
    }

}
