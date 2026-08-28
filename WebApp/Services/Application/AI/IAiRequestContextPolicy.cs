// Defines how untrusted AI requests receive their verified server-side tenant context.
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

public interface IAiRequestContextPolicy
{
    AiRequestContextResult Apply(
        AiQueryRequest request,
        JeevesRuntimeContext runtimeContext,
        bool isAdministrator,
        bool requireTenantDataSource);
}

public sealed record AiRequestContextResult(bool Success, string? Error)
{
    public static AiRequestContextResult Allowed() => new(true, null);

    public static AiRequestContextResult Denied(string error) => new(false, error);
}
