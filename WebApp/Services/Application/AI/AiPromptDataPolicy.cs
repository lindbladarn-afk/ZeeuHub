// Masks common personal, credential, and banking fields before model summarization.
using System.Globalization;
using System.Text.RegularExpressions;

namespace WebApp.Services.Application.AI;

public sealed class AiPromptDataPolicy : IAiPromptDataPolicy
{
    private static readonly Regex SensitiveColumn = new(
        @"(?ix)(email|e_mail|mailaddress|phone|telefon|mobile|mobil|personnummer|personalnumber|socialsecurity|address|adress|street|gata|postal|postnummer|iban|bankaccount|bankkonto|accountnumber|password|passwd|secret|token|apikey)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string FormatCell(string columnName, object? value)
    {
        if (SensitiveColumn.IsMatch(columnName ?? string.Empty))
            return "[MASKERAT]";

        return value switch
        {
            null => "NULL",
            DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "NULL"
        };
    }
}
