using System.Net;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraJeevesDuplicateOrderException : Exception;

public sealed class FlowEngineCentraJeevesManualReviewException : Exception
{
    public FlowEngineCentraJeevesManualReviewException(string message)
        : base(message)
    {
    }
}

internal sealed record FlowEngineCentraAuthorizedResponse(HttpStatusCode StatusCode, string Body)
{
    public bool IsSuccessStatusCode => (int)StatusCode is >= 200 and <= 299;
}
