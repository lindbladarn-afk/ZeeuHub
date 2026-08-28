// Verifies safe Intelligence errors and deterministic follow-up suggestions.
using System.Text.Json;
using WebApp.Models.AI;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiQueryResponsePresenterTests
{
    [Fact]
    public void Prepare_QueryFailure_ReplacesTechnicalDetailsWithSafeError()
    {
        var response = new AiQueryResponse
        {
            Success = false,
            Answer = "SQL-körningen stoppades: Invalid column InternalSecret.",
            ErrorMessage = "Invalid column InternalSecret.",
            Warning = "SQL execution failed."
        };

        var prepared = AiQueryResponsePresenter.Prepare(response, "Visa omsättning");

        Assert.Equal("execution_failed", prepared.Error?.Code);
        Assert.DoesNotContain("InternalSecret", prepared.Answer, StringComparison.Ordinal);
        Assert.DoesNotContain("säker fråga", prepared.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Null(prepared.Warning);
        Assert.Null(prepared.Sql);
        Assert.True(prepared.Error?.CanRetry);
        Assert.NotEmpty(prepared.Suggestions);
    }

    [Fact]
    public void Prepare_CustomerQueryFailure_ReturnsCustomerSpecificSuggestions()
    {
        var response = new AiQueryResponse
        {
            Success = false,
            Answer = "Kunde inte generera SQL.",
            ErrorMessage = "Kunde inte generera SQL."
        };

        var prepared = AiQueryResponsePresenter.Prepare(response, "Visa mina kunder");

        Assert.Equal("planning_failed", prepared.Error?.Code);
        Assert.Equal(
            ["Visa alla kunder", "Visa kundnummer och kundnamn"],
            prepared.Suggestions);
    }

    [Fact]
    public void Prepare_Clarification_PreservesConversationalQuestion()
    {
        var response = new AiQueryResponse
        {
            Success = false,
            Answer = "Menar du antal sålda eller omsättning?",
            ErrorMessage = "Frågan kräver förtydligande."
        };

        var prepared = AiQueryResponsePresenter.Prepare(response, "Visa försäljning");

        Assert.Equal("clarification_required", prepared.Error?.Code);
        Assert.Equal("Menar du antal sålda eller omsättning?", prepared.Answer);
        Assert.Equal("info", prepared.Error?.Tone);
        Assert.Contains(prepared.Suggestions, suggestion => suggestion.Contains("antal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Prepare_TruncatedResult_AddsUsefulFollowUpSuggestions()
    {
        var response = new AiQueryResponse
        {
            Rows = [[1], [2]],
            Truncated = true
        };

        var prepared = AiQueryResponsePresenter.Prepare(response, "Visa kunder");

        Assert.Contains("Begränsa resultatet till de 20 största posterna", prepared.Suggestions);
        Assert.Contains("Bryt ned samma analys per månad", prepared.Suggestions);
    }

    [Fact]
    public void Serialization_DoesNotExposeTechnicalErrorMessage()
    {
        var response = new AiQueryResponse
        {
            Success = false,
            ErrorMessage = "Server=secret;Password=secret",
            Error = new AiQueryError { Message = "Ett säkert felmeddelande." }
        };

        var json = JsonSerializer.Serialize(response);
        var roundTrip = JsonSerializer.Deserialize<AiQueryResponse>(json);

        Assert.DoesNotContain("Password", json, StringComparison.Ordinal);
        Assert.Equal("Ett säkert felmeddelande.", roundTrip?.Error?.Message);
    }
}
