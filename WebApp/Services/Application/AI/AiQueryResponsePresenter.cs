// Shapes safe errors and useful follow-up suggestions for Intelligence responses.
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

public static class AiQueryResponsePresenter
{
    public static AiQueryResponse Prepare(AiQueryResponse response, string? originalQuestion)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.Success)
        {
            response.Error ??= ClassifyError(response);
            response.Answer = response.Error.Message;
            response.Warning = null;
            response.Sql = null;
        }

        if (response.Suggestions.Count == 0)
        {
            response.Suggestions = BuildSuggestions(response, originalQuestion);
        }

        return response;
    }

    private static AiQueryError ClassifyError(AiQueryResponse response)
    {
        var diagnostic = string.Join(
            " ",
            response.ErrorMessage,
            response.Warning,
            response.Answer,
            response.QuotaStatus).ToLowerInvariant();

        if (response.QuotaNeedsDecision ||
            diagnostic.Contains("quota", StringComparison.Ordinal) ||
            diagnostic.Contains("kvot", StringComparison.Ordinal))
        {
            return Error(
                "quota_action_required",
                "AI-kvoten behöver hanteras",
                response.QuotaMessage ?? "Välj om AI ska fortsätta med kostnad eller pausas till nästa period.",
                canRetry: false,
                tone: "warning");
        }

        if (diagnostic.Contains("förtydlig", StringComparison.Ordinal) ||
            diagnostic.Contains("otydlig", StringComparison.Ordinal) ||
            diagnostic.Contains("menar du", StringComparison.Ordinal))
        {
            return Error(
                "clarification_required",
                "Förtydliga frågan",
                SafeConversationalMessage(response.Answer, "Jag behöver lite mer information för att kunna svara korrekt."),
                canRetry: false,
                tone: "info");
        }

        if (diagnostic.Contains("datakäll", StringComparison.Ordinal) ||
            diagnostic.Contains("anslutningssträng", StringComparison.Ordinal) ||
            diagnostic.Contains("aktivt bolag", StringComparison.Ordinal) ||
            diagnostic.Contains("bolagskontext", StringComparison.Ordinal))
        {
            return Error(
                "data_source_unavailable",
                "Datakällan är inte tillgänglig",
                "Kontrollera att rätt bolag och datakälla är valda och försök sedan igen.",
                canRetry: true,
                tone: "warning");
        }

        if (diagnostic.Contains("ingen data", StringComparison.Ordinal) ||
            diagnostic.Contains("0 rader", StringComparison.Ordinal))
        {
            return Error(
                "no_data",
                "Ingen matchande data",
                "Jag hittade ingen data som matchar frågan. Prova en bredare period eller färre filter.",
                canRetry: false,
                tone: "info");
        }

        if (diagnostic.Contains("kunde inte generera sql", StringComparison.Ordinal) ||
            diagnostic.Contains("kunde inte skapa en databasfråga", StringComparison.Ordinal))
        {
            return Error(
                "planning_failed",
                "Frågan kunde inte planeras",
                "Jag kunde inte skapa en databasfråga för den här formuleringen. Försök igen eller ange vilka uppgifter du vill visa.",
                canRetry: true,
                tone: "warning");
        }

        if (diagnostic.Contains("sql", StringComparison.Ordinal) ||
            diagnostic.Contains("schema", StringComparison.Ordinal) ||
            diagnostic.Contains("foretagkod", StringComparison.Ordinal))
        {
            return Error(
                "execution_failed",
                "Databasfrågan kunde inte köras",
                "Jag skapade en databasfråga, men databasen kunde inte köra den. Försök igen så kan ZeeU skapa en ny frågeplan.",
                canRetry: true,
                tone: "warning");
        }

        if (diagnostic.Contains("inte tillgänglig", StringComparison.Ordinal) ||
            diagnostic.Contains("åtkomst", StringComparison.Ordinal))
        {
            return Error(
                "feature_unavailable",
                "ZeeU Intelligence är inte tillgängligt",
                "Funktionen eller bolagsbehörigheten kunde inte verifieras.",
                canRetry: false,
                tone: "warning");
        }

        return Error(
            "unexpected",
            "Frågan kunde inte slutföras",
            "Ett oväntat fel uppstod. Försök igen om en stund.",
            canRetry: true,
            tone: "danger");
    }

    private static List<string> BuildSuggestions(AiQueryResponse response, string? originalQuestion)
    {
        if (!response.Success)
        {
            return response.Error?.Code switch
            {
                "clarification_required" =>
                [
                    "Visa antal sålda per månad i år",
                    "Visa omsättning per månad i år"
                ],
                "no_data" =>
                [
                    "Visa samma resultat för de senaste 12 månaderna",
                    "Visa resultatet utan extra filter"
                ],
                "planning_failed" => BuildQueryFailureSuggestions(originalQuestion),
                "execution_failed" => BuildQueryFailureSuggestions(originalQuestion),
                "query_failed" => BuildQueryFailureSuggestions(originalQuestion),
                _ => []
            };
        }

        if (response.Rows is null || response.Rows.Count == 0)
            return [];

        var suggestions = new List<string>();
        if (response.Truncated == true)
            suggestions.Add("Begränsa resultatet till de 20 största posterna");

        var metricLabel = response.Plan?.Metric switch
        {
            "net_revenue" => "omsättningen",
            "quantity_sold" => "antalet",
            "invoice_balance" => "fakturabeloppet",
            "order_value" => "ordervärdet",
            _ => "samma analys"
        };
        if (response.Plan is not null &&
            !response.Plan.Dimensions.Contains("month", StringComparer.OrdinalIgnoreCase))
        {
            suggestions.Add($"Bryt ned {metricLabel} per månad");
        }
        if (response.Plan is not null && string.IsNullOrWhiteSpace(response.Plan.Period))
            suggestions.Add("Visa samma analys för innevarande år");

        var question = (originalQuestion ?? string.Empty).ToLowerInvariant();
        if (response.Plan is null && !question.Contains("månad", StringComparison.Ordinal))
            suggestions.Add("Bryt ned samma analys per månad");
        if (!question.Contains("föregående", StringComparison.Ordinal))
            suggestions.Add("Jämför med föregående period");

        return suggestions.Take(3).ToList();
    }

    private static List<string> BuildQueryFailureSuggestions(string? originalQuestion)
    {
        var question = (originalQuestion ?? string.Empty).ToLowerInvariant();
        if (question.Contains("kund", StringComparison.Ordinal) ||
            question.Contains("customer", StringComparison.Ordinal))
        {
            return
            [
                "Visa alla kunder",
                "Visa kundnummer och kundnamn"
            ];
        }

        if (question.Contains("artikel", StringComparison.Ordinal) ||
            question.Contains("produkt", StringComparison.Ordinal) ||
            question.Contains("item", StringComparison.Ordinal))
        {
            return
            [
                "Visa alla artiklar",
                "Visa artikelnummer och artikelbeskrivning"
            ];
        }

        if (question.Contains("leverantör", StringComparison.Ordinal) ||
            question.Contains("supplier", StringComparison.Ordinal))
        {
            return
            [
                "Visa alla leverantörer",
                "Visa leverantörsnummer och leverantörsnamn"
            ];
        }

        return
        [
            "Visa omsättning per månad i år",
            "Visa de fem största kunderna i år"
        ];
    }

    private static AiQueryError Error(
        string code,
        string title,
        string message,
        bool canRetry,
        string tone) =>
        new()
        {
            Code = code,
            Title = title,
            Message = message,
            CanRetry = canRetry,
            Tone = tone
        };

    private static string SafeConversationalMessage(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var message = value.Trim();
        return message.Length <= 600 ? message : fallback;
    }
}
