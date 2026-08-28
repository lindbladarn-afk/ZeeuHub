namespace Repository.Contracts;

public interface IWebApprovalPriceListRepository
{
    Task<IEnumerable<WebApprovalPriceListDto>> GetPriceListWithRowsAsync(string connectionString, int? foretagKod, string? persSign, int? priceListId = null);
    Task UpdatePriceListStatusAsync(string connectionString, Guid id, string attestStatus, string? message, string approvedBy);
}