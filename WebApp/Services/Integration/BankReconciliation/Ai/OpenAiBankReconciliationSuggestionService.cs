using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.BankReconciliation;

// OpenAI-backed bank reconciliation suggestions use minimized rule evidence and deterministic verification.
public sealed class OpenAiBankReconciliationSuggestionService : IBankReconciliationAiSuggestionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAiOptions _openAiOptions;
    private readonly BankReconciliationAiSuggestionOptions _options;
    private readonly IBankReconciliationAiSuggestionVerifier _verifier;

    public OpenAiBankReconciliationSuggestionService(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAiOptions> openAiOptions,
        IOptions<BankReconciliationAiSuggestionOptions> options,
        IBankReconciliationAiSuggestionVerifier verifier)
    {
        _httpClientFactory = httpClientFactory;
        _openAiOptions = openAiOptions.Value;
        _options = options.Value;
        _verifier = verifier;
    }

    public async Task<BankReconciliationAiSuggestionResult> BuildSuggestionsAsync(
        BankReconciliationAiSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var inputHash = BankReconciliationAiSuggestionInputHasher.BuildInputHash(request);
        if (!_options.Enabled)
        {
            return Disabled("disabled", "AI-förslag är avstängt. Regelmotorns kandidater används tills AI aktiveras.", inputHash);
        }

        if (!HasProviderConfiguration())
        {
            return Disabled("provider-not-configured", "AI är aktiverat men Azure OpenAI-konfiguration saknas.", inputHash);
        }

        var ruleCandidates = request.RuleCandidates
            .Where(candidate => _verifier.EvaluateEligibility(request, candidate).IsEligible)
            .Take(Math.Clamp(_options.MaxCandidates, 1, 8))
            .ToList();
        if (ruleCandidates.Count == 0)
        {
            return new BankReconciliationAiSuggestionResult
            {
                Enabled = true,
                Status = request.RuleCandidates.Count == 0 ? "no-rule-candidates" : "no-eligible-rule-candidates",
                PromptVersion = _options.PromptVersion,
                InputHash = inputHash,
                Message = request.RuleCandidates.Count == 0
                    ? "AI kördes inte eftersom regelmotorn saknar kandidater."
                    : "AI kördes inte eftersom inga kandidater klarade matchningsreglerna.",
                Suggestions = new List<BankReconciliationAiSuggestionCandidate>()
            };
        }

        var minimizedRequest = new BankReconciliationAiSuggestionRequest
        {
            CompanyId = request.CompanyId,
            StateKey = request.StateKey,
            RequestedByUserId = request.RequestedByUserId,
            Transaction = request.Transaction,
            RuleCandidates = ruleCandidates
        };

        var rawJson = await CallAzureOpenAiAsync(minimizedRequest, cancellationToken);
        var parsedCandidates = ParseCandidates(rawJson);
        var verifiedCandidates = parsedCandidates
            .Select(candidate => _verifier.Verify(minimizedRequest, candidate))
            .Where(result => result.IsValid)
            .Select(result => result.Candidate)
            .ToList();

        return new BankReconciliationAiSuggestionResult
        {
            Enabled = true,
            Status = verifiedCandidates.Count > 0 ? "verified" : "no-verified-suggestions",
            PromptVersion = _options.PromptVersion,
            InputHash = inputHash,
            Message = verifiedCandidates.Count > 0
                ? "AI-förslag verifierades mot regelmotorns kandidater."
                : "AI returnerade inga förslag som klarade verifieringen.",
            Suggestions = verifiedCandidates
        };
    }

    private BankReconciliationAiSuggestionResult Disabled(string status, string message, string inputHash)
        => new()
        {
            Enabled = false,
            Status = status,
            PromptVersion = _options.PromptVersion,
            InputHash = inputHash,
            Message = message,
            Suggestions = new List<BankReconciliationAiSuggestionCandidate>()
        };

    private bool HasProviderConfiguration()
        => !string.IsNullOrWhiteSpace(_openAiOptions.Endpoint)
           && !string.IsNullOrWhiteSpace(_openAiOptions.ApiKey)
           && !string.IsNullOrWhiteSpace(_openAiOptions.Deployment)
           && !string.IsNullOrWhiteSpace(_openAiOptions.ApiVersion);

    private async Task<string> CallAzureOpenAiAsync(
        BankReconciliationAiSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("OpenAI");
        client.DefaultRequestHeaders.Remove("api-key");
        client.DefaultRequestHeaders.Add("api-key", _openAiOptions.ApiKey);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var endpoint = _openAiOptions.Endpoint.Trim().TrimEnd('/');
        var requestUri =
            $"{endpoint}/openai/deployments/{Uri.EscapeDataString(_openAiOptions.Deployment)}/chat/completions" +
            $"?api-version={Uri.EscapeDataString(_openAiOptions.ApiVersion)}";

        var payload = new
        {
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = BuildSystemPrompt()
                },
                new
                {
                    role = "user",
                    content = JsonSerializer.Serialize(BuildPromptPayload(request))
                }
            },
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens,
            response_format = new { type = "json_object" }
        };

        using var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(requestUri, body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure OpenAI error {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";
    }

    private static string BuildSystemPrompt()
        => """
           Du är en strikt bankavstämningsassistent för ZeeU.
           Du får bara välja bland fakturakandidaterna i input.
           Du får aldrig hitta på fakturor, belopp, kunder, referenser eller transaktioner.
           Jämför betalarens namn med kandidatens kundnamn när de finns i input.
           Du får aldrig föreslå automatisk bokning. requiresManualConfirmation måste vara true.
           Returnera bara JSON enligt formatet:
           {"suggestions":[{"invoiceId":"string","matchedAmount":0.0,"currency":"SEK","confidenceScore":0,"reasonCode":"string","explanation":"string","requiresManualConfirmation":true}]}
           Om underlaget är osäkert, returnera {"suggestions":[]}.
           """;

    private static object BuildPromptPayload(BankReconciliationAiSuggestionRequest request)
        => new
        {
            transaction = new
            {
                id = request.Transaction.TransactionId,
                amount = request.Transaction.Amount,
                currency = request.Transaction.Currency,
                bookingStatus = request.Transaction.EntryStatus,
                direction = request.Transaction.Direction,
                date = request.Transaction.Date,
                valueDate = request.Transaction.ValueDate,
                debtorName = request.Transaction.DebtorName,
                remittance = request.Transaction.Remittance,
                references = request.Transaction.ReferenceCandidates
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(NormalizeReference)
                    .Distinct(StringComparer.Ordinal)
                    .Take(12)
                    .ToArray()
            },
            candidates = request.RuleCandidates.Select(candidate => new
            {
                invoiceId = candidate.Invoice.Id,
                invoiceNo = candidate.Invoice.InvoiceNo,
                customerName = candidate.Invoice.CustomerName,
                amount = candidate.Invoice.Amount,
                remainingAmount = candidate.Invoice.RemainingAmount,
                currency = candidate.Invoice.Currency,
                isSupplierInvoice = candidate.Invoice.IsSupplierInvoice,
                confidenceScore = candidate.Confidence.Score,
                ruleKey = candidate.RuleKey,
                requiresManualConfirmation = candidate.RequiresManualConfirmation,
                evidence = new
                {
                    referenceMatches = candidate.Evidence.ReferenceMatches.Select(match => new
                    {
                        match.TransactionSource,
                        match.InvoiceSource,
                        match.NormalizedTransactionValue,
                        match.NormalizedInvoiceValue,
                        match.MatchType
                    }),
                    candidate.Evidence.AmountDifference,
                    candidate.Evidence.CurrencyMatched,
                    candidate.Evidence.MatchedNameTokens,
                    candidate.Evidence.DateDifferenceDays,
                    eligibilityRules = candidate.Evidence.EligibilityRules.Select(rule => new
                    {
                        rule.Code,
                        rule.Status
                    })
                }
            })
        };

    private static List<BankReconciliationAiSuggestionCandidate> ParseCandidates(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        if (!doc.RootElement.TryGetProperty("suggestions", out var suggestions) ||
            suggestions.ValueKind != JsonValueKind.Array)
        {
            return new List<BankReconciliationAiSuggestionCandidate>();
        }

        var result = new List<BankReconciliationAiSuggestionCandidate>();
        foreach (var item in suggestions.EnumerateArray())
        {
            result.Add(new BankReconciliationAiSuggestionCandidate
            {
                InvoiceId = ReadString(item, "invoiceId"),
                MatchedAmount = ReadDecimal(item, "matchedAmount"),
                Currency = ReadString(item, "currency", "SEK"),
                ConfidenceScore = ReadInt(item, "confidenceScore"),
                ReasonCode = ReadString(item, "reasonCode"),
                Explanation = ReadString(item, "explanation"),
                RequiresManualConfirmation = ReadBool(item, "requiresManualConfirmation", defaultValue: true)
            });
        }

        return result;
    }

    private static string ReadString(JsonElement item, string propertyName, string fallback = "")
        => item.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;

    private static decimal ReadDecimal(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var property))
            return 0m;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDecimal(out var value) => value,
            JsonValueKind.String when decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) => value,
            _ => 0m
        };
    }

    private static int ReadInt(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var property))
            return 0;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) => value,
            _ => 0
        };
    }

    private static bool ReadBool(JsonElement item, string propertyName, bool defaultValue)
    {
        if (!item.TryGetProperty(propertyName, out var property))
            return defaultValue;

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }

    private static string NormalizeReference(string value)
        => new string(value
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}
