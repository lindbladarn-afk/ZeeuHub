using Microsoft.AspNetCore.Http;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineRequestNormalizer : IFlowEngineRequestNormalizer
{
    public FlowEngineExecuteJobRequest Normalize(FlowEngineExecuteJobRequest request, IFormCollection? form)
    {
        if (form is null)
            return request;

        request.Flags.TestMode = GetBoolean(form, "FlowEngineGlobalTestMode", request.Flags.TestMode);
        request.Flags.DryRun = GetBoolean(form, "FlowEngineGlobalDryRun", request.Flags.DryRun);
        request.Flags.DebugHttp = GetBoolean(form, "FlowEngineGlobalDebugHttp", request.Flags.DebugHttp);
        request.Flags.SkipJeevesCheck = GetBoolean(form, "FlowEngineGlobalSkipJeevesCheck", request.Flags.SkipJeevesCheck);

        var useLimit = GetBoolean(form, "FlowEngineGlobalUseLimit", request.Params.UseLimit);
        request.Params.UseLimit = useLimit;

        if (useLimit && TryGetInt32(form, "FlowEngineGlobalLimit", out var limit) && limit > 0)
            request.Params.Limit = limit;

        return request;
    }

    private static bool GetBoolean(IFormCollection form, string key, bool fallback)
    {
        if (!form.TryGetValue(key, out var values))
            return fallback;

        var raw = values.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        if (bool.TryParse(raw, out var parsed))
            return parsed;

        return raw.Trim() switch
        {
            "1" => true,
            "0" => false,
            _ => fallback
        };
    }

    private static bool TryGetInt32(IFormCollection form, string key, out int value)
    {
        value = 0;
        return form.TryGetValue(key, out var values)
            && int.TryParse(values.ToString(), out value);
    }
}
