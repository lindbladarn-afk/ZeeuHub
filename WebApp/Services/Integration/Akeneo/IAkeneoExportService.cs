namespace WebApp.Services.Integration.Akeneo
{
    public interface IAkeneoExportService
    {
        Task<AkeneoExportResult> ExportProductsXmlAsync(int limit, string? fileName, CancellationToken ct = default);
        Task<AkeneoExportResult> ExportProductsXmlAsync(IReadOnlyList<string> skus, int limit, string? fileName, CancellationToken ct = default);
    }
}
