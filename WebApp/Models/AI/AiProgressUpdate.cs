// Defines progress events emitted by the real Intelligence execution stages.
namespace WebApp.Models.AI;

public sealed record AiProgressUpdate(string Step, string Message, int Percent);

public delegate Task AiProgressCallback(AiProgressUpdate update, CancellationToken cancellationToken);

public sealed class AiQueryStreamEvent
{
    public string Type { get; init; } = string.Empty;
    public AiProgressUpdate? Progress { get; init; }
    public AiQueryResponse? Result { get; init; }

    public static AiQueryStreamEvent FromProgress(AiProgressUpdate update) =>
        new() { Type = "progress", Progress = update };

    public static AiQueryStreamEvent FromResult(AiQueryResponse result) =>
        new() { Type = "result", Result = result };
}
