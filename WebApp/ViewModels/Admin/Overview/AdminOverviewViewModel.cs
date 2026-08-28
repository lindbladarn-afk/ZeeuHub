namespace WebApp.ViewModels.Admin;

public class AdminOverviewViewModel
{
    public int CompanyCount { get; set; }
    public int UserCount { get; set; }
    public int ExcelImports { get; set; }
    public int AiQueries { get; set; }
    public int SessionMinutes { get; set; }
    public string SessionDurationText { get; set; } = "0 min";
    public List<HealthStatusItem> HealthStatuses { get; set; } = new();
    public int InternalOperationsCount { get; set; }
    public int InternalOperationsHighPriorityCount { get; set; }
    public bool InternalOperationsDegraded { get; set; }
    public int InternalOperationsProviderFailureCount { get; set; }
    public List<InternalOperationsItem> InternalOperations { get; set; } = new();
    public List<InternalOperationsProviderFailure> InternalOperationsProviderFailures { get; set; } = new();

    public class HealthStatusItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool Pending { get; set; }
    }

    public class InternalOperationsItem
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string PriorityLabel { get; set; } = string.Empty;
        public string PriorityCssClass { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public string? LinkText { get; set; }
        public string? LinkUrl { get; set; }
    }

    public class InternalOperationsProviderFailure
    {
        public string ProviderKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
    }
}
