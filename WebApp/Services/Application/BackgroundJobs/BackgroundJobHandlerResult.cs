namespace WebApp.Services.Application.BackgroundJobs;

public sealed class BackgroundJobHandlerResult
{
    public bool Succeeded { get; set; }
    // Persisted with the job; keep this compact and free from business-row data.
    public string? ResultJson { get; set; }
    // Published to short-lived runtime status only; may include a bounded row preview.
    public string? RuntimeResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan? RetryDelay { get; set; }

    public static BackgroundJobHandlerResult Success(string? resultJson = null, string? runtimeResultJson = null)
        => new()
        {
            Succeeded = true,
            ResultJson = resultJson,
            RuntimeResultJson = runtimeResultJson
        };

    public static BackgroundJobHandlerResult Retry(
        string? errorCode,
        string? errorMessage,
        TimeSpan retryDelay,
        string? resultJson = null,
        string? runtimeResultJson = null)
        => new()
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            RetryDelay = retryDelay,
            ResultJson = resultJson,
            RuntimeResultJson = runtimeResultJson
        };

    public static BackgroundJobHandlerResult Failure(
        string? errorCode,
        string? errorMessage,
        string? resultJson = null,
        string? runtimeResultJson = null)
        => new()
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ResultJson = resultJson,
            RuntimeResultJson = runtimeResultJson
        };
}
