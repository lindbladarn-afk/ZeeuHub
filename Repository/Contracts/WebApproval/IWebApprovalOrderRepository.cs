namespace Repository.Contracts;

public interface IWebApprovalOrderRepository
{
    Task<IEnumerable<WebApprovalSaleOrderVM>> GetAllSalesAttestOrdersAsync(string connectionString, int? foretagKod, string emailAddress, int? status = null);

    Task<WebApprovalSaleOrderVM> GetAttestOrderWithRowsAsync(string connectionString, Guid id);

    Task UpdateAttestOrderStatusAsync(string connectionString, Guid id, string action, string? message, string approvedBy);
}
