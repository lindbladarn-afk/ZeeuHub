namespace WebApp.Services.ExcelImport;

// Resolves edit-session adapters by import type so orchestration does not depend on module-specific services.
public sealed class ExcelImportEditSessionAdapterResolver
{
    private readonly IReadOnlyDictionary<string, IExcelImportEditSessionAdapter> _adapters;

    public ExcelImportEditSessionAdapterResolver(IEnumerable<IExcelImportEditSessionAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(
            adapter => adapter.ImportType,
            adapter => adapter,
            StringComparer.OrdinalIgnoreCase);
    }

    public IExcelImportEditSessionAdapter? Find(string? importType)
    {
        if (string.IsNullOrWhiteSpace(importType))
            return null;

        return _adapters.TryGetValue(importType.Trim(), out var adapter) ? adapter : null;
    }

    public IExcelImportEditSessionAdapter GetRequired(string importType)
    {
        return Find(importType)
            ?? throw new InvalidOperationException($"Excel import edit-session adapter saknas för '{importType}'.");
    }
}
