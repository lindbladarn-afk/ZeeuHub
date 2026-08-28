using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Tests;

// AI suggestion tests verify that model output cannot bypass deterministic reconciliation rules.
public sealed class BankReconciliationAiSuggestionVerifierTests
{
    private readonly BankReconciliationAiSuggestionVerifier _verifier = new();

    [Fact]
    public void Verify_ValidRuleCandidateSuggestion_IsAcceptedWithManualConfirmation()
    {
        var request = CreateRequest();
        var candidate = new BankReconciliationAiSuggestionCandidate
        {
            InvoiceId = "1001",
            MatchedAmount = 100m,
            Currency = "SEK",
            ConfidenceScore = 85,
            RequiresManualConfirmation = true
        };

        var result = _verifier.Verify(request, candidate);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal("verified", result.Candidate.VerificationStatus);
    }

    [Fact]
    public void Verify_InvoiceOutsideRuleCandidates_IsRejected()
    {
        var request = CreateRequest();
        var candidate = new BankReconciliationAiSuggestionCandidate
        {
            InvoiceId = "9999",
            MatchedAmount = 100m,
            Currency = "SEK",
            ConfidenceScore = 85,
            RequiresManualConfirmation = true
        };

        var result = _verifier.Verify(request, candidate);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("inte finns bland regelmotorns kandidater", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("rejected", result.Candidate.VerificationStatus);
    }

    [Fact]
    public void Verify_AiRemovesManualConfirmation_IsRejected()
    {
        var request = CreateRequest(ruleRequiresManualConfirmation: true);
        var candidate = new BankReconciliationAiSuggestionCandidate
        {
            InvoiceId = "1001",
            MatchedAmount = 100m,
            Currency = "SEK",
            ConfidenceScore = 85,
            RequiresManualConfirmation = false
        };

        var result = _verifier.Verify(request, candidate);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("manuell bekräftelse", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Verify_AmountAboveRemaining_IsRejected()
    {
        var request = CreateRequest();
        var candidate = new BankReconciliationAiSuggestionCandidate
        {
            InvoiceId = "1001",
            MatchedAmount = 150m,
            Currency = "SEK",
            ConfidenceScore = 85,
            RequiresManualConfirmation = true
        };

        var result = _verifier.Verify(request, candidate);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("överstiger", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Verify_UnbookedTransaction_IsRejectedBySharedEligibilityRules()
    {
        var request = CreateRequest();
        request.Transaction.EntryStatus = "PDNG";

        var result = _verifier.Verify(request, ValidCandidate());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("BOOK", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Verify_CurrencyMismatch_IsRejectedBySharedEligibilityRules()
    {
        var request = CreateRequest();
        request.Transaction.Currency = "EUR";

        var result = _verifier.Verify(request, ValidCandidate());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("ISO-valuta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Verify_SupplierDebit_UsesAbsoluteAmountAndIsAccepted()
    {
        var request = CreateRequest();
        request.Transaction.Amount = -100m;
        request.Transaction.Direction = "DBIT";
        request.RuleCandidates[0].Invoice.IsSupplierInvoice = true;

        var result = _verifier.Verify(request, ValidCandidate());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task DisabledService_ReturnsDisabledResultWithStableHash()
    {
        var service = new DisabledBankReconciliationAiSuggestionService(Options.Create(new BankReconciliationAiSuggestionOptions
        {
            Enabled = false,
            PromptVersion = "test-v1"
        }));
        var request = CreateRequest();

        var first = await service.BuildSuggestionsAsync(request);
        var second = await service.BuildSuggestionsAsync(request);

        Assert.False(first.Enabled);
        Assert.Equal("disabled", first.Status);
        Assert.Equal("test-v1", first.PromptVersion);
        Assert.NotEmpty(first.InputHash);
        Assert.Equal(first.InputHash, second.InputHash);
        Assert.Empty(first.Suggestions);
    }

    [Fact]
    public async Task OpenAiService_Disabled_DoesNotCallProvider()
    {
        var handler = new StubHttpMessageHandler("{}");
        var service = CreateOpenAiService(
            handler,
            new OpenAiOptions
            {
                Endpoint = "https://example.openai.azure.com",
                ApiKey = "test-key",
                Deployment = "test-deployment",
                ApiVersion = "2024-10-21"
            },
            new BankReconciliationAiSuggestionOptions
            {
                Enabled = false,
                PromptVersion = "test-v1"
            });

        var result = await service.BuildSuggestionsAsync(CreateRequest());

        Assert.False(result.Enabled);
        Assert.Equal("disabled", result.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task OpenAiService_EnabledWithoutProviderConfiguration_ReturnsProviderMissing()
    {
        var handler = new StubHttpMessageHandler("{}");
        var service = CreateOpenAiService(
            handler,
            new OpenAiOptions(),
            new BankReconciliationAiSuggestionOptions
            {
                Enabled = true,
                PromptVersion = "test-v1"
            });

        var result = await service.BuildSuggestionsAsync(CreateRequest());

        Assert.False(result.Enabled);
        Assert.Equal("provider-not-configured", result.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task OpenAiService_VerifiesProviderSuggestionAgainstRuleCandidates()
    {
        var responseContent = "{\"suggestions\":[{\"invoiceId\":\"1001\",\"matchedAmount\":100,\"currency\":\"SEK\",\"confidenceScore\":82,\"reasonCode\":\"reference-amount\",\"explanation\":\"Referens och belopp stödjer matchningen.\",\"requiresManualConfirmation\":true}]}";
        var handler = new StubHttpMessageHandler(CreateChatResponse(responseContent));
        var service = CreateOpenAiService(
            handler,
            CreateConfiguredOpenAiOptions(),
            new BankReconciliationAiSuggestionOptions
            {
                Enabled = true,
                PromptVersion = "test-v1",
                MaxCandidates = 4
            });

        var result = await service.BuildSuggestionsAsync(CreateRequest());

        Assert.True(result.Enabled);
        Assert.Equal("verified", result.Status);
        Assert.Single(result.Suggestions);
        Assert.Equal("1001", result.Suggestions[0].InvoiceId);
        Assert.Equal("verified", result.Suggestions[0].VerificationStatus);
        Assert.Equal(1, handler.Calls);
        Assert.Contains("requiresManualConfirmation", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("debtorName", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("Nordic Servicebolaget AB", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("customerName", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiService_RejectsProviderSuggestionOutsideRuleCandidates()
    {
        var responseContent = "{\"suggestions\":[{\"invoiceId\":\"9999\",\"matchedAmount\":100,\"currency\":\"SEK\",\"confidenceScore\":82,\"reasonCode\":\"unknown\",\"explanation\":\"Felaktigt förslag.\",\"requiresManualConfirmation\":true}]}";
        var handler = new StubHttpMessageHandler(CreateChatResponse(responseContent));
        var service = CreateOpenAiService(
            handler,
            CreateConfiguredOpenAiOptions(),
            new BankReconciliationAiSuggestionOptions
            {
                Enabled = true,
                PromptVersion = "test-v1"
            });

        var result = await service.BuildSuggestionsAsync(CreateRequest());

        Assert.True(result.Enabled);
        Assert.Equal("no-verified-suggestions", result.Status);
        Assert.Empty(result.Suggestions);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task OpenAiService_BlockedTransaction_DoesNotCallProvider()
    {
        var handler = new StubHttpMessageHandler("{}");
        var service = CreateOpenAiService(
            handler,
            CreateConfiguredOpenAiOptions(),
            new BankReconciliationAiSuggestionOptions
            {
                Enabled = true,
                PromptVersion = "test-v1"
            });
        var request = CreateRequest();
        request.Transaction.EntryStatus = "PDNG";

        var result = await service.BuildSuggestionsAsync(request);

        Assert.True(result.Enabled);
        Assert.Equal("no-eligible-rule-candidates", result.Status);
        Assert.Empty(result.Suggestions);
        Assert.Equal(0, handler.Calls);
    }

    private static BankReconciliationAiSuggestionRequest CreateRequest(bool ruleRequiresManualConfirmation = false)
        => new()
        {
            CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            StateKey = "session.xml",
            RequestedByUserId = "user-1",
            Transaction = new BankReconciliationTransactionCandidate
            {
                TransactionId = "TX-1",
                EntryStatus = "BOOK",
                Direction = "CRDT",
                Date = "2026-05-12",
                Amount = 100m,
                Currency = "SEK",
                ReferenceCandidates = new List<string> { "462166596" },
                DebtorName = "Nordic Servicebolaget AB",
                Remittance = "Serviceavtal april"
            },
            RuleCandidates = new List<BankReconciliationRecommendationItem>
            {
                new()
                {
                    Invoice = new BankReconciliationRecommendationInvoice
                    {
                        Id = "1001",
                        InvoiceNo = "1001",
                        Ocr = "462166596",
                        Amount = 100m,
                        RemainingAmount = 100m,
                        Currency = "SEK",
                        CustomerName = "Nordic Servicebolaget AB",
                        DueDate = "2026-05-12"
                    },
                    Confidence = new BankReconciliationConfidence
                    {
                        Level = "Hög",
                        Score = 95
                    },
                    RuleKey = "ref-exact|amount-exact",
                    RequiresManualConfirmation = ruleRequiresManualConfirmation,
                    Evidence = new BankReconciliationMatchEvidence
                    {
                        MatchedNameTokens = new List<string> { "NORDIC", "SERVICEBOLAGET" }
                    }
                }
            }
        };

    private static BankReconciliationAiSuggestionCandidate ValidCandidate()
        => new()
        {
            InvoiceId = "1001",
            MatchedAmount = 100m,
            Currency = "SEK",
            ConfidenceScore = 85,
            RequiresManualConfirmation = true
        };

    private static OpenAiBankReconciliationSuggestionService CreateOpenAiService(
        HttpMessageHandler handler,
        OpenAiOptions openAiOptions,
        BankReconciliationAiSuggestionOptions options)
        => new(
            new StubHttpClientFactory(handler),
            Options.Create(openAiOptions),
            Options.Create(options),
            new BankReconciliationAiSuggestionVerifier());

    private static OpenAiOptions CreateConfiguredOpenAiOptions()
        => new()
        {
            Endpoint = "https://example.openai.azure.com",
            ApiKey = "test-key",
            Deployment = "test-deployment",
            ApiVersion = "2024-10-21"
        };

    private static string CreateChatResponse(string content)
        => $$"""
             {
               "choices": [
                 {
                   "message": {
                     "content": {{JsonSerializer.Serialize(content)}}
                   }
                 }
               ]
             }
             """;

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler);
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;

        public StubHttpMessageHandler(string responseContent)
        {
            _responseContent = responseContent;
        }

        public int Calls { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
        }
    }
}
