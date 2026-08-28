using Repository.Execution;

namespace Repository;

public class WebApprovalPriceListRepository : IWebApprovalPriceListRepository
{
    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

    public WebApprovalPriceListRepository(IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public async Task<IEnumerable<WebApprovalPriceListDto>> GetPriceListWithRowsAsync(string connectionString, int? foretagKod, string? persSign, int? priceListId = null)
    {
        var prisLists = new List<WebApprovalPriceListDto>();
        var query = "q_zu_CustomerPortal_WebApprovalPriceList";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetWebApprovalPriceListsAndRows");
        param.Add("ForetagKod", foretagKod);
        param.Add("PriceListId", priceListId);
        param.Add("PersSign2", persSign);

        return await _jeevesSqlExecutor.WithConnectionAsync(
            connectionString,
            async connection =>
            {
                using var multi = await connection.QueryMultipleAsync(new CommandDefinition(query, param, commandType: CommandType.StoredProcedure, commandTimeout: 15));
                var priceLists = (await multi.ReadAsync<PriceList>()).ToList();
                var priceListRowsDto = (await multi.ReadAsync<WebApprovalPriceListRowDto>()).ToList();

                if (priceListRowsDto == null)
                    throw new Exception("Failed to find any data!");

                var uniquePriceListIds = priceListRowsDto.Select(x => x.PriceListId).Distinct().ToList();

                foreach (var tempPriceListId in uniquePriceListIds)
                {
                    var priceList = priceLists.FirstOrDefault(x => x.PriceListId == tempPriceListId && x.CompanyCode == foretagKod);
                    var priceListDto = MapPriceList(priceList);
                    if (priceListDto == null)
                        continue;

                    priceListDto.Rows = priceListRowsDto
                        .Where(x => x.PriceListId == tempPriceListId)
                        .OrderBy(x => x.ArticleNumber)
                        .ThenByDescending(x => x.LowLimit)
                        .ToList();

                    prisLists.Add(priceListDto);
                }

                return prisLists.OrderBy(x => x.PriceListId).ToList().AsEnumerable();
            },
            operationName: "WebApprovalPriceListRepository.GetPriceListWithRows");
    }

    // Price list rows come from a separate result set, so the header mapping stays explicit here.
    private static WebApprovalPriceListDto? MapPriceList(PriceList? source)
    {
        if (source is null)
            return null;

        return new WebApprovalPriceListDto
        {
            PriceListId = source.PriceListId,
            PriceListDescription = source.PriceListDescription,
            CompanyCode = source.CompanyCode,
            ValidFrom = source.ValidFrom,
            ValidTo = source.ValidTo,
            Currency = source.Currency
        };
    }

    public async Task UpdatePriceListStatusAsync(string connectionString, Guid id, string attestStatus, string? message, string approvedBy)
    {
        var query = "q_zu_CustomerPortal_WebApprovalPriceList";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "UpdatePriceListRow");
        param.Add("Id", id);
        param.Add("Status", attestStatus);
        param.Add("Message", message);
        param.Add("ApprovedBy", approvedBy);

        await _jeevesSqlExecutor.ExecuteAsync(
            connectionString,
            query,
            param,
            CommandType.StoredProcedure,
            operationName: "WebApprovalPriceListRepository.UpdatePriceListStatus");
    }
}
