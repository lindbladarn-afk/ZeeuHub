// Holds a compact, prompt-safe snapshot of the latest database result for follow-up questions.
using System;
using System.Collections.Generic;

namespace WebApp.Services.Application.AI;

public sealed class AiConversationResultContext
{
    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;
    public string? Intent { get; init; }
    public string? Metric { get; init; }
    public string? Period { get; init; }
    public List<string> Columns { get; init; } = [];
    public List<List<string>> Rows { get; init; } = [];
    public int TotalRowCount { get; init; }
    public bool Truncated { get; init; }
}
