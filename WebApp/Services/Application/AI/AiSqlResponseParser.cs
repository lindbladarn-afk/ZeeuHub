using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

/// <summary>
/// Parses structured LLM responses for SQL generation.
/// This class controls how JSON payloads like
/// { "sql": "...", "requires_clarification": false, "reason": "..." }
/// are interpreted before SQL execution logic continues.
/// </summary>
public static class AiSqlResponseParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryParseStructuredSqlResponse(
        string raw,
        out string? sql,
        out bool requiresClarification,
        out string? reason)
    {
        var success = TryParseStructuredQueryResponse(raw, out var response);
        sql = response.Sql;
        requiresClarification = response.RequiresClarification;
        reason = response.Reason;
        return success;
    }

    public static bool TryParseStructuredQueryResponse(
        string raw,
        out AiStructuredQueryResponse response)
    {
        response = new AiStructuredQueryResponse();
        if (!TryExtractJsonObject(raw, out var json))
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<AiStructuredQueryResponse>(json, SerializerOptions);
            if (parsed is null)
                return false;

            response = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryExtractJsonObject(string text, out string json)
    {
        json = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var start = text.IndexOf('{');
        if (start < 0)
            return false;

        var depth = 0;
        var inString = false;
        var escape = false;
        var end = -1;

        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];

            if (inString)
            {
                if (escape)
                {
                    escape = false;
                }
                else if (ch == '\\')
                {
                    escape = true;
                }
                else if (ch == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{')
            {
                depth++;
                continue;
            }

            if (ch == '}')
            {
                depth--;
                if (depth == 0)
                {
                    end = i;
                    break;
                }
            }
        }

        if (end <= start)
            return false;

        json = text.Substring(start, end - start + 1);
        return true;
    }
}

public sealed class AiStructuredQueryResponse
{
    [JsonPropertyName("plan")]
    public AiQueryPlan? Plan { get; set; }

    [JsonPropertyName("sql")]
    public string? Sql { get; set; }

    [JsonPropertyName("requires_clarification")]
    public bool RequiresClarification { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
