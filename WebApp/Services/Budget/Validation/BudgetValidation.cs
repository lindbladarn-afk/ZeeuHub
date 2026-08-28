using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Budget
{
    // Validates budget workbook headers and row values before staging.
    public static class BudgetValidation
    {
        public static readonly string[] ExpectedHeaders = new[]
        {
            "Account",
            "Cost center",
            "Cost unit",
            "K4",
            "K5",
            "K6",
            "K7",
            "Project",
            "Period",
            "Distribution curve",
            "Amount"
        };

        public static ExcelImportWorkbookDefinition WorkbookDefinition => new()
        {
            ExpectedHeaders = ExpectedHeaders,
            ValidateHeaders = ValidateHeaders,
            BuildRowData = BuildRowData,
            HasAnyValue = HasAnyValue
        };

        public static bool ValidateHeaders(IReadOnlyList<string> headerRow, List<string> errors)
            => ExcelImportWorkbookParserHelpers.ValidateFixedHeaders(headerRow, ExpectedHeaders, errors);

        public static Dictionary<string, string> BuildRowData(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> headers)
            => ExcelImportWorkbookParserHelpers.BuildFixedRowData(row, headers, ExpectedHeaders);

        public static IEnumerable<string> ValidateRowData(IReadOnlyDictionary<string, string> rowData)
        {
            var errors = new List<string>();

            string Get(string header) => rowData.TryGetValue(header, out var value)
                ? value?.Trim() ?? string.Empty
                : string.Empty;

            var account = Get("Account");
            var amount = Get("Amount");
            var period = Get("Period");

            if (string.IsNullOrWhiteSpace(account))
                errors.Add("Account saknas.");

            if (string.IsNullOrWhiteSpace(amount))
                errors.Add("Amount saknas.");
            else if (!TryParseDecimal(amount, out _))
                errors.Add("Amount måste vara ett tal.");

            if (!string.IsNullOrWhiteSpace(period))
            {
                if (!int.TryParse(period, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) || p < 1 || p > 12)
                    errors.Add("Period måste vara ett heltal mellan 1 och 12.");
            }

            return errors;
        }

        public static string BuildRowSnapshot(IReadOnlyDictionary<string, string> rowData)
        {
            var parts = new List<string>(ExpectedHeaders.Length);
            foreach (var header in ExpectedHeaders)
            {
                if (!rowData.TryGetValue(header, out var value))
                {
                    parts.Add($"{header}=<saknas>");
                    continue;
                }

                var trimmed = value?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    trimmed = "<tomt>";
                }

                parts.Add($"{header}={trimmed}");
            }

            return string.Join("; ", parts);
        }

        public static bool HasAnyValue(IReadOnlyDictionary<string, string> rowData)
        {
            foreach (var value in rowData.Values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return true;
            }

            return false;
        }

        private static bool TryParseDecimal(string input, out decimal value)
        {
            var trimmed = input?.Trim() ?? string.Empty;
            return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.GetCultureInfo("sv-SE"), out value)
                   || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private static int GetScale(decimal value)
        {
            var bits = decimal.GetBits(value);
            return (bits[3] >> 16) & 0xFF;
        }
    }
}
