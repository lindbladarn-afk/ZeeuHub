using System;
using Microsoft.Extensions.Configuration;

namespace WebApp.Services.ExcelImport;

// Preserves the current fallback order while centralizing the decision in one place.
public sealed class ExcelImportConnectionResolver : IExcelImportConnectionResolver
{
    private readonly IConfiguration _configuration;

    public ExcelImportConnectionResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ResolveConnectionString()
    {
        var connStr = _configuration["CONNECTION_STRING_EXCELIMPORT"]
                      ?? _configuration.GetConnectionString("Jeeves")
                      ?? _configuration["CONNECTION_STRING_JEEVES"];

        if (string.IsNullOrWhiteSpace(connStr))
        {
            throw new InvalidOperationException(
                "Ingen connection string hittades för Excelimport. Kontrollera CONNECTION_STRING_EXCELIMPORT, ConnectionStrings:Jeeves eller CONNECTION_STRING_JEEVES.");
        }

        return connStr;
    }
}
