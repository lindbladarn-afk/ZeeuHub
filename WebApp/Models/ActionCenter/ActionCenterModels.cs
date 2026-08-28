using System;
using System.Collections.Generic;
using WebApp.ViewModels.Shared;

namespace WebApp.Models.ActionCenter;

public enum ActionCenterPriority
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

public enum ActionCenterStatus
{
    Open = 0,
    InProgress = 1,
    Done = 2
}

public enum ActionCenterAudience
{
    Customer = 0,
    InternalAdmin = 1
}

public sealed class ActionCenterInsight
{
    public string Key { get; init; } = string.Empty;
    public ActionCenterAudience Audience { get; init; } = ActionCenterAudience.Customer;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; set; } = "Allmänt";
    public ActionCenterStatus Status { get; set; } = ActionCenterStatus.Open;
    public ActionCenterPriority Priority { get; init; }
    public DateTime DetectedAt { get; init; }
    public DateTime? DueAt { get; init; }
    public string? AssignedTo { get; init; }
    public bool IsMock { get; init; }
    public string? LinkText { get; init; }
    public string? LinkUrl { get; init; }
    public string? Comment { get; set; }
    public IReadOnlyList<ActionCenterMetric> Metrics { get; init; } = Array.Empty<ActionCenterMetric>();
    public IReadOnlyList<ActionCenterTimelinePoint> Timeline { get; init; } = Array.Empty<ActionCenterTimelinePoint>();
}

public sealed class ActionCenterMetric
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class ActionCenterTimelinePoint
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal Amount { get; init; }
}

public sealed class ActionCenterViewModel
{
    public int TotalCount { get; init; }
    public ActionCenterAudience Audience { get; init; } = ActionCenterAudience.Customer;
    public bool IsDegraded { get; init; }
    public ModuleBannerViewModel? AvailabilityBanner { get; init; }
    public IReadOnlyList<ActionCenterInsight> Insights { get; init; } = Array.Empty<ActionCenterInsight>();
    public IReadOnlyList<ActionCenterHistoryItem> History { get; init; } = Array.Empty<ActionCenterHistoryItem>();
    public IReadOnlyList<ActionCenterProviderFailure> ProviderFailures { get; init; } = Array.Empty<ActionCenterProviderFailure>();
}

public sealed class ActionCenterSummaryDto
{
    public int Count { get; init; }
    public bool HasHighPriority { get; init; }
    public bool IsDegraded { get; init; }
    public ActionCenterAudience Audience { get; init; } = ActionCenterAudience.Customer;
    public DateTime? LatestDetectedAt { get; init; }
}

public sealed class ActionCenterHistoryItem
{
    public string Key { get; init; } = string.Empty;
    public ActionCenterAudience Audience { get; init; } = ActionCenterAudience.Customer;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = "Allmänt";
    public ActionCenterPriority Priority { get; init; }
    public DateTime DetectedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Comment { get; init; }
}

public sealed class ActionCenterProviderFailure
{
    public string ProviderKey { get; init; } = string.Empty;
    public ActionCenterAudience Audience { get; init; } = ActionCenterAudience.Customer;
    public string Message { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
