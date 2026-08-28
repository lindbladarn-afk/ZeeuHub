using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Vouchers
{
    // Defines the accepted voucher import layout and row-level checks before staging.
    public static class VoucherValidation
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
            "Debit",
            "Credit",
            "VAT code",
            "Voucher text",
            "Allocation"
        };

        // Canonical header -> accepted incoming labels in files (case-insensitive compare used below).
        private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Account"] = new[] { "Account", "Konto" },
            ["Cost center"] = new[] { "Cost center", "Koststalle", "Kostställe" },
            ["Cost unit"] = new[] { "Cost unit", "Kostbar" },
            ["K4"] = new[] { "K4" },
            ["K5"] = new[] { "K5" },
            ["K6"] = new[] { "K6" },
            ["K7"] = new[] { "K7" },
            ["Project"] = new[] { "Project", "Projekt" },
            ["Debit"] = new[] { "Debit", "Debbel" },
            ["Credit"] = new[] { "Credit", "Krebel" },
            ["VAT code"] = new[] { "VAT code", "VAT Code", "Momskod" },
            ["Voucher text"] = new[] { "Voucher text", "Voucher Text", "Verifikationstext" },
            ["Allocation"] = new[] { "Allocation", "Posting Template", "Konteringsmall", "Autoregel" }
        };

        public static ExcelImportWorkbookDefinition WorkbookDefinition { get; } = new()
        {
            ExpectedHeaders = ExpectedHeaders,
            ValidateHeaders = ValidateHeaderValues,
            BuildRowData = BuildRowData,
            HasAnyValue = HasAnyValue
        };

        public static bool ValidateHeaders(ClosedXML.Excel.IXLRow headerRow, List<string> errors)
        {
            var actualHeaders = headerRow.Cells(1, Math.Max(ExpectedHeaders.Length, headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0))
                .Select(cell => cell.GetString())
                .ToList();
            return ValidateHeaderValues(actualHeaders, errors);
        }

        public static bool ValidateHeaderValues(IReadOnlyList<string> actualHeaders, List<string> errors)
        {
            for (var i = 0; i < ExpectedHeaders.Length; i++)
            {
                var cellValue = i < actualHeaders.Count
                    ? actualHeaders[i].Trim()
                    : string.Empty;
                var expected = ExpectedHeaders[i];
                var accepted = HeaderAliases.TryGetValue(expected, out var aliases)
                    ? aliases
                    : new[] { expected };
                var isMatch = accepted.Any(x => string.Equals(cellValue, x, StringComparison.OrdinalIgnoreCase));
                if (!isMatch)
                {
                    errors.Add($"Fel rubrik i kolumn {i + 1}. Förväntat '{expected}' (alt: {string.Join(", ", accepted)}), fick '{cellValue}'.");
                    return false;
                }
            }

            var extraHeaders = actualHeaders
                .Skip(ExpectedHeaders.Length)
                .Select(header => header.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (extraHeaders.Any())
            {
                errors.Add($"Filen innehåller fler kolumnrubriker än mallen tillåter: {string.Join(", ", extraHeaders)}.");
                return false;
            }

            return true;
        }

        public static Dictionary<string, string> BuildRowData(
            IReadOnlyList<string> cells,
            IReadOnlyDictionary<string, int> headers)
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in ExpectedHeaders)
            {
                if (!headers.TryGetValue(header, out var columnNumber))
                {
                    data[header] = string.Empty;
                    continue;
                }

                var index = columnNumber - 1;
                var raw = index >= 0 && index < cells.Count
                    ? cells[index]
                    : string.Empty;
                data[header] = Normalize(header, raw);
            }

            return data;
        }

        public static Dictionary<string, string> BuildRowData(ClosedXML.Excel.IXLRow row, IReadOnlyDictionary<string, int> headers)
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in ExpectedHeaders)
            {
                if (!headers.TryGetValue(header, out var col))
                {
                    data[header] = string.Empty;
                    continue;
                }

                var raw = row.Cell(col).GetFormattedString();
                var value = Normalize(header, raw);
                data[header] = value;
            }

            return data;
        }

        public static IEnumerable<string> ValidateRowData(IReadOnlyDictionary<string, string> rowData)
        {
            var errors = new List<string>();

            string Get(string header) => rowData.TryGetValue(header, out var value)
                ? value?.Trim() ?? string.Empty
                : string.Empty;

            var account = Get("Account");
            if (string.IsNullOrWhiteSpace(account))
                errors.Add("Account saknas.");

            var debit = Get("Debit");
            var credit = Get("Credit");
            ValidateDecimal("Debit", debit, errors);
            ValidateDecimal("Credit", credit, errors);

            if (string.IsNullOrWhiteSpace(debit) && string.IsNullOrWhiteSpace(credit))
                errors.Add("Debit eller Credit måste anges.");

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
                if (!string.IsNullOrWhiteSpace(value) && !IsZeroLike(value))
                    return true;
            }

            return false;
        }

        public static string Normalize(string key, string value)
        {
            var s = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(s)) return string.Empty;

            if (string.Equals(key, "Account", StringComparison.OrdinalIgnoreCase))
            {
                if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var num)
                    && num == Math.Truncate(num))
                {
                    s = ((long)num).ToString(CultureInfo.InvariantCulture);
                }
            }

            return s;
        }

        private static void ValidateDecimal(string label, string value, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!TryParseDecimal(value, out _))
            {
                errors.Add($"{label} måste vara ett tal.");
            }
        }

        private static bool TryParseDecimal(string input, out decimal value)
        {
            var trimmed = input?.Trim() ?? string.Empty;
            return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.GetCultureInfo("sv-SE"), out value)
                   || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsZeroLike(string value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed)) return true;
            if (TryParseDecimal(trimmed, out var number))
            {
                return number == 0m;
            }
            return false;
        }

        private static int GetScale(decimal value)
        {
            var bits = decimal.GetBits(value);
            return (bits[3] >> 16) & 0xFF;
        }
    }
}
