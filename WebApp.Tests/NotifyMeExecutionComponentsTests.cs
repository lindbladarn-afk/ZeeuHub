using WebApp.Services.NotifyMe;

namespace WebApp.Tests;

// Covers the small execution helpers that keep NotifyMe runtime behavior isolated.
public sealed class NotifyMeExecutionComponentsTests
{
    [Fact]
    public void BuildHtml_EncodesStateAndResultValues()
    {
        var renderer = new NotifyMeMailRenderer();
        var state = new PortalNotifyMeState
        {
            NotificationId = 42,
            Description = "<Order>",
            TypeCode = "A&B",
            PriorityCode = "Hög",
            Comment = "Kontrollera <status>"
        };
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?>
            {
                ["Kund <nr>"] = "A&B",
                ["Belopp"] = 12.5m,
                ["Skapad"] = new DateTime(2026, 6, 19, 8, 30, 15)
            }
        };

        var html = renderer.BuildHtml(state, rows);

        Assert.Contains("&lt;Order&gt;", html);
        Assert.Contains("A&amp;B", html);
        Assert.Contains("Kontrollera &lt;status&gt;", html);
        Assert.Contains("Kund &lt;nr&gt;", html);
        Assert.Contains("12.5", html);
        Assert.Contains("2026-06-19 08:30:15", html);
        Assert.DoesNotContain("<Order>", html);
        Assert.DoesNotContain("Kontrollera <status>", html);
    }

    [Fact]
    public void ComposeNoHitTestMail_BuildsConfirmationMessage()
    {
        var mail = NotifyMeNoHitTestMailComposer.Compose("Order saknar leveransdatum");

        Assert.Equal("[Ingen träff] Order saknar leveransdatum", mail.Subject);
        Assert.Contains("Ingen träff i källdatan", mail.Html);
        Assert.Contains("testmailet skickades", mail.Html);
    }

    [Fact]
    public void GroupByRecipient_GroupsDynamicRowsByMottagare()
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Mottagare"] = "anna@example.com", ["OrderNr"] = 100 },
            new Dictionary<string, object?> { ["Mottagare"] = "bo@example.com", ["OrderNr"] = 200 },
            new Dictionary<string, object?> { ["Mottagare"] = "anna@example.com", ["OrderNr"] = 300 }
        };

        var batches = NotifyMeDynamicRecipientGrouper.GroupByRecipient(rows);

        Assert.Equal(2, batches.Count);
        Assert.Equal("anna@example.com", batches[0].Recipient);
        Assert.Equal(2, batches[0].Rows.Count);
        Assert.Equal("bo@example.com", batches[1].Recipient);
        Assert.Single(batches[1].Rows);
    }

    [Fact]
    public void GroupByRecipient_RequiresMottagareColumn()
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["OrderNr"] = 100 }
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => NotifyMeDynamicRecipientGrouper.GroupByRecipient(rows));

        Assert.Contains("Mottagare", exception.Message);
    }

    [Fact]
    public void GroupByRecipient_RequiresRecipientValue()
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Mottagare"] = " ", ["OrderNr"] = 100 }
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => NotifyMeDynamicRecipientGrouper.GroupByRecipient(rows));

        Assert.Contains("Mottagare", exception.Message);
    }

    [Theory]
    [InlineData("Regeln saknar mottagare.")]
    [InlineData("Regeln saknar SQL-underlag.")]
    [InlineData("Dynamiska mottagare stöds inte ännu.")]
    [InlineData("Notifieringen hittades inte.")]
    public void IsRetryable_TreatsConfigurationErrorsAsPermanent(string message)
    {
        var policy = new NotifyMeRetryPolicy();

        var retryable = policy.IsRetryable(new InvalidOperationException(message));

        Assert.False(retryable);
    }

    [Fact]
    public void IsRetryable_RetriesUnexpectedInvalidOperation()
    {
        var policy = new NotifyMeRetryPolicy();

        var retryable = policy.IsRetryable(new InvalidOperationException("Tillfälligt körningsfel."));

        Assert.True(retryable);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 6)]
    public void CalculateRetryAt_UsesConfiguredMinuteDelays(int attempt, int expectedMinutes)
    {
        var policy = new NotifyMeRetryPolicy();
        var failedAt = new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc);

        var retryAt = policy.CalculateRetryAt(failedAt, attempt);

        Assert.Equal(failedAt.AddMinutes(expectedMinutes), retryAt);
    }
}
