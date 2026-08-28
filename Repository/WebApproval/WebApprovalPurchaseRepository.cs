using LoggerService;
using Repository.Execution;

namespace Repository;

public class WebApprovalPurchaseRepository : IWebApprovalPurchaseRepository
{
    private readonly ILoggerManager _loggerManager;
    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

    public WebApprovalPurchaseRepository(ILoggerManager loggerManager, IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _loggerManager = loggerManager;
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public async Task<IEnumerable<WebApprovalPurchaseOrderVM>> GetAllPurchaseAttestOrdersAsync(string connectionString, int? foretagKod, string emailAddress, int? status = null)
    {
        var query = "q_zu_CustomerPortal_WebApprovalPurchase";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetWebApprovalPurchaseOrders");
        param.Add("ForetagKod", foretagKod);
        param.Add("EmailAddress", emailAddress);
        param.Add("Status", status);

        var data = await _jeevesSqlExecutor.QueryAsync<WebApprovalPurchaseOrderVM>(
            connectionString,
            query,
            param,
            CommandType.StoredProcedure,
            operationName: "WebApprovalPurchaseRepository.GetAllPurchaseAttestOrders");

        if (data == null)
            throw new Exception("Failed to find any data!");

        return data;
    }

    public async Task<WebApprovalPurchaseOrderVM> GetAttestPurchaseOrderWithRowsAsync(string connectionString, Guid id)
    {
        var query = "q_zu_CustomerPortal_WebApprovalPurchase";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetWebApprovalPurchaseOrder");
        param.Add("Id", id);

        return await _jeevesSqlExecutor.WithConnectionAsync(
            connectionString,
            async connection =>
            {
                using var multi = await connection.QueryMultipleAsync(new CommandDefinition(query, param, commandType: CommandType.StoredProcedure, commandTimeout: 15));
                var data = (await multi.ReadAsync<WebApprovalPurchaseOrderVM>()).FirstOrDefault();

                if (data is null)
                    throw new Exception("Failed to find any data!");

                if (multi.IsConsumed)
                    return data;

                data.OrderRows = (await multi.ReadAsync<WebApprovalPurchaseOrderRowVM>()).ToList();
                return data;
            },
            operationName: "WebApprovalPurchaseRepository.GetAttestPurchaseOrderWithRows");
    }

    public async Task UpdateOrderStatusAsync(string connectionString, Guid orderId, string attestStatus, string approvedBy, string? message = null)
    {
        var query = "q_zu_CustomerPortal_WebApprovalPurchase";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "UpdateWebApprovalPurchaseOrder");
        param.Add("Id", orderId);
        param.Add("AttestStatus", attestStatus);
        param.Add("ApprovedBy", approvedBy);
        param.Add("Message", message);

        await _jeevesSqlExecutor.ExecuteAsync(
            connectionString,
            query,
            param,
            CommandType.StoredProcedure,
            operationName: "WebApprovalPurchaseRepository.UpdateOrderStatus");
    }
}
