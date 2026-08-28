using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PurchasePrice
{
    // Validates purchase price workbook headers and row values before staging.
    public static class PurchasePriceValidation
    {
        public static readonly string[] ExpectedHeaders = new[]
        {
            "Företagkod",
            "Lev Ftgnr",
            "ArtNr",
            "Inpris brutto valuta",
            "Rabatt %",
            "Hemtagn. %",
            "Fraktkost %",
            "Kommentar"
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

            var artNr = Get("ArtNr");
            var inpris = Get("Inpris brutto valuta");
            var rabatt = Get("Rabatt %");
            var hemtagn = Get("Hemtagn. %");
            var frakt = Get("Fraktkost %");

            if (string.IsNullOrWhiteSpace(artNr))
                errors.Add("ArtNr saknas.");

            if (string.IsNullOrWhiteSpace(inpris))
                errors.Add("Inpris brutto valuta saknas.");
            else if (!TryParseDecimal(inpris, out var inprisDec) || inprisDec < 0)
                errors.Add("Inpris brutto valuta måste vara ett tal >= 0.");

            ValidatePercent("Rabatt %", rabatt, errors);
            ValidatePercent("Hemtagn. %", hemtagn, errors);
            ValidatePercent("Fraktkost %", frakt, errors);

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

        private static void ValidatePercent(string label, string value, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!TryParseDecimal(value, out var dec) || dec < 0 || dec > 100)
            {
                errors.Add($"{label} måste vara ett tal mellan 0 och 100.");
            }
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
