namespace Repository.Contracts;

public interface IWebApprovalPurchaseRepository
{
    Task<IEnumerable<WebApprovalPurchaseOrderVM>> GetAllPurchaseAttestOrdersAsync(string connectionString, int? foretagKod, string emailAddress, int? status = null);

    /// <summary>
    /// Returns an purchase order with rows
    /// </summary>
    /// <param name="connectionString"></param>
    /// <param name="foretagKod"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<WebApprovalPurchaseOrderVM> GetAttestPurchaseOrderWithRowsAsync(string connectionString, Guid id);

    /// <summary>
    /// Update the purchase order with the attest status
    /// </summary>
    /// <param name="connectionString"></param>
    /// <param name="orderId"></param>
    /// <param name="attestStatus"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    Task UpdateOrderStatusAsync(string connectionString, Guid orderId, string attestStatus, string approvedBy, string? message = null);
}
