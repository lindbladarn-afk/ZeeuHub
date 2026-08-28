namespace Repository.Contracts;

public interface IZeeuDashboardRepository
{
    IEnumerable<ProductionDashboardVM> GetProductionPersonal(string connectionString, int? foretagKod);

    /// <summary>
    /// Update the prv extension table with the next work order and production group
    /// </summary>
    /// <param name="connectionString"></param>
    /// <param name="persSign"></param>
    /// <param name="foretagKod"></param>
    /// <param name="nextWorkOrder"></param>
    /// <param name="nextProductionGroup"></param>
    void UpdateNextWorkOrder(string connectionString, string? persSign, int? foretagKod, string? nextWorkOrder, string? nextProductionGroup);
}