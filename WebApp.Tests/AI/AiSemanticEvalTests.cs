// Runs the versioned Intelligence semantic regression dataset.
using System.Text.Json;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiSemanticEvalTests
{
    [Theory]
    [InlineData("Hittills i år jämfört med samma period i fjol")]
    [InlineData("YTD mot föregående år")]
    [InlineData("Hur ligger vi till mot förra året?")]
    [InlineData("Årets omsättning mot förra årets under samma period")]
    public void YearToDateComparisonPhrasings_ProduceTheSameVerifiablePlan(string question)
    {
        var catalog = new AiSemanticCatalog();

        var plan = catalog.CreateFallbackPlan(question);

        Assert.Equal("comparison", plan.Intent);
        Assert.Equal("current_vs_previous_same_period", plan.Comparison);
        Assert.Equal("single_row", plan.ResultContract.Shape);
        Assert.Contains("current_period", plan.ResultContract.RequiredRoles);
        Assert.Contains("previous_period", plan.ResultContract.RequiredRoles);
        Assert.Contains("difference", plan.ResultContract.RequiredRoles);
        Assert.Equal("comparison", plan.ResultContract.PreferredVisualization);
    }

    [Fact]
    public async Task SemanticDataset_ResolvesExpectedBusinessMeaning()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "ai-semantic-evals.json");
        await using var stream = File.OpenRead(path);
        var cases = await JsonSerializer.DeserializeAsync<List<SemanticEvalCase>>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var catalog = new AiSemanticCatalog();

        Assert.NotNull(cases);
        Assert.True(cases!.Count >= 15);
        foreach (var eval in cases)
        {
            var plan = catalog.CreateFallbackPlan(eval.Question);
            Assert.True(eval.Metric == plan.Metric, $"{eval.Question}: expected metric {eval.Metric}, got {plan.Metric}.");
            Assert.True(eval.Intent == plan.Intent, $"{eval.Question}: expected intent {eval.Intent}, got {plan.Intent}.");
            Assert.True(eval.Period == plan.Period, $"{eval.Question}: expected period {eval.Period}, got {plan.Period}.");
            foreach (var dimension in eval.Dimensions)
                Assert.Contains(dimension, plan.Dimensions);
        }
    }

    private sealed class SemanticEvalCase
    {
        public string Question { get; set; } = string.Empty;
        public string Metric { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public string? Period { get; set; }
        public List<string> Dimensions { get; set; } = [];
    }
}
