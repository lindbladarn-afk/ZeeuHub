using System.Text.Json;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration.Akeneo;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineAkeneoSendToCentraService : IFlowEngineAkeneoSendToCentraService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IAkeneoExportService _akeneoExportService;

    public FlowEngineAkeneoSendToCentraService(IAkeneoExportService akeneoExportService)
    {
        _akeneoExportService = akeneoExportService;
    }

    public async Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Operation != FlowEngineOperationType.AkeneoSendToCentra)
            throw new InvalidOperationException($"Operationen {request.Operation} stods inte av Akeneo send-to-centra-tjansten.");

        var limit = request.Params.UseLimit && request.Params.Limit.HasValue && request.Params.Limit.Value > 0
            ? request.Params.Limit.Value
            : 100;

        var selectedSkus = request.Params.AkeneoSkus
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        AkeneoExportResult result;
        string scopeLabel;

        if (selectedSkus.Count > 0)
        {
            result = await _akeneoExportService.ExportProductsXmlAsync(selectedSkus, limit, null, cancellationToken);
            scopeLabel = $"skus ({selectedSkus.Count})";
        }
        else
        {
            result = await _akeneoExportService.ExportProductsXmlAsync(limit, null, cancellationToken);
            scopeLabel = $"all (limit {limit})";
        }

        var payload = new FlowEngineAkeneoSendToCentraPayload
        {
            DryRun = true,
            Scope = result.Scope,
            FileName = result.FileName,
            Count = result.Count,
            RequestedSkus = result.RequestedSkus,
            NotFoundSkus = result.NotFoundSkus,
            Xml = result.Xml
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Akeneo send-to-centra dry run: Count={result.Count}, FileName={result.FileName}",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                $"Selection: {scopeLabel}",
                $"NotFoundSkus: {result.NotFoundSkus.Count}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }
}
