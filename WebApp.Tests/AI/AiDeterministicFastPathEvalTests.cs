// Guards the low-cost route for frequent business questions without blocking novel analysis requests.
using System.Reflection;
using System.Text.Json;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiDeterministicFastPathEvalTests
{
    [Fact]
    public async Task QuestionBattery_UsesTheExpectedExecutionRoute()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "ai-deterministic-fast-path-evals.json");
        await using var stream = File.OpenRead(path);
        var cases = await JsonSerializer.DeserializeAsync<List<FastPathEvalCase>>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(cases);
        Assert.True(cases!.Count >= 30, "Frågebatteriet ska täcka minst 30 formuleringar.");

        foreach (var testCase in cases)
        {
            var actual = ShouldUseFastPath(testCase.Question);
            Assert.True(
                actual == testCase.UseDeterministicFastPath,
                $"{testCase.Question}: expected fast path {testCase.UseDeterministicFastPath}, got {actual}.");
        }
    }

    private static bool ShouldUseFastPath(string question)
    {
        var method = typeof(AiDbChatOrchestrator).GetMethod(
            "ShouldUseDeterministicFastPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, [question]));
    }

    private sealed class FastPathEvalCase
    {
        public string Question { get; set; } = string.Empty;
        public bool UseDeterministicFastPath { get; set; }
    }
}
