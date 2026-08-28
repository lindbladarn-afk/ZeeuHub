// Protects the Intelligence progress, error, feedback, suggestion, and chart UX contracts.
namespace WebApp.Tests;

public sealed class AiUxClientContractTests
{
    private static readonly string WebAppRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));

    [Fact]
    public void QueryClient_ConsumesRealServerProgressInsteadOfTimedStatusText()
    {
        var client = ReadScript("ai-query-client.js");
        var ui = ReadScript("ai.js");

        Assert.Contains("'/AI/query-stream'", client, StringComparison.Ordinal);
        Assert.Contains("response.body.getReader()", client, StringComparison.Ordinal);
        Assert.Contains("streamEvent.type === 'progress'", client, StringComparison.Ordinal);
        Assert.Contains("updateProgressBubble(loadingBubble, progress)", ui, StringComparison.Ordinal);
        Assert.DoesNotContain("startProgressiveStatus", ui, StringComparison.Ordinal);
        Assert.DoesNotContain("thinkingSteps", ui, StringComparison.Ordinal);
    }

    [Fact]
    public void Client_SeparatesApplicationErrorsFromSuccessfulAnswers()
    {
        var ui = ReadScript("ai.js");

        Assert.Contains("if (resp?.success === false)", ui, StringComparison.Ordinal);
        Assert.Contains("resp?.error?.code === 'clarification_required'", ui, StringComparison.Ordinal);
        Assert.Contains("e?.canRetry !== false", ui, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorBubble_DoesNotUseGlobalFullPageErrorClass()
    {
        var view = File.ReadAllText(Path.Combine(
            WebAppRoot,
            "Views",
            "AI",
            "Partials",
            "_AiChatPanel.cshtml"));
        var css = File.ReadAllText(Path.Combine(WebAppRoot, "wwwroot", "css", "ai.css"));

        Assert.Contains("chat-bubble ai-error-bubble", view, StringComparison.Ordinal);
        Assert.DoesNotContain("chat-bubble error", view, StringComparison.Ordinal);
        Assert.Contains(".chat-bubble.ai-error-bubble", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".chat-bubble.error", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AnswersExposeFeedbackAndContextualSuggestions()
    {
        var view = File.ReadAllText(Path.Combine(
            WebAppRoot,
            "Views",
            "AI",
            "Partials",
            "_AiChatPanel.cshtml"));
        var ui = ReadScript("ai.js");
        var client = ReadScript("ai-query-client.js");

        Assert.Contains("data-ai-feedback=\"helpful\"", view, StringComparison.Ordinal);
        Assert.Contains("data-ai-feedback=\"not_helpful\"", view, StringComparison.Ordinal);
        Assert.Contains("ai-suggestion-wrap", view, StringComparison.Ordinal);
        Assert.Contains("queryClient.submitFeedback", ui, StringComparison.Ordinal);
        Assert.Contains("resp?.suggestions", ui, StringComparison.Ordinal);
        Assert.Contains("'/AI/feedback'", client, StringComparison.Ordinal);
    }

    [Fact]
    public void ChartLayerDetectsMetricsAndOffersMultipleChartTypes()
    {
        var chart = ReadScript("ai-chart.js");
        var resultsView = File.ReadAllText(Path.Combine(
            WebAppRoot,
            "Views",
            "AI",
            "Partials",
            "_AiResultsPanel.cshtml"));

        Assert.Contains("isNumericColumn", chart, StringComparison.Ordinal);
        Assert.Contains(".slice(0, 3)", chart, StringComparison.Ordinal);
        Assert.Contains("looksTemporal", chart, StringComparison.Ordinal);
        Assert.Contains("horizontalBar", resultsView, StringComparison.Ordinal);
        Assert.Contains("ai-chart-summary", resultsView, StringComparison.Ordinal);
    }

    [Fact]
    public void DynamicColumnLabelsAreAddedAsTextNotHtml()
    {
        var ui = ReadScript("ai.js");

        Assert.Contains("document.createTextNode(` ${escapeText(name)}`)", ui, StringComparison.Ordinal);
        Assert.DoesNotContain("label.innerHTML = `<input", ui, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryCanBeCancelledAndEvidenceIsShown()
    {
        var client = ReadScript("ai-query-client.js");
        var ui = ReadScript("ai.js");
        var view = File.ReadAllText(Path.Combine(
            WebAppRoot,
            "Views",
            "AI",
            "Partials",
            "_AiChatPanel.cshtml"));

        Assert.Contains("cancelActiveQuery", client, StringComparison.Ordinal);
        Assert.Contains("id=\"ai-cancel\"", view, StringComparison.Ordinal);
        Assert.Contains("queryClient.cancelActiveQuery()", ui, StringComparison.Ordinal);
        Assert.Contains("resp?.evidence?.verificationStatus", ui, StringComparison.Ordinal);
        Assert.Contains("resp.evidence.metricLabel", ui, StringComparison.Ordinal);
    }

    [Fact]
    public void ChartPreservesMissingValuesInsteadOfTurningThemIntoZero()
    {
        var chart = ReadScript("ai-chart.js");

        Assert.Contains("data: chartRows.map(row => parseNumber(row?.[index]))", chart, StringComparison.Ordinal);
        Assert.DoesNotContain("parseNumber(row?.[index]) ?? 0", chart, StringComparison.Ordinal);
    }

    [Fact]
    public void ChartExcludesIdentifiersFromMetricsAndLabelsAxes()
    {
        var chart = ReadScript("ai-chart.js");

        Assert.Contains("const isIdentifierColumn", chart, StringComparison.Ordinal);
        Assert.Contains(".filter(index => !isIdentifierColumn(columns[index]))", chart, StringComparison.Ordinal);
        Assert.Contains("'number', 'nummer'", chart, StringComparison.Ordinal);
        Assert.Contains("'code', 'kod'", chart, StringComparison.Ordinal);
        Assert.Contains("text: horizontal ? metricTitle : current.labelName", chart, StringComparison.Ordinal);
        Assert.Contains("text: horizontal ? current.labelName : metricTitle", chart, StringComparison.Ordinal);
    }

    [Fact]
    public void ChartUsesTheVerifiedResultContractForComparisonResults()
    {
        var chart = ReadScript("ai-chart.js");
        var ui = ReadScript("ai.js");

        Assert.Contains("resp?.plan?.resultContract?.preferredVisualization", ui, StringComparison.Ordinal);
        Assert.Contains("preferredType === 'comparison' && rows.length === 1", chart, StringComparison.Ordinal);
        Assert.Contains("labels: metricIndexes.map(index => String(columns[index]))", chart, StringComparison.Ordinal);
    }

    private static string ReadScript(string fileName) =>
        File.ReadAllText(Path.Combine(WebAppRoot, "wwwroot", "js", fileName));
}
