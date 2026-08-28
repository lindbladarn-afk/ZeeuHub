using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.Integration;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraSendReturnsRetryHelper
{
    public static async Task RetryDeadlockFailuresSequentiallyAsync(
        Guid companyId,
        IntegrationSourceConfig jeevesConfig,
        IReadOnlyList<CentraSendReturnsContracts.CentraRawReturn> sourceReturns,
        List<FlowEngineSendReturnsRow> currentResults,
        IFlowEngineCentraJeevesBridgeService jeevesBridgeService,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < currentResults.Count && index < sourceReturns.Count; index++)
        {
            var existing = currentResults[index];
            if (!string.Equals(existing.Status, "failed_api", StringComparison.Ordinal) ||
                !IsDeadlockFailureMessage(existing.ErrorMessage))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            var returnData = sourceReturns[index];
            var extOrderNr = $"C{returnData.Id}";
            try
            {
                var exists = await jeevesBridgeService.OrderExistsAsync(companyId, jeevesConfig, 1, extOrderNr, cancellationToken);
                if (exists)
                {
                    currentResults[index] = FlowEngineCentraSendReturnsRowBuilder.Copy(existing, "already_exists", null);
                    continue;
                }

                var payload = FlowEngineCentraReturnJeevesMapper.Map(returnData);
                await jeevesBridgeService.CreateOrderAsync(
                    companyId,
                    jeevesConfig,
                    JsonSerializer.Serialize(payload, jsonOptions),
                    "send returns",
                    cancellationToken);
                currentResults[index] = FlowEngineCentraSendReturnsRowBuilder.Copy(existing, "sent", null);
            }
            catch (FlowEngineCentraJeevesDuplicateOrderException)
            {
                currentResults[index] = FlowEngineCentraSendReturnsRowBuilder.Copy(existing, "already_exists", null);
            }
            catch (Exception ex)
            {
                currentResults[index] = FlowEngineCentraSendReturnsRowBuilder.Copy(existing, "failed_api", ex.Message);
            }
        }
    }

    private static bool IsDeadlockFailureMessage(string? message)
        => !string.IsNullOrWhiteSpace(message) && message.Contains("deadlock", StringComparison.OrdinalIgnoreCase);
}
