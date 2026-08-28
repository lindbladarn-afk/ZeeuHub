using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using WebApp.Services.Application;

namespace WebApp.Services.Application.AI;

public sealed class AiConversationMemory : IAiConversationMemory
{
    private const int MaxMessages = 16; // 8 user+assistant turns
    private const string ResultContextSuffix = ":last-result";
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(45);

    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, object> _keyLocks = new(StringComparer.Ordinal);

    public AiConversationMemory(IMemoryCache cache)
    {
        _cache = cache;
    }

    public List<OpenAiChatMessage> GetHistory(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new List<OpenAiChatMessage>();

        return _cache.TryGetValue(key, out List<OpenAiChatMessage>? history)
            ? history?.Select(Clone).ToList() ?? new List<OpenAiChatMessage>()
            : new List<OpenAiChatMessage>();
    }

    public void AppendTurn(string key, string userMessage, string assistantMessage)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var userText = (userMessage ?? string.Empty).Trim();
        var assistantText = (assistantMessage ?? string.Empty).Trim();
        if (userText.Length == 0 || assistantText.Length == 0)
            return;

        lock (_keyLocks.GetOrAdd(key, _ => new object()))
        {
            var history = GetHistory(key);
            history.Add(new OpenAiChatMessage { Role = "user", Content = userText });
            history.Add(new OpenAiChatMessage { Role = "assistant", Content = assistantText });

            if (history.Count > MaxMessages)
                history = history.Skip(history.Count - MaxMessages).ToList();

            _cache.Set(key, history, new MemoryCacheEntryOptions { SlidingExpiration = SlidingExpiration });
        }
    }

    public AiConversationResultContext? GetLastResultContext(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return _cache.TryGetValue(BuildResultContextKey(key), out AiConversationResultContext? resultContext) &&
               resultContext is not null
            ? Clone(resultContext)
            : null;
    }

    public void SetLastResultContext(string key, AiConversationResultContext resultContext)
    {
        if (string.IsNullOrWhiteSpace(key) || resultContext is null)
            return;

        _cache.Set(
            BuildResultContextKey(key),
            Clone(resultContext),
            new MemoryCacheEntryOptions { SlidingExpiration = SlidingExpiration });
    }

    public void Clear(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        _cache.Remove(key);
        _cache.Remove(BuildResultContextKey(key));
        _keyLocks.TryRemove(key, out _);
    }

    private static string BuildResultContextKey(string key) => $"{key}{ResultContextSuffix}";

    private static OpenAiChatMessage Clone(OpenAiChatMessage message) =>
        new() { Role = message.Role, Content = message.Content };

    private static AiConversationResultContext Clone(AiConversationResultContext resultContext) =>
        new()
        {
            CapturedAtUtc = resultContext.CapturedAtUtc,
            Intent = resultContext.Intent,
            Metric = resultContext.Metric,
            Period = resultContext.Period,
            Columns = resultContext.Columns.ToList(),
            Rows = resultContext.Rows.Select(row => row.ToList()).ToList(),
            TotalRowCount = resultContext.TotalRowCount,
            Truncated = resultContext.Truncated
        };
}
