namespace Repository;

using Repository.Execution;

public class PurchaseRepository : IPurchaseRepository
{
    private const string PurchaseProcedure = "dbo.q_zu_CustomerPortal_Purchase";
    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

    public PurchaseRepository(IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public async Task<IEnumerable<IPurchaseOrderVM>> GetAllSuppliersAsync(string connectionString, int? companyCode)
    {
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetAllSuppliers");
        param.Add("ForetagKod", companyCode);

        return await _jeevesSqlExecutor.QueryAsync<PurchaseOrderVM>(
            connectionString,
            PurchaseProcedure,
            param,
            CommandType.StoredProcedure,
            operationName: "PurchaseRepository.GetAllSuppliers");
    }

    public async Task<IEnumerable<IPurchaseSuppliersAutoCompleteDto>> GetAutocompleteSuppliersAsync(string connectionString, int companyCode)
    {
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetAutoCompleteSuppliers");
        param.Add("ForetagKod", companyCode);

        return await _jeevesSqlExecutor.QueryAsync<PurchaseSuppliersAutoCompleteDto>(
            connectionString,
            PurchaseProcedure,
            param,
            CommandType.StoredProcedure,
            operationName: "PurchaseRepository.GetAutocompleteSuppliers");
    }

    public async Task<IEnumerable<IPurchaseSupplierContactVM>> GetAllContactsAsync(string connectionString, int? companyCode)
    {
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetAllContacts");
        param.Add("ForetagKod", companyCode);

        return await _jeevesSqlExecutor.QueryAsync<PurchaseSupplierContactVM>(
            connectionString,
            PurchaseProcedure,
            param,
            CommandType.StoredProcedure,
            operationName: "PurchaseRepository.GetAllContacts");
    }

    public async Task<IEnumerable<IPurchaseArticleVM>> GetPurchaseArticlesAsync(string connectionString, int? companyCode)
    {
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetExpenceArticles");
        param.Add("ForetagKod", companyCode);

        return await _jeevesSqlExecutor.QueryAsync<PurchaseArticleVM>(
            connectionString,
            PurchaseProcedure,
            param,
            CommandType.StoredProcedure,
            operationName: "PurchaseRepository.GetPurchaseArticles");
    }

    public async Task<IEnumerable<IPurchaseOrderVM>> GetMyPurchaseOrdersAsync(string connectionString, int? companyCode, string perssign)
    {
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetPurchaseOrders");
        param.Add("ForetagKod", companyCode);
        param.Add("c_perssign", perssign);

        return await _jeevesSqlExecutor.QueryAsync<PurchaseOrderVM>(
            connectionString,
            PurchaseProcedure,
            param,
            CommandType.StoredProcedure,
            operationName: "PurchaseRepository.GetMyPurchaseOrders");
    }

    public async Task<IPurchaseOrderVM> GetPurchaseOrderAsync(string connectionString, int? companyCode, string perssign, int orderNumber)
    {
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetPurchaseOrder");
        param.Add("ForetagKod", companyCode);
        param.Add("OrderNumber", orderNumber);
        param.Add("c_perssign", perssign);

        return await _jeevesSqlExecutor.WithConnectionAsync(
            connectionString,
            async connection =>
            {
                using var multi = await connection.QueryMultipleAsync(new CommandDefinition(PurchaseProcedure, param, commandType: CommandType.StoredProcedure, commandTimeout: 15));
                var data = multi.Read<PurchaseOrderVM>().FirstOrDefault();
                if (data == null)
                    throw new Exception("Failed to find any data");
                if (multi.IsConsumed)
                {
                    data.OrderRows = new List<PurchaseOrderRowVM>();
                    return data;
                }

                data.OrderRows = multi.Read<PurchaseOrderRowVM>().ToList() ?? new List<PurchaseOrderRowVM>();
                return data;
            },
            operationName: "PurchaseRepository.GetPurchaseOrder");
    }

    public IPurchaseOrderResultDto CreatePurchaseOrder(string connectionString, string perssign, string? userFullName, int? companyCode, IPurchaseOrderVM purchaseOrder)
    {
        var purchaseOrderResult = new PurchaseOrderResultDto();

        try
        {
            using var transactionScope = new TransactionScope();

            var headerParam = new DynamicParameters();
            headerParam.Add("SelectStatement", "CreatePurchaseOrder");
            headerParam.Add("ForetagKod", companyCode);
            headerParam.Add("c_ftgnr", purchaseOrder.SupplierNumber);
            headerParam.Add("c_bestberlevdat", purchaseOrder.DeliveryDate);
            headerParam.Add("c_besttyp", purchaseOrder.PurchaseOrderTypeId);
            headerParam.Add("c_vref", userFullName);
            headerParam.Add("c_valkod", purchaseOrder.Currency);
            headerParam.Add("c_edit", purchaseOrder.Message);
            headerParam.Add("c_perssign", perssign);
            headerParam.Add("c_ordlevadr1", purchaseOrder.DeliveryCompany);
            headerParam.Add("c_ordlevadr2", purchaseOrder.DeliveryCo);
            headerParam.Add("c_ordlevadr3", purchaseOrder.DeliveryStreet);
            headerParam.Add("c_ftgpostnr", purchaseOrder.DeliveryZip);
            headerParam.Add("c_ordlevadrbstort", purchaseOrder.DeliveryCity);
            headerParam.Add("c_ordlevadrlandskod", purchaseOrder.DeliveryCountry);

            var data = _jeevesSqlExecutor.QueryFirstOrDefault<PurchaseOrderSqlResponseDto>(
                connectionString,
                PurchaseProcedure,
                headerParam,
                CommandType.StoredProcedure,
                operationName: "PurchaseRepository.CreatePurchaseOrder");

            if (data != null && data.Success)
                purchaseOrderResult.OrderNumber = (int)data.BestNr;

            if (purchaseOrderResult.OrderNumber is not null)
            {
                foreach (var orderRow in purchaseOrder.OrderRows)
                {
                    var rowParam = new DynamicParameters();
                    rowParam.Add("SelectStatement", "CreatePurchaseOrderRow");
                    rowParam.Add("ForetagKod", companyCode);
                    rowParam.Add("c_bestnr", purchaseOrderResult.OrderNumber);
                    rowParam.Add("c_artnr", orderRow.ArticleNumber);
                    rowParam.Add("c_artbeskr", orderRow.ArticleDescription);
                    rowParam.Add("c_bestant", orderRow.Quantity);
                    rowParam.Add("c_vb_inpris", orderRow.Price);
                    rowParam.Add("c_rabatt1", orderRow.Discount);
                    rowParam.Add("c_ktonr", orderRow.Account);
                    rowParam.Add("c_koststallekod", orderRow.CostCenter);
                    rowParam.Add("c_perssign", perssign);

                    _jeevesSqlExecutor.QueryFirstOrDefault<PurchaseOrderRowSqlResponseDto>(
                        connectionString,
                        PurchaseProcedure,
                        rowParam,
                        CommandType.StoredProcedure,
                        operationName: "PurchaseRepository.CreatePurchaseOrderRow");
                }
            }

            transactionScope.Complete();
            purchaseOrderResult.Success = true;
            purchaseOrderResult.Message = $"Purchase order {purchaseOrderResult.OrderNumber} was created successfully";
            return purchaseOrderResult;
        }
        catch (SqlException ex)
        {
            purchaseOrderResult.Success = false;
            purchaseOrderResult.Message = ex.Message;
            return purchaseOrderResult;
        }
    }

    public IPurchaseOrderResultDto CreateStockDelivery(string connectionString, string perssign, int? companyCode, IPurchaseOrderVM purchaseOrder)
    {
        var purchaseOrderResult = new PurchaseOrderResultDto();
        try
        {
            using var transaction = new TransactionScope();

            foreach (var orderRow in purchaseOrder.OrderRows)
            {
                if (orderRow.AddToStock != true)
                    continue;

                var qtyToAdd = orderRow.Quantity - orderRow.RecievedQuantity;
                if (qtyToAdd <= 0)
                    continue;

                var param = new DynamicParameters();
                param.Add("SelectStatement", "CreateStockDeliveryRow");
                param.Add("ForetagKod", companyCode);
                param.Add("c_perssign", perssign);
                param.Add("c_bestnr", purchaseOrder.OrderNumber);
                param.Add("c_artnr", orderRow.ArticleNumber);
                param.Add("c_bestlevant", qtyToAdd);
                param.Add("c_bestlevdat", DateTime.Now);
                param.Add("c_bestradnr", orderRow.RowNumber);

                _jeevesSqlExecutor.QueryFirstOrDefault<PurchaseOrderRowSqlResponseDto>(
                    connectionString,
                    PurchaseProcedure,
                    param,
                    CommandType.StoredProcedure,
                    operationName: "PurchaseRepository.CreateStockDeliveryRow");
            }

            transaction.Complete();
            purchaseOrderResult.Success = true;
            purchaseOrderResult.Message = $"{purchaseOrder.OrderRows.Where(x => x.IsRecieved == true).Count()} rows updated successfully on order {purchaseOrderResult.OrderNumber}";
            return purchaseOrderResult;
        }
        catch (Exception ex)
        {
            purchaseOrderResult.Success = false;
            purchaseOrderResult.Message = ex.Message;
            return purchaseOrderResult;
        }
    }
}
