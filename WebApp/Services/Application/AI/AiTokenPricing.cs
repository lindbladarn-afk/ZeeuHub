using System;

namespace WebApp.Services.Application.AI;

public static class AiTokenPricing
{
    // GPT-4.1 Global (standard) från kundens prisunderlag, SEK per 1M tokens.
    public const decimal StandardInputSekPer1M = 18.06m;
    public const decimal StandardOutputSekPer1M = 72.24m;

    public static decimal? CalculateInputCostSek(int? promptTokens)
        => CalculateInputCostSek(promptTokens.HasValue ? (long?)promptTokens.Value : null);

    public static decimal? CalculateOutputCostSek(int? completionTokens)
        => CalculateOutputCostSek(completionTokens.HasValue ? (long?)completionTokens.Value : null);

    public static decimal? CalculateTotalCostSek(int? promptTokens, int? completionTokens, int? totalTokens = null)
        => CalculateTotalCostSek(
            promptTokens.HasValue ? (long?)promptTokens.Value : null,
            completionTokens.HasValue ? (long?)completionTokens.Value : null,
            totalTokens.HasValue ? (long?)totalTokens.Value : null);

    public static decimal? CalculateInputCostSek(long? promptTokens)
    {
        if (!promptTokens.HasValue || promptTokens.Value < 0)
            return null;

        return RoundSek((promptTokens.Value / 1_000_000m) * StandardInputSekPer1M);
    }

    public static decimal? CalculateOutputCostSek(long? completionTokens)
    {
        if (!completionTokens.HasValue || completionTokens.Value < 0)
            return null;

        return RoundSek((completionTokens.Value / 1_000_000m) * StandardOutputSekPer1M);
    }

    public static decimal? CalculateTotalCostSek(long? promptTokens, long? completionTokens, long? totalTokens = null)
    {
        var input = CalculateInputCostSek(promptTokens);
        var output = CalculateOutputCostSek(completionTokens);

        if (!input.HasValue && !output.HasValue)
        {
            // Backward compatibility för äldre loggrader som bara har TotalTokens.
            if (totalTokens.HasValue && totalTokens.Value >= 0)
                return RoundSek((totalTokens.Value / 1_000_000m) * StandardInputSekPer1M);

            return null;
        }

        return RoundSek((input ?? 0m) + (output ?? 0m));
    }

    private static decimal RoundSek(decimal amount)
        => Math.Round(amount, 4, MidpointRounding.AwayFromZero);
}

