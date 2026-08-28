using System.Text.Json;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration.Akeneo;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineAkeneoExportService : IFlowEngineAkeneoExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IAkeneoExportService _akeneoExportService;

    public FlowEngineAkeneoExportService(IAkeneoExportService akeneoExportService)
    {
        _akeneoExportService = akeneoExportService;
    }

    public async Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var limit = request.Params.UseLimit && request.Params.Limit.HasValue && request.Params.Limit.Value > 0
            ? request.Params.Limit.Value
            : 100;

        AkeneoExportResult result;
        string scopeLabel;

        switch (request.Operation)
        {
            case FlowEngineOperationType.AkeneoProducts:
            {
                var skus = request.Params.AkeneoSkus
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (skus.Count == 0)
                    throw new InvalidOperationException("Minst ett SKU maste anges for Akeneo export.");

                result = await _akeneoExportService.ExportProductsXmlAsync(skus, limit, null, cancellationToken);
                scopeLabel = $"skus ({skus.Count})";
                break;
            }
            case FlowEngineOperationType.AkeneoAllProducts:
                result = await _akeneoExportService.ExportProductsXmlAsync(limit, null, cancellationToken);
                scopeLabel = "all products";
                break;
            default:
                throw new InvalidOperationException($"Operationen {request.Operation} stods inte av Akeneo export-tjansten.");
        }

        var payload = new FlowEngineAkeneoExportPayload
        {
            Scope = result.Scope,
            Count = result.Count,
            FileName = result.FileName,
            RequestedSkus = result.RequestedSkus,
            NotFoundSkus = result.NotFoundSkus,
            Xml = result.Xml
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Akeneo export ({scopeLabel}): Count={result.Count}, FileName={result.FileName}",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                $"NotFoundSkus: {result.NotFoundSkus.Count}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }
}
