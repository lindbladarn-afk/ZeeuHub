using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineJeevesReadService
{
    Task<FlowEngineOperationExecutionData> GetCustomerAddressesAsync(
        JeevesRuntimeContext runtimeContext,
        string customerNumber,
        CancellationToken cancellationToken = default);

    Task<FlowEngineOperationExecutionData> GetOrdersAsync(
        JeevesRuntimeContext runtimeContext,
        int companyCode,
        string lookupField,
        string lookupValue,
        CancellationToken cancellationToken = default);

    Task<FlowEngineOperationExecutionData> OrderExistsAsync(
        JeevesRuntimeContext runtimeContext,
        string orderId,
        CancellationToken cancellationToken = default);

    Task<FlowEngineOperationExecutionData> GetProductAsync(
        JeevesRuntimeContext runtimeContext,
        string articleNumber,
        CancellationToken cancellationToken = default);

    Task<FlowEngineOperationExecutionData> GetArtStatusAsync(
        JeevesRuntimeContext runtimeContext,
        IReadOnlyList<string> articleNumbers,
        CancellationToken cancellationToken = default);
}
