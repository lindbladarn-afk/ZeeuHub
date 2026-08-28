using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraShipmentJeevesStatusService
{
    private readonly IFlowEngineCentraJeevesBridgeService _centraJeevesBridgeService;

    public FlowEngineCentraShipmentJeevesStatusService(IFlowEngineCentraJeevesBridgeService centraJeevesBridgeService)
    {
        _centraJeevesBridgeService = centraJeevesBridgeService;
    }

    internal async Task<JeevesOrderCheckResult> CheckOrderAsync(
        Guid companyId,
        IntegrationSourceConfig jeevesConfig,
        string orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            return MapJeevesCheck(
                await _centraJeevesBridgeService.CheckOrderAsync(companyId, jeevesConfig, orderId, cancellationToken));
        }
        catch (Exception ex)
        {
            return new JeevesOrderCheckResult
            {
                Status = JeevesCheckStatus.Error,
                JeevesOrderStatus = 0,
                StatusName = $"Jeeves check failed: {ex.Message}"
            };
        }
    }

    internal async Task<Dictionary<string, JeevesOrderCheckResult>> CheckOrdersAsync(
        Guid companyId,
        IntegrationSourceConfig jeevesConfig,
        IReadOnlyList<ShipmentOrderContext> orders,
        CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, JeevesOrderCheckResult>(StringComparer.Ordinal);

        foreach (var order in orders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            checks[order.OrderId] = await CheckOrderAsync(companyId, jeevesConfig, order.OrderId, cancellationToken);
        }

        return checks;
    }

    private static JeevesOrderCheckResult MapJeevesCheck(FlowEngineJeevesOrderCheckResult check)
        => new()
        {
            Status = check.Status switch
            {
                FlowEngineJeevesLookupStatus.NotFound => JeevesCheckStatus.NotFound,
                FlowEngineJeevesLookupStatus.Error => JeevesCheckStatus.Error,
                _ => JeevesCheckStatus.Found
            },
            JeevesOrderStatus = check.JeevesOrderStatus,
            OrderNumber = check.JeevesOrderNumber,
            StatusName = check.ErrorMessage ?? check.StatusName,
            TrackingUrl = check.TrackingUrl
        };
}
