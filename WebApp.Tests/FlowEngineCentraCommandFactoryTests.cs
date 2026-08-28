// Verifies that Centra form input produces stable, normalized Flow Engine commands.
using WebApp.Models.Integration;
using WebApp.Services.Integration.FlowEngine;

namespace WebApp.Tests;

public sealed class FlowEngineCentraCommandFactoryTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 29, 8, 30, 0, TimeSpan.Zero);
    private readonly FlowEngineCentraCommandFactory _factory = new(new FixedTimeProvider(FixedUtcNow));

    [Fact]
    public void BuildBatchCommands_PreserveLimitsFlagsAndDates()
    {
        var checkOrders = _factory.BuildCheckOrders(new FlowEngineRunCheckOrdersInput
        {
            DateUtc = "   ",
            Limit = 25
        });
        var createShipments = _factory.BuildCreateShipments(new FlowEngineRunCreateShipmentsInput
        {
            DateUtc = " 2026-07-20 ",
            Limit = 0,
            DryRun = true
        });
        var createPending = _factory.BuildCreateShipmentsPending(new FlowEngineRunCreateShipmentsPendingInput
        {
            Limit = 10,
            DryRun = false
        });
        var sendOrders = _factory.BuildSendOrders(new FlowEngineRunSendOrdersInput
        {
            DateUtc = " 2026-07-21 ",
            Limit = 50,
            DryRun = true,
            SkipJeevesCheck = true
        });
        var sendReturns = _factory.BuildSendReturns(new FlowEngineRunSendReturnsInput
        {
            DateUtc = " 2026-07-22 ",
            Limit = -1,
            DryRun = false
        });

        AssertCommand(checkOrders, "centra-check-orders", "Centra check orders", FlowEngineOperationType.CheckOrders);
        Assert.Equal("2026-07-29", checkOrders.Params.DateUtc);
        Assert.True(checkOrders.Params.UseLimit);
        Assert.Equal(25, checkOrders.Params.Limit);

        AssertCommand(createShipments, "centra-create-shipments", "Centra create shipments", FlowEngineOperationType.CreateShipments);
        Assert.Equal("2026-07-20", createShipments.Params.DateUtc);
        Assert.False(createShipments.Params.UseLimit);
        Assert.Equal(0, createShipments.Params.Limit);
        Assert.True(createShipments.Flags.DryRun);

        AssertCommand(createPending, "centra-create-shipments-pending", "Centra create shipments pending", FlowEngineOperationType.CreateShipmentsPending);
        Assert.True(createPending.Params.UseLimit);
        Assert.Equal(10, createPending.Params.Limit);
        Assert.False(createPending.Flags.DryRun);

        AssertCommand(sendOrders, "centra-send-orders", "Centra send orders", FlowEngineOperationType.SendOrders);
        Assert.Equal("2026-07-21", sendOrders.Params.DateUtc);
        Assert.True(sendOrders.Params.UseLimit);
        Assert.Equal(50, sendOrders.Params.Limit);
        Assert.True(sendOrders.Flags.DryRun);
        Assert.True(sendOrders.Flags.SkipJeevesCheck);

        AssertCommand(sendReturns, "centra-send-returns", "Centra send returns", FlowEngineOperationType.SendReturns);
        Assert.Equal("2026-07-22", sendReturns.Params.DateUtc);
        Assert.False(sendReturns.Params.UseLimit);
        Assert.Equal(-1, sendReturns.Params.Limit);
        Assert.False(sendReturns.Flags.DryRun);
    }

    [Fact]
    public void BuildRangeCommands_ApplyExistingDateAndRangeRules()
    {
        var datedOrders = _factory.BuildFetchOrders(new FlowEngineRunCentraFetchOrdersInput
        {
            DateUtc = " 2026-07-10 ",
            ForceRange = true
        });
        var rangedOrders = _factory.BuildFetchOrders(new FlowEngineRunCentraFetchOrdersInput
        {
            DateUtc = "2026-07-10",
            SinceUtc = " 2026-07-01T00:00:00Z ",
            UntilUtc = " 2026-07-02T00:00:00Z ",
            ForceRange = true
        });
        var latestReturns = _factory.BuildFetchReturns(new FlowEngineRunCentraFetchReturnsInput
        {
            DateUtc = "2026-07-10",
            UseLatestDay = true
        });

        AssertCommand(datedOrders, "centra-fetch-orders", "Centra fetch orders", FlowEngineOperationType.CentraFetchOrders);
        Assert.Equal("2026-07-10", datedOrders.Params.DateUtc);
        Assert.Null(datedOrders.Params.SinceUtc);
        Assert.Null(datedOrders.Params.UntilUtc);
        Assert.True(datedOrders.Flags.ForceRange);

        Assert.Null(rangedOrders.Params.DateUtc);
        Assert.Equal("2026-07-01T00:00:00Z", rangedOrders.Params.SinceUtc);
        Assert.Equal("2026-07-02T00:00:00Z", rangedOrders.Params.UntilUtc);
        Assert.True(rangedOrders.Flags.ForceRange);

        AssertCommand(latestReturns, "centra-fetch-returns", "Centra fetch returns", FlowEngineOperationType.CentraFetchReturns);
        Assert.Null(latestReturns.Params.DateUtc);
        Assert.True(latestReturns.Params.UseLatestDay);
    }

    [Fact]
    public void BuildSingleCommands_TrimIdentifiersAndPreserveFlags()
    {
        var fetchOrder = _factory.BuildFetchOrder(new FlowEngineRunCentraFetchOrderInput { OrderId = " 1001 " });
        var fetchReturn = _factory.BuildFetchReturn(new FlowEngineRunCentraFetchReturnInput { ReturnId = " R-2 " });
        var createShipment = _factory.BuildCreateShipment(new FlowEngineRunCreateShipmentInput
        {
            OrderId = " 1003 ",
            DryRun = true
        });
        var sendOrder = _factory.BuildSendOrder(new FlowEngineRunSendOrderInput
        {
            OrderId = " 1004 ",
            DryRun = false,
            SkipJeevesCheck = true
        });
        var sendReturn = _factory.BuildSendReturn(new FlowEngineRunSendReturnInput
        {
            ReturnId = " R-5 ",
            DryRun = true
        });

        AssertCommand(fetchOrder, "centra-fetch-order", "Centra fetch order", FlowEngineOperationType.CentraFetchOrder);
        Assert.Equal("1001", fetchOrder.Params.OrderId);

        AssertCommand(fetchReturn, "centra-fetch-return", "Centra fetch return", FlowEngineOperationType.CentraFetchReturn);
        Assert.Equal("R-2", fetchReturn.Params.ReturnId);

        AssertCommand(createShipment, "centra-create-shipment", "Centra create shipment", FlowEngineOperationType.CreateShipment);
        Assert.Equal("1003", createShipment.Params.OrderId);
        Assert.True(createShipment.Flags.DryRun);

        AssertCommand(sendOrder, "centra-send-order", "Centra send order", FlowEngineOperationType.SendOrder);
        Assert.Equal("1004", sendOrder.Params.OrderId);
        Assert.False(sendOrder.Flags.DryRun);
        Assert.True(sendOrder.Flags.SkipJeevesCheck);

        AssertCommand(sendReturn, "centra-send-return", "Centra send return", FlowEngineOperationType.SendReturn);
        Assert.Equal("R-5", sendReturn.Params.ReturnId);
        Assert.True(sendReturn.Flags.DryRun);
    }

    private static void AssertCommand(
        FlowEngineExecuteJobRequest request,
        string name,
        string label,
        FlowEngineOperationType operation)
    {
        Assert.Equal(name, request.Name);
        Assert.Equal(label, request.UiLabel);
        Assert.Equal(operation, request.Operation);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
