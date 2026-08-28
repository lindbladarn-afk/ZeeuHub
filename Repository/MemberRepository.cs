namespace Repository;

public class MemberRepository
{
    public IEnumerable<SalesMemberVM> GetSalesDashboard(string connectionString, int? foretagKod)
    {
        var query = "q_zu_CustomerPortal_Member";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "ProductionScreen");
        param.Add("ForetagKod", foretagKod);

        using (var connection = new SqlConnection(connectionString))
        {
            var data = connection.Query<SalesMemberVM>(query, param, commandType: CommandType.StoredProcedure);

            if (data == null)
                throw new Exception("Failed to find any data!");

            return data;
        }
    }
}
