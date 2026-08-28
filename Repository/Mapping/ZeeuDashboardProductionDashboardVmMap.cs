// Maps Jeeves query columns to portal models for Dapper.
namespace Repository.Mapping;

public class ZeeuDashboardProductionDashboardVmMap : EntityMap<ProductionDashboardVM>
{
    public ZeeuDashboardProductionDashboardVmMap()
    {
        Map(u => u.PersSign).ToColumn("PersSign2");
        Map(u => u.Name).ToColumn("RespNamn");
        Map(u => u.Present).ToColumn("Present");
        Map(u => u.ArrivedDate).ToColumn("KomDatum");
        Map(u => u.ArrivedTime).ToColumn("KomTid");
        Map(u => u.WorkOrder).ToColumn("AoNr");
        Map(u => u.OperationNumber).ToColumn("OpNr");
        Map(u => u.OperationStarted).ToColumn("Startat");
        Map(u => u.ProductionGroup).ToColumn("ProdGrp");
        Map(u => u.CalculatedOperationTime).ToColumn("StycktidTimmar");
        Map(u => u.ReportedOperationTime).ToColumn("StycktidRapporteratTimmar");
        Map(u => u.NextWorkOrder).ToColumn("NextWorkOrder");
        Map(u => u.NextProductionGroup).ToColumn("NextProductionGroup");
    }
}
