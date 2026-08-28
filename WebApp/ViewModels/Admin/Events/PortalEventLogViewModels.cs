namespace WebApp.ViewModels.Admin;

public sealed class PortalEventLogListItemVm
{
    public Guid Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int? JeevesCompanyCode { get; set; }
    public string? UserEmail { get; set; }
    public string? RequestPath { get; set; }
    public string? CorrelationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? AdditionalData { get; set; }
}

public sealed class PortalEventLogFilterOptionVm
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class PortalEventLogCompanyFilterOptionVm
{
    public Guid CompanyId { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class PortalEventLogsPageVm
{
    public int DaysBack { get; set; } = 7;
    public string? Module { get; set; }
    public string? Severity { get; set; }
    public Guid? CompanyId { get; set; }
    public string? Search { get; set; }
    public int TotalEvents { get; set; }
    public int EventsLast24Hours { get; set; }
    public int DistinctModules { get; set; }
    public int DistinctCompanies { get; set; }
    public int LatestPage { get; set; } = 1;
    public int LatestPageSize { get; set; } = 10;
    public int LatestTotalCount { get; set; }
    public int LatestTotalPages => LatestPageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(LatestTotalCount / (double)LatestPageSize));
    public IReadOnlyCollection<PortalEventLogFilterOptionVm> AvailableModules { get; set; } = Array.Empty<PortalEventLogFilterOptionVm>();
    public IReadOnlyCollection<PortalEventLogFilterOptionVm> AvailableSeverities { get; set; } = Array.Empty<PortalEventLogFilterOptionVm>();
    public IReadOnlyCollection<PortalEventLogCompanyFilterOptionVm> AvailableCompanies { get; set; } = Array.Empty<PortalEventLogCompanyFilterOptionVm>();
    public IReadOnlyCollection<PortalEventLogListItemVm> Latest { get; set; } = Array.Empty<PortalEventLogListItemVm>();
}
