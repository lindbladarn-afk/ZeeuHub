using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraCheckOrdersService : IFlowEngineCentraCheckOrdersService
{
    private const int CentraOriginJeevesCompanyCode = 1;
    private static readonly JsonSerializerOptions PrettyJsonOptions = new() { WriteIndented = true };

    private readonly IEnumerable<IOrderSourceClient> _orderSourceClients;
    private readonly ILogger<FlowEngineCentraCheckOrdersService> _logger;

    public FlowEngineCentraCheckOrdersService(
        IEnumerable<IOrderSourceClient> orderSourceClients,
        ILogger<FlowEngineCentraCheckOrdersService> logger)
    {
        _orderSourceClients = orderSourceClients;
        _logger = logger;
    }

    public async Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var targetDateUtc = ResolveTargetDateUtc(request.Params.DateUtc);
        var dayStartUtc = DateTime.SpecifyKind(targetDateUtc.Date, DateTimeKind.Utc);
        var dayEndUtc = dayStartUtc.AddDays(1);
        var limit = request.Params.UseLimit ? request.Params.Limit : null;

        var centraClient = ResolveClient(IntegrationSource.Centra);
        var jeevesClient = ResolveClient(IntegrationSource.Jeeves);
        var stopwatch = Stopwatch.StartNew();

        var centraOrders = await centraClient.FetchOrdersAsync(
            new IntegrationFetchRequest
            {
                CompanyId = runtimeContext.CompanyId,
                FromUtc = dayStartUtc,
                ToUtc = dayEndUtc
            },
            cancellationToken);

        var orderedCentra = centraOrders
            .Where(order => order.OrderDate is null || order.OrderDate.Value >= dayStartUtc && order.OrderDate.Value < dayEndUtc)
            .OrderBy(order => order.OrderDate ?? DateTime.MinValue)
            .ThenBy(order => order.ExternalId, StringComparer.Ordinal)
            .ToList();

        if (limit.HasValue && limit.Value > 0)
            orderedCentra = orderedCentra.Take(limit.Value).ToList();

        var missing = new List<FlowEngineCheckOrdersMissingRow>();
        var deleted = new List<FlowEngineCheckOrdersDeletedRow>();
        var errors = new List<FlowEngineCheckOrdersErrorRow>();
        var foundCount = 0;

        foreach (var order in orderedCentra)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var jeevesMatch = await jeevesClient.FetchOrdersAsync(
                    new IntegrationFetchRequest
                    {
                        CompanyId = runtimeContext.CompanyId,
                        ExternalOrderId = order.ExternalId,
                        JeevesCompanyCode = CentraOriginJeevesCompanyCode
                    },
                    cancellationToken);

                if (jeevesMatch.Count > 0)
                {
                    foundCount++;
                    continue;
                }

                if (IsDeleted(order.Status))
                {
                    deleted.Add(new FlowEngineCheckOrdersDeletedRow
                    {
                        Id = order.ExternalId,
                        Number = order.OrderNo ?? string.Empty,
                        CreatedAt = order.OrderDate,
                        Status = order.Status ?? string.Empty
                    });
                }
                else
                {
                    missing.Add(new FlowEngineCheckOrdersMissingRow
                    {
                        Id = order.ExternalId,
                        Number = order.OrderNo ?? string.Empty,
                        CreatedAt = order.OrderDate
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FlowEngine Centra check orders failed for order {ExternalId}.", order.ExternalId);
                errors.Add(new FlowEngineCheckOrdersErrorRow
                {
                    Id = order.ExternalId,
                    Number = order.OrderNo ?? string.Empty,
                    CreatedAt = order.OrderDate,
                    ErrorMessage = ex.Message
                });
            }
        }

        stopwatch.Stop();

        var payload = new FlowEngineCheckOrdersPayload
        {
            Date = dayStartUtc.ToString("yyyy-MM-dd"),
            Missing = missing,
            Deleted = deleted,
            Errors = errors,
            Counts = new FlowEngineCheckOrdersCounts
            {
                Centra = orderedCentra.Count,
                Found = foundCount,
                Missing = missing.Count,
                Deleted = deleted.Count,
                Error = errors.Count,
                Total = orderedCentra.Count
            },
            RuntimeSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 2)
        };

        var limitSummary = limit.HasValue && limit.Value > 0 ? $" (limit {limit.Value})" : string.Empty;

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Counts for {payload.Date}{limitSummary}: Centra={payload.Counts.Centra}, Jeeves={payload.Counts.Found}, Missing={payload.Counts.Missing}, Deleted={payload.Counts.Deleted}, Error={payload.Counts.Error}, Total={payload.Counts.Total}, Runtime={payload.RuntimeSeconds:0.00}s",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                $"Jeeves check company code: {CentraOriginJeevesCompanyCode}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, PrettyJsonOptions)
        };
    }

    private IOrderSourceClient ResolveClient(IntegrationSource source)
        => _orderSourceClients.FirstOrDefault(client => client.Source == source)
            ?? throw new InvalidOperationException($"Ingen order source finns registrerad for {source}.");

    private static DateTime ResolveTargetDateUtc(string? dateUtc)
    {
        if (string.IsNullOrWhiteSpace(dateUtc))
            return DateTime.UtcNow.Date;

        if (!DateTime.TryParse(dateUtc, out var parsed))
            throw new InvalidOperationException("Datum maste anges i formatet yyyy-MM-dd for Centra check orders.");

        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
    }

    private static bool IsDeleted(string? status)
        => string.Equals(status?.Trim(), "DELETED", StringComparison.OrdinalIgnoreCase);
}
