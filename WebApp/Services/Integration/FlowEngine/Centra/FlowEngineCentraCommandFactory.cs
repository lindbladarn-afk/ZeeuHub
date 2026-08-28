// Normalizes Centra form values and builds the corresponding Flow Engine commands.
using System.Globalization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraCommandFactory : IFlowEngineCentraCommandFactory
{
    private readonly TimeProvider _timeProvider;

    public FlowEngineCentraCommandFactory(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public FlowEngineExecuteJobRequest BuildCheckOrders(FlowEngineRunCheckOrdersInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return BuildBatchRequest(
            "centra-check-orders",
            "Centra check orders",
            FlowEngineOperationType.CheckOrders,
            input.DateUtc,
            input.Limit);
    }

    public FlowEngineExecuteJobRequest BuildFetchOrder(FlowEngineRunCentraFetchOrderInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new FlowEngineExecuteJobRequest
        {
            Name = "centra-fetch-order",
            UiLabel = "Centra fetch order",
            Operation = FlowEngineOperationType.CentraFetchOrder,
            Params = new FlowEngineExecutionParams
            {
                OrderId = input.OrderId.Trim()
            }
        };
    }

    public FlowEngineExecuteJobRequest BuildFetchOrders(FlowEngineRunCentraFetchOrdersInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return BuildRangeRequest(
            "centra-fetch-orders",
            "Centra fetch orders",
            FlowEngineOperationType.CentraFetchOrders,
            input.DateUtc,
            input.SinceUtc,
            input.UntilUtc,
            input.UseLatestDay,
            input.ForceRange);
    }

    public FlowEngineExecuteJobRequest BuildFetchReturn(FlowEngineRunCentraFetchReturnInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new FlowEngineExecuteJobRequest
        {
            Name = "centra-fetch-return",
            UiLabel = "Centra fetch return",
            Operation = FlowEngineOperationType.CentraFetchReturn,
            Params = new FlowEngineExecutionParams
            {
                ReturnId = input.ReturnId.Trim()
            }
        };
    }

    public FlowEngineExecuteJobRequest BuildFetchReturns(FlowEngineRunCentraFetchReturnsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return BuildRangeRequest(
            "centra-fetch-returns",
            "Centra fetch returns",
            FlowEngineOperationType.CentraFetchReturns,
            input.DateUtc,
            input.SinceUtc,
            input.UntilUtc,
            input.UseLatestDay,
            input.ForceRange);
    }

    public FlowEngineExecuteJobRequest BuildCreateShipments(FlowEngineRunCreateShipmentsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return BuildBatchRequest(
            "centra-create-shipments",
            "Centra create shipments",
            FlowEngineOperationType.CreateShipments,
            input.DateUtc,
            input.Limit,
            input.DryRun);
    }

    public FlowEngineExecuteJobRequest BuildCreateShipmentsPending(FlowEngineRunCreateShipmentsPendingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new FlowEngineExecuteJobRequest
        {
            Name = "centra-create-shipments-pending",
            UiLabel = "Centra create shipments pending",
            Operation = FlowEngineOperationType.CreateShipmentsPending,
            Flags = new FlowEngineExecutionFlags
            {
                DryRun = input.DryRun
            },
            Params = BuildLimitParams(input.Limit)
        };
    }

    public FlowEngineExecuteJobRequest BuildCreateShipment(FlowEngineRunCreateShipmentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new FlowEngineExecuteJobRequest
        {
            Name = "centra-create-shipment",
            UiLabel = "Centra create shipment",
            Operation = FlowEngineOperationType.CreateShipment,
            Flags = new FlowEngineExecutionFlags
            {
                DryRun = input.DryRun
            },
            Params = new FlowEngineExecutionParams
            {
                OrderId = input.OrderId.Trim()
            }
        };
    }

    public FlowEngineExecuteJobRequest BuildSendOrder(FlowEngineRunSendOrderInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new FlowEngineExecuteJobRequest
        {
            Name = "centra-send-order",
            UiLabel = "Centra send order",
            Operation = FlowEngineOperationType.SendOrder,
            Flags = new FlowEngineExecutionFlags
            {
                DryRun = input.DryRun,
                SkipJeevesCheck = input.SkipJeevesCheck
            },
            Params = new FlowEngineExecutionParams
            {
                OrderId = input.OrderId.Trim()
            }
        };
    }

    public FlowEngineExecuteJobRequest BuildSendOrders(FlowEngineRunSendOrdersInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return BuildBatchRequest(
            "centra-send-orders",
            "Centra send orders",
            FlowEngineOperationType.SendOrders,
            input.DateUtc,
            input.Limit,
            input.DryRun,
            input.SkipJeevesCheck);
    }

    public FlowEngineExecuteJobRequest BuildSendReturn(FlowEngineRunSendReturnInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new FlowEngineExecuteJobRequest
        {
            Name = "centra-send-return",
            UiLabel = "Centra send return",
            Operation = FlowEngineOperationType.SendReturn,
            Flags = new FlowEngineExecutionFlags
            {
                DryRun = input.DryRun
            },
            Params = new FlowEngineExecutionParams
            {
                ReturnId = input.ReturnId.Trim()
            }
        };
    }

    public FlowEngineExecuteJobRequest BuildSendReturns(FlowEngineRunSendReturnsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return BuildBatchRequest(
            "centra-send-returns",
            "Centra send returns",
            FlowEngineOperationType.SendReturns,
            input.DateUtc,
            input.Limit,
            input.DryRun);
    }

    private FlowEngineExecuteJobRequest BuildBatchRequest(
        string name,
        string label,
        FlowEngineOperationType operation,
        string? dateUtc,
        int? limit,
        bool dryRun = false,
        bool skipJeevesCheck = false)
    {
        var parameters = BuildLimitParams(limit);
        parameters.DateUtc = NormalizeDate(dateUtc);

        return new FlowEngineExecuteJobRequest
        {
            Name = name,
            UiLabel = label,
            Operation = operation,
            Flags = new FlowEngineExecutionFlags
            {
                DryRun = dryRun,
                SkipJeevesCheck = skipJeevesCheck
            },
            Params = parameters
        };
    }

    private FlowEngineExecuteJobRequest BuildRangeRequest(
        string name,
        string label,
        FlowEngineOperationType operation,
        string? dateUtc,
        string? sinceUtc,
        string? untilUtc,
        bool useLatestDay,
        bool forceRange)
    {
        var hasRangeInput = !string.IsNullOrWhiteSpace(sinceUtc) || !string.IsNullOrWhiteSpace(untilUtc);

        return new FlowEngineExecuteJobRequest
        {
            Name = name,
            UiLabel = label,
            Operation = operation,
            Flags = new FlowEngineExecutionFlags
            {
                ForceRange = forceRange
            },
            Params = new FlowEngineExecutionParams
            {
                DateUtc = useLatestDay || hasRangeInput ? null : NormalizeDate(dateUtc),
                SinceUtc = NormalizeOptional(sinceUtc),
                UntilUtc = NormalizeOptional(untilUtc),
                UseLatestDay = useLatestDay
            }
        };
    }

    private static FlowEngineExecutionParams BuildLimitParams(int? limit)
        => new()
        {
            UseLimit = limit.HasValue && limit.Value > 0,
            Limit = limit
        };

    private string NormalizeDate(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? _timeProvider.GetUtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : value.Trim();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
