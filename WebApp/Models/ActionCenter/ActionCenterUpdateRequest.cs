using System;
using System.Text.Json.Serialization;

namespace WebApp.Models.ActionCenter;

// Request snapshot saved when a user changes the state of an Action Center insight.
public sealed class ActionCenterUpdateRequest
{
    public const int MaxInsightIdLength = 256;
    public const int MaxTitleLength = 256;
    public const int MaxDescriptionLength = 512;
    public const int MaxCategoryLength = 64;
    public const int MaxCommentLength = 256;

    [JsonPropertyName("insightId")]
    public string InsightId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("priority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ActionCenterPriority? Priority { get; set; }

    [JsonPropertyName("detectedAt")]
    public DateTime? DetectedAt { get; set; }
}
