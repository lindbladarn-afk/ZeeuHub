using System;
using Microsoft.Extensions.Configuration;

namespace WebApp.Services.Application;

// Centralizes the default Jeeves fallback order so repos and services do not duplicate config logic.
public sealed class JeevesConnectionResolver : IJeevesConnectionResolver
{
    private readonly IConfiguration _configuration;

    public JeevesConnectionResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ResolveConnectionString()
    {
        var connStr = _configuration.GetConnectionString("Jeeves")
                      ?? _configuration["CONNECTION_STRING_JEEVES"]
                      ?? _configuration["ZEEU_CONNECTION_STRING_LOCALDEVELOPMENT"];

        if (string.IsNullOrWhiteSpace(connStr))
        {
            throw new InvalidOperationException(
                "Ingen default-connection string hittades för Jeeves. Kontrollera ConnectionStrings:Jeeves, CONNECTION_STRING_JEEVES eller ZEEU_CONNECTION_STRING_LOCALDEVELOPMENT.");
        }

        return connStr;
    }
}
