namespace Repository;

using Repository.Execution;

public class WebApprovalOrderRepository : IWebApprovalOrderRepository
{
    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

    public WebApprovalOrderRepository(IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public async Task<IEnumerable<WebApprovalSaleOrderVM>> GetAllSalesAttestOrdersAsync(string connectionString, int? foretagKod, string emailAddress, int? status = null)
    {
        var query = "q_zu_CustomerPortal_WebApprovalSales";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetWebApprovalOrders");
        param.Add("ForetagKod", foretagKod);
        param.Add("EmailAddress", emailAddress);
        param.Add("Status", status);

        var data = await _jeevesSqlExecutor.QueryAsync<WebApprovalSaleOrderVM>(
            connectionString,
            query,
            param,
            CommandType.StoredProcedure,
            operationName: "WebApprovalOrderRepository.GetAllSalesAttestOrders");

        if (data == null)
            throw new Exception("Failed to find any data!");

        return data;
    }

    public Task<WebApprovalSaleOrderVM> GetAttestOrderWithRowsAsync(string connectionString, Guid id)
    {
        var query = "q_zu_CustomerPortal_WebApprovalSales";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetWebApprovalOrderWithRows");
        param.Add("Id", id);

        return _jeevesSqlExecutor.WithConnectionAsync(
            connectionString,
            async connection =>
            {
                using var multi = await connection.QueryMultipleAsync(new CommandDefinition(query, param, commandType: CommandType.StoredProcedure, commandTimeout: 15));
                var data = multi.Read<WebApprovalSaleOrderVM>().FirstOrDefault();

                if (data == null)
                    throw new Exception("Failed to find any data!");

                if (multi.IsConsumed)
                    return data;

                data.OrderRows = multi.Read<WebApprovalSaleOrderRowVM>().ToList();
                return data;
            },
            operationName: "WebApprovalOrderRepository.GetAttestOrderWithRows");
    }

    public async Task UpdateAttestOrderStatusAsync(string connectionString, Guid id, string attestStatus, string? message, string approvedBy)
    {
        var query = "q_zu_CustomerPortal_WebApprovalSales";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "UpdateWebApprovalOrder");
        param.Add("Id", id);
        param.Add("AttestStatus", attestStatus);
        param.Add("ApprovedBy", approvedBy);
        param.Add("Message", message);

        await _jeevesSqlExecutor.ExecuteAsync(
            connectionString,
            query,
            param,
            CommandType.StoredProcedure,
            operationName: "WebApprovalOrderRepository.UpdateAttestOrderStatus");
    }
}
