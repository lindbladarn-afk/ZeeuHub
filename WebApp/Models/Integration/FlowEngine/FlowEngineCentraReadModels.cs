using System.Text.Json;

namespace WebApp.Services.Integration.FlowEngine;

internal sealed record CentraDateSelection(DateTime SinceUtc, DateTime UntilUtc, string SelectionKind);

internal sealed record CentraPagedCollectionResult(IReadOnlyList<JsonElement> Items, IReadOnlyList<JsonElement> Errors);
