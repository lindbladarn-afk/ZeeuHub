using System.Text.Json;

namespace WebApp.Models.Integration;

public sealed class FlowEngineHistoryPanelViewModel
{
    public string CardId { get; set; } = string.Empty;
    public string Title { get; set; } = "Execution History";
    public string Subtitle { get; set; } = string.Empty;
    public string EmptyMessage { get; set; } = string.Empty;
    public string SelectedJobAction { get; set; } = string.Empty;
    public string Fragment { get; set; } = string.Empty;
    public bool ShowSystemColumn { get; set; }
    public bool EnableFilters { get; set; }
    public bool ShowDetail { get; set; }
    public IReadOnlyList<FlowEngineJobSnapshot> Jobs { get; set; } = Array.Empty<FlowEngineJobSnapshot>();
    public FlowEngineJobSnapshot? SelectedJob { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 15;
    public FlowEngineHistoryFilterState Filters { get; set; } = new();
    public IReadOnlyList<string> AvailableSystems { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableOperations { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableStatuses { get; set; } = Array.Empty<string>();
}

public sealed class FlowEngineHistoryPageResult
{
    public IReadOnlyList<FlowEngineJobSnapshot> Jobs { get; set; } = Array.Empty<FlowEngineJobSnapshot>();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 15;
    public IReadOnlyList<string> AvailableSystems { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableOperations { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableStatuses { get; set; } = Array.Empty<string>();
    public FlowEngineHistoryFilterState Filters { get; set; } = new();
}

public sealed class FlowEngineHistoryFilterState
{
    public string? System { get; set; }
    public string? Operation { get; set; }
    public string? Status { get; set; }
    public string? DateStart { get; set; }
    public string? DateEnd { get; set; }
}

public sealed class FlowEngineStatusChipViewModel
{
    public string Label { get; set; } = string.Empty;
    public string CssClass { get; set; } = string.Empty;
}

public static class FlowEngineJobPresentation
{
    private static readonly JsonSerializerOptions HistoryJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static string GetStatusLabel(FlowEngineJobStatus status)
    {
        return status switch
        {
            FlowEngineJobStatus.Succeeded => "Completed",
            FlowEngineJobStatus.Failed => "Failed",
            FlowEngineJobStatus.Running => "Running",
            FlowEngineJobStatus.Cancelled => "Cancelled",
            _ => "Queued"
        };
    }

    public static string GetStatusClass(FlowEngineJobStatus status)
    {
        return status switch
        {
            FlowEngineJobStatus.Succeeded => "flowengine-job-status-pill--success",
            FlowEngineJobStatus.Failed => "flowengine-job-status-pill--danger",
            FlowEngineJobStatus.Running => "flowengine-job-status-pill--info",
            FlowEngineJobStatus.Cancelled => "flowengine-job-status-pill--muted",
            _ => "flowengine-job-status-pill--planned"
        };
    }

    public static string GetSystemLabel(FlowEngineJobSnapshot job)
    {
        return job.Arguments.FirstOrDefault() switch
        {
            "centra" => "Centra",
            "shopify" => "Shopify",
            "jeeves" => "Jeeves",
            "akeneo" => "Akeneo",
            _ => "FlowEngine"
        };
    }

    public static bool HasFallbackStorage(IReadOnlyList<FlowEngineJobSnapshot> jobs)
        => jobs.Any(job => job.StorageKind == FlowEngineJobStorageKind.InMemoryFallback);

    public static string? GetFallbackWarning(IReadOnlyList<FlowEngineJobSnapshot> jobs)
        => jobs.FirstOrDefault(job => !string.IsNullOrWhiteSpace(job.StorageWarning))?.StorageWarning;

    public static string GetStorageLabel(FlowEngineJobSnapshot job)
    {
        return job.StorageKind switch
        {
            FlowEngineJobStorageKind.InMemoryFallback => "Temporary",
            _ => "Saved"
        };
    }

    public static string GetStorageClass(FlowEngineJobSnapshot job)
    {
        return job.StorageKind switch
        {
            FlowEngineJobStorageKind.InMemoryFallback => "flowengine-job-storage-pill flowengine-job-storage-pill--warning",
            _ => "flowengine-job-storage-pill flowengine-job-storage-pill--stable"
        };
    }

    public static IReadOnlyList<FlowEngineStatusChipViewModel> GetStatusChips(FlowEngineJobSnapshot job)
    {
        var chips = new List<FlowEngineStatusChipViewModel>
        {
            new()
            {
                Label = GetStatusLabel(job.Status),
                CssClass = $"flowengine-job-status-pill {GetStatusClass(job.Status)}"
            }
        };

        if (HasArgument(job, "--test"))
        {
            chips.Add(new FlowEngineStatusChipViewModel
            {
                Label = "TEST",
                CssClass = "flowengine-job-status-pill flowengine-job-status-pill--flag flowengine-job-status-pill--flag-test"
            });
        }

        if (HasArgument(job, "--dry-run"))
        {
            chips.Add(new FlowEngineStatusChipViewModel
            {
                Label = "DRY RUN",
                CssClass = "flowengine-job-status-pill flowengine-job-status-pill--flag flowengine-job-status-pill--flag-dryrun"
            });
        }

        return chips;
    }

    public static bool IsAkeneoExportJob(FlowEngineJobSnapshot job)
    {
        if (!string.Equals(GetSystemLabel(job), "Akeneo", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(job.Name, "akeneo-products", StringComparison.OrdinalIgnoreCase)
            || string.Equals(job.Name, "akeneo-all-products", StringComparison.OrdinalIgnoreCase)
            || string.Equals(job.Name, "akeneo-send-to-centra", StringComparison.OrdinalIgnoreCase)
            || (job.UiLabel?.Contains("Akeneo", StringComparison.OrdinalIgnoreCase) == true
                && (job.UiLabel.Contains("products", StringComparison.OrdinalIgnoreCase)
                    || job.UiLabel.Contains("centra", StringComparison.OrdinalIgnoreCase)));
    }

    public static FlowEngineAkeneoExportPayload? TryParseAkeneoExportPayload(FlowEngineJobSnapshot job)
        => TryParsePayload<FlowEngineAkeneoExportPayload>(job);

    public static FlowEngineAkeneoSendToCentraPayload? TryParseAkeneoSendToCentraPayload(FlowEngineJobSnapshot job)
        => TryParsePayload<FlowEngineAkeneoSendToCentraPayload>(job);

    public static FlowEngineCheckOrdersPayload? TryParseCentraCheckOrdersPayload(FlowEngineJobSnapshot job)
        => IsCommand(job, "centra", "check-orders")
            ? TryParsePayload<FlowEngineCheckOrdersPayload>(job)
            : null;

    public static FlowEngineCreateShipmentsPayload? TryParseCentraCreateShipmentsPayload(FlowEngineJobSnapshot job)
        => IsCommand(job, "centra", "create-shipments")
            ? TryParsePayload<FlowEngineCreateShipmentsPayload>(job)
            : null;

    public static FlowEngineSendOrdersPayload? TryParseCentraSendOrdersPayload(FlowEngineJobSnapshot job)
        => IsCommand(job, "centra", "send-orders")
            ? TryParsePayload<FlowEngineSendOrdersPayload>(job)
            : null;

    public static FlowEngineSendOrderSinglePayload? TryParseCentraSendOrderPayload(FlowEngineJobSnapshot job)
        => IsCommand(job, "centra", "send-order")
            ? TryParsePayload<FlowEngineSendOrderSinglePayload>(job)
            : null;

    public static FlowEngineSendReturnsPayload? TryParseCentraSendReturnsPayload(FlowEngineJobSnapshot job)
        => IsCommand(job, "centra", "send-returns")
            ? TryParsePayload<FlowEngineSendReturnsPayload>(job)
            : null;

    public static FlowEngineShopifyCheckOrdersPayload? TryParseShopifyCheckOrdersPayload(FlowEngineJobSnapshot job)
        => IsCommand(job, "shopify", "check-orders")
            ? TryParsePayload<FlowEngineShopifyCheckOrdersPayload>(job)
            : null;

    public static FlowEngineShopifyValidateOrdersPayload? TryParseShopifyValidateOrdersPayload(FlowEngineJobSnapshot job)
        => IsCommand(job, "shopify", "validate-orders")
            ? TryParsePayload<FlowEngineShopifyValidateOrdersPayload>(job)
            : null;

    public static FlowEngineShopifySendOrdersPayload? TryParseShopifySendOrdersPayload(FlowEngineJobSnapshot job)
        => IsCommand(job, "shopify", "send-orders")
            ? TryParsePayload<FlowEngineShopifySendOrdersPayload>(job)
            : null;

    public static FlowEngineShopifyCompleteOrdersPayload? TryParseShopifyCompleteOrdersPayload(FlowEngineJobSnapshot job)
        => IsCommand(job, "shopify", "complete-orders")
            ? TryParsePayload<FlowEngineShopifyCompleteOrdersPayload>(job)
            : null;

    public static FlowEngineJeevesImportOrderHistoryPayload? TryParseJeevesImportOrderPayload(FlowEngineJobSnapshot job)
        => IsCommand(job, "jeeves", "import-order")
            ? TryParsePayload<FlowEngineJeevesImportOrderHistoryPayload>(job)
            : null;

    public static string GetCommandLabel(FlowEngineJobSnapshot job)
    {
        if (job.Arguments.Count < 2)
            return string.Empty;

        return $"{job.Arguments[0].Trim().ToLowerInvariant()}/{job.Arguments[1].Trim().ToLowerInvariant()}";
    }

    public static bool IsCommand(FlowEngineJobSnapshot job, string system, string command)
    {
        if (job.Arguments.Count < 2)
            return false;

        return string.Equals(job.Arguments[0], system, StringComparison.OrdinalIgnoreCase)
            && string.Equals(job.Arguments[1], command, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasArgument(FlowEngineJobSnapshot job, string argument)
        => job.Arguments.Any(entry => string.Equals(entry, argument, StringComparison.OrdinalIgnoreCase));

    public static TPayload? TryParsePayload<TPayload>(FlowEngineJobSnapshot job)
        where TPayload : class
    {
        var raw = job.Result?.StandardOutput;
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var jsonStart = raw.IndexOf('{');
        if (jsonStart < 0)
            return null;

        try
        {
            return JsonSerializer.Deserialize<TPayload>(
                raw[jsonStart..],
                HistoryJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<string> GetSummaryLines(FlowEngineJobSnapshot job)
    {
        var raw = job.Result?.StandardOutput;
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        var jsonStart = raw.IndexOf('{');
        var summaryBlock = jsonStart >= 0 ? raw[..jsonStart] : raw;

        return summaryBlock
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }
}

public sealed class FlowEngineJeevesImportOrderHistoryPayload
{
    public int CompanyCode { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
    public string ExternalOrderNumber { get; set; } = string.Empty;
    public string CustomerReference { get; set; } = string.Empty;
    public string OrderDate { get; set; } = string.Empty;
    public int OrderType { get; set; }
    public string? DeliveryPlaceCode { get; set; }
    public string PaidAmount { get; set; } = string.Empty;
    public List<FlowEngineJeevesImportOrderHistoryLine> OrderLines { get; set; } = new();
}

public sealed class FlowEngineJeevesImportOrderHistoryLine
{
    public string ArticleNumber { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
}
