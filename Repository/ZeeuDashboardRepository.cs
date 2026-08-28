namespace Repository;

public class ZeeuDashboardRepository : IZeeuDashboardRepository
{
    public IEnumerable<ProductionDashboardVM> GetProductionPersonal(string connectionString, int? foretagKod)
    {
        var query = "q_zu_CustomerPortal_ZeeuDashboard";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "ProductionScreen");
        param.Add("ForetagKod", foretagKod);

        using (var connection = new SqlConnection(connectionString))
        {
            var data = connection.Query<ProductionDashboardVM>(query, param, commandType: CommandType.StoredProcedure);

            if (data == null)
                throw new Exception("Failed to find any data!");

            return data;
        }
    }

    public void UpdateNextWorkOrder(string connectionString, string? persSign, int? foretagKod, string? nextWorkOrder, string? nextProductionGroup)
		{
        var query = "q_zu_CustomerPortal_ZeeuDashboard";

        using (var connection = new SqlConnection(connectionString))
        {
            var param = new DynamicParameters();
            param.Add("SelectStatement", "ProductionScreen_UpdateNextWorkOrder");
            param.Add("PersSign2", persSign);
            param.Add("ForetagKod", foretagKod);
            param.Add("NextWorkOrder", nextWorkOrder);
            param.Add("NextProductionGroup", nextProductionGroup);

            connection.Execute(query, param, commandType: CommandType.StoredProcedure);

        }
    }
}
