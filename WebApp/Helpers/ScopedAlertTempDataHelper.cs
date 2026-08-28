using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using WebApp.Models;

namespace WebApp.Helpers;

public static class ScopedAlertTempDataHelper
{
    public const string ScopedAlertsKey = "__ScopedAlerts";

    public static string BuildScope(string? controllerName, string? actionName)
    {
        var controller = string.IsNullOrWhiteSpace(controllerName) ? "unknown" : controllerName.Trim();
        var action = string.IsNullOrWhiteSpace(actionName) ? "index" : actionName.Trim();
        return $"{controller}:{action}".ToLowerInvariant();
    }

    public static void Add(ITempDataDictionary tempData, string level, string? message, string? controllerName, string? actionName)
    {
        if (tempData == null || string.IsNullOrWhiteSpace(message))
            return;

        var alerts = Read(tempData, peek: true);
        alerts.Add(new ScopedAlertMessage
        {
            Level = string.IsNullOrWhiteSpace(level) ? Alert.INFORMATION : level,
            Message = message.Trim(),
            Scope = BuildScope(controllerName, actionName)
        });

        Write(tempData, alerts);
    }

    public static List<ScopedAlertMessage> Read(ITempDataDictionary tempData, bool peek)
    {
        if (tempData == null)
            return new List<ScopedAlertMessage>();

        var raw = peek ? tempData.Peek(ScopedAlertsKey) as string : tempData[ScopedAlertsKey] as string;
        if (string.IsNullOrWhiteSpace(raw))
            return new List<ScopedAlertMessage>();

        try
        {
            return JsonSerializer.Deserialize<List<ScopedAlertMessage>>(raw) ?? new List<ScopedAlertMessage>();
        }
        catch
        {
            return new List<ScopedAlertMessage>();
        }
    }

    public static void Write(ITempDataDictionary tempData, IReadOnlyCollection<ScopedAlertMessage> alerts)
    {
        if (tempData == null)
            return;

        if (alerts == null || alerts.Count == 0)
        {
            tempData.Remove(ScopedAlertsKey);
            return;
        }

        tempData[ScopedAlertsKey] = JsonSerializer.Serialize(alerts);
    }
}
