using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PriceUpdate
{
    // Validates price update workbook headers and row values before staging.
    public static class PriceUpdateValidation
    {
        public static readonly string[] ExpectedHeaders = new[]
        {
            "Artnr",
            "Pris",
            "Antalgräns",
            "Nettopris ej rabatt (1/0)",
            "Rabatt %",
            "Matrisrabatt (1/0)",
            "Selekt för prislista",
            "Valutakod",
            "Nytt pris datum",
            "Nytt pris"
        };

        public static ExcelImportWorkbookDefinition WorkbookDefinition => new()
        {
            ExpectedHeaders = ExpectedHeaders,
            ValidateHeaders = ValidateHeaders,
            BuildRowData = (row, headers) => ExcelImportWorkbookParserHelpers.BuildFixedRowData(row, headers, ExpectedHeaders),
            HasAnyValue = HasAnyValue
        };

        public static IEnumerable<string> ValidateRowData(IReadOnlyDictionary<string, string> rowData)
        {
            var errors = new List<string>();

            string Get(string header) => rowData.TryGetValue(header, out var value)
                ? value?.Trim() ?? string.Empty
                : string.Empty;

            var artnr = Get("Artnr");
            var pris = Get("Pris");
            var antalGrans = Get("Antalgräns");
            var nettoprisEjRabatt = Get("Nettopris ej rabatt (1/0)");
            var rabatt = Get("Rabatt %");
            var matrisrabatt = Get("Matrisrabatt (1/0)");
            var valutakod = Get("Valutakod");
            var nyttPrisDatum = Get("Nytt pris datum");
            var nyttPris = Get("Nytt pris");

            if (string.IsNullOrWhiteSpace(artnr))
                errors.Add("Artikelnummer saknas.");

            if (string.IsNullOrWhiteSpace(pris))
                errors.Add("Pris saknas.");
            else if (!TryParseDecimal(pris, out _))
                errors.Add("Pris måste vara ett tal.");

            if (!string.IsNullOrWhiteSpace(antalGrans))
            {
                if (!TryParseDecimal(antalGrans, out var antalDec) || antalDec < 0)
                    errors.Add("Antalgräns måste vara ett tal >= 0.");
            }

            if (!string.IsNullOrWhiteSpace(nettoprisEjRabatt) && !IsZeroOrOne(nettoprisEjRabatt))
                errors.Add("Nettopris ej rabatt (1/0) måste vara 0 eller 1.");

            if (!string.IsNullOrWhiteSpace(matrisrabatt) && !IsZeroOrOne(matrisrabatt))
                errors.Add("Matrisrabatt (1/0) måste vara 0 eller 1.");

            if (!string.IsNullOrWhiteSpace(rabatt))
            {
                if (!TryParseDecimal(rabatt, out var rabattDec) || rabattDec < 0 || rabattDec > 100)
                    errors.Add("Rabatt % måste vara ett tal mellan 0 och 100.");
            }

            if (!string.IsNullOrWhiteSpace(nyttPris) && !TryParseDecimal(nyttPris, out _))
                errors.Add("Nytt pris måste vara ett tal.");

            if (!string.IsNullOrWhiteSpace(nyttPrisDatum) && !TryParseSwedishExcelDate(nyttPrisDatum))
                errors.Add("Nytt pris datum måste vara ett giltigt svenskt Excel-datum.");

            if (!string.IsNullOrWhiteSpace(valutakod) && valutakod.Length != 3)
                errors.Add("Valutakod måste vara tom eller 3 bokstäver.");

            return errors;
        }

        public static bool ValidateHeaders(IReadOnlyList<string> headerRow, List<string> errors)
            => ExcelImportWorkbookParserHelpers.ValidateFixedHeaders(headerRow, ExpectedHeaders, errors);

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

        public static bool TryParseDecimal(string input, out decimal value)
        {
            var trimmed = input?.Trim() ?? string.Empty;
            return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.GetCultureInfo("sv-SE"), out value)
                   || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        public static bool IsZeroOrOne(string input)
        {
            if (!TryParseDecimal(input, out var value))
                return false;
            return value == 0m || value == 1m;
        }

        public static int GetScale(decimal value)
        {
            var bits = decimal.GetBits(value);
            return (bits[3] >> 16) & 0xFF;
        }

        public static bool TryParseSwedishExcelDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return true;

            if (double.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var oaDate))
            {
                try
                {
                    _ = DateTime.FromOADate(oaDate);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return DateTime.TryParse(raw, CultureInfo.GetCultureInfo("sv-SE"), DateTimeStyles.None, out _);
        }
    }
}
