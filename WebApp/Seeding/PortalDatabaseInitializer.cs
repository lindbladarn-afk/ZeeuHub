// Initializes the portal schema safely when a development database starts empty.
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using WebApp.Data;

namespace WebApp.Seeding;

public static class PortalDatabaseInitializer
{
    private const string MigrationHistoryTableName = "__EFMigrationsHistory";

    public static async Task InitializeAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Database.IsRelational())
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        if (context.Database.GetMigrations().Any())
        {
            await context.Database.MigrateAsync(cancellationToken);
            await BankReconciliationSchemaInitializer.EnsureCreatedAsync(context, cancellationToken);
            return;
        }

        var databaseWasCreated = await context.Database.EnsureCreatedAsync(cancellationToken);
        if (!context.Database.IsSqlServer())
        {
            return;
        }

        if (!databaseWasCreated)
        {
            var applicationTableCount = await CountApplicationTablesAsync(context, cancellationToken);
            if (ShouldCreateTables(databaseWasCreated, applicationTableCount))
            {
                var databaseCreator = context.GetService<IRelationalDatabaseCreator>();
                await databaseCreator.CreateTablesAsync(cancellationToken);
            }
        }

        await BankReconciliationSchemaInitializer.EnsureCreatedAsync(context, cancellationToken);
    }

    internal static bool ShouldCreateTables(bool databaseWasCreated, long applicationTableCount)
        => !databaseWasCreated && applicationTableCount == 0;

    private static async Task<long> CountApplicationTablesAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT COUNT_BIG(*) FROM sys.tables WHERE [name] <> N'{MigrationHistoryTableName}';";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? 0 : Convert.ToInt64(result);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
