// Creates the bank reconciliation tables in portal databases that predate EF migrations.
using Microsoft.EntityFrameworkCore;
using WebApp.Data;

namespace WebApp.Seeding;

public static class BankReconciliationSchemaInitializer
{
    public static async Task EnsureCreatedAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
IF SCHEMA_ID(N'Identity') IS NULL EXEC(N'CREATE SCHEMA [Identity]');

IF OBJECT_ID(N'[Identity].[BankReconciliationStates]', N'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[BankReconciliationStates] (
        [CompanyId] uniqueidentifier NOT NULL,
        [StateKeyHash] nvarchar(64) NOT NULL,
        [Version] int NOT NULL,
        [StateJson] nvarchar(max) NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_BankReconciliationStates] PRIMARY KEY ([CompanyId], [StateKeyHash])
    );
    CREATE INDEX [IX_BankReconciliationStates_UpdatedAtUtc]
        ON [Identity].[BankReconciliationStates] ([UpdatedAtUtc]);
END;

IF OBJECT_ID(N'[Identity].[BankReconciliationImportRegistries]', N'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[BankReconciliationImportRegistries] (
        [CompanyId] uniqueidentifier NOT NULL,
        [AccountFingerprint] nvarchar(64) NOT NULL,
        [Version] int NOT NULL,
        [RegistryJson] nvarchar(max) NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_BankReconciliationImportRegistries] PRIMARY KEY ([CompanyId], [AccountFingerprint])
    );
    CREATE INDEX [IX_BankReconciliationImportRegistries_UpdatedAtUtc]
        ON [Identity].[BankReconciliationImportRegistries] ([UpdatedAtUtc]);
END;

IF OBJECT_ID(N'[Identity].[BankReconciliationCodingRules]', N'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[BankReconciliationCodingRules] (
        [CompanyId] uniqueidentifier NOT NULL,
        [BankAccountKeyHash] nvarchar(64) NOT NULL,
        [Version] int NOT NULL,
        [RuleSetJson] nvarchar(max) NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_BankReconciliationCodingRules] PRIMARY KEY ([CompanyId], [BankAccountKeyHash])
    );
    CREATE INDEX [IX_BankReconciliationCodingRules_UpdatedAtUtc]
        ON [Identity].[BankReconciliationCodingRules] ([UpdatedAtUtc]);
END;
""";

        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
