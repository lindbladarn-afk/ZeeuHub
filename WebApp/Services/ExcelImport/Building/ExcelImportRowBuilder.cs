using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace WebApp.Services.ExcelImport;

public static class ExcelImportRowBuilder
{
    public static (List<string> HeaderList, Dictionary<string, int> HeaderMap) BuildHeaders(IXLRow headerRow)
    {
        var headerCells = headerRow.Cells()
            .Where(c => !string.IsNullOrWhiteSpace(c.GetString()))
            .OrderBy(c => c.Address.ColumnNumber)
            .ToList();

        var headerList = headerCells
            .Select(c => c.GetString().Trim())
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();

        var headerMap = headerCells
            .Select(c => new { Name = c.GetString().Trim(), Col = c.Address.ColumnNumber })
            .Where(h => !string.IsNullOrWhiteSpace(h.Name))
            .ToDictionary(h => h.Name, h => h.Col, StringComparer.OrdinalIgnoreCase);

        return (headerList, headerMap);
    }

    public static (List<string> HeaderList, Dictionary<string, int> HeaderMap) BuildHeaderDisplayMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var orderedKeys = new List<string>();
        var usedCells = headerRow.CellsUsed()
            .OrderBy(c => c.Address.ColumnNumber)
            .ToList();

        foreach (var cell in usedCells)
        {
            var header = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(header)) continue;

            var key = header;
            var suffix = 2;
            while (map.ContainsKey(key))
            {
                key = $"{header} ({suffix})";
                suffix++;
            }

            map[key] = cell.Address.ColumnNumber;
            orderedKeys.Add(key);
        }

        return (orderedKeys, map);
    }

    public static Dictionary<string, string> BuildRowDictionary(IXLRow row, IReadOnlyDictionary<string, int> headerMap)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in headerMap)
        {
            var val = row.Cell(h.Value).GetString();
            if (!string.IsNullOrWhiteSpace(val))
            {
                dict[h.Key] = val;
            }
        }

        return dict;
    }

    public static Dictionary<string, string> BuildRowData(IXLRow row, IReadOnlyDictionary<string, int> headerMap, IReadOnlyList<string> headerList)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headerList)
        {
            if (!headerMap.TryGetValue(header, out var col))
            {
                data[header] = string.Empty;
                continue;
            }

            var value = row.Cell(col).GetFormattedString().Trim();
            data[header] = value;
        }

        return data;
    }
}
