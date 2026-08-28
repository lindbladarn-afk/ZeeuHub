using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using WebApp.Observability;
using WebApp.Services.Integration;

namespace WebApp.Services.ExcelImport;

// Runs explicit schema initialization for Excel import staging tables outside normal user requests.
public sealed class ExcelImportTableInitializationService : IExcelImportTableInitializationService
{
    private readonly IExcelImportConnectionResolver _connectionResolver;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ExcelImportTableInitializationService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly ExcelImportSchemaInitializationOptions _options;

    public ExcelImportTableInitializationService(
        IExcelImportConnectionResolver connectionResolver,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ExcelImportTableInitializationService> logger,
        IWebHostEnvironment environment,
        IOptions<ExcelImportSchemaInitializationOptions> options)
    {
        _connectionResolver = connectionResolver;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _environment = environment;
        _options = options.Value;
    }

    public async Task<ExcelImportTableInitializationResult> EnsureImportTablesAsync(CancellationToken cancellationToken)
    {
        if (_environment.IsProduction() && !_options.AllowRuntimeInitializationInProduction)
        {
            _logger.LogWarning(
                "Excel import runtime schema initialization was blocked in production. Deploy the database schema through the release process.");
            return new ExcelImportTableInitializationResult
            {
                Success = false,
                Items =
                [
                    new ExcelImportTableInitializationItem
                    {
                        TableName = "Excel Import schema",
                        Success = false,
                        Message = "Schemaändringar är spärrade i produktion och ska installeras via ordinarie releaseprocess."
                    }
                ]
            };
        }

        await using var conn = new SqlConnection(_connectionResolver.ResolveConnectionString());
        await conn.OpenAsync(cancellationToken);

        var items = new List<ExcelImportTableInitializationItem>();

        foreach (var script in Scripts)
        {
            try
            {
                var command = new CommandDefinition(
                    script.Sql,
                    commandTimeout: 120,
                    cancellationToken: cancellationToken);

                await conn.ExecuteAsync(command);

                items.Add(new ExcelImportTableInitializationItem
                {
                    TableName = script.TableName,
                    Success = true,
                    Message = "OK"
                });
            }
            catch (Exception ex)
            {
                var supportId = GetOrCreateSupportId();
                var diagnostic = IntegrationLogSanitizer.Diagnostic(ex.Message);

                _logger.LogWarning(
                    ex,
                    "Excel import table initialization failed. {ErrorCode} SupportId={SupportId} TableName={TableName}.",
                    PortalErrorCodes.DatabaseOperationFailed,
                    supportId,
                    script.TableName);

                items.Add(new ExcelImportTableInitializationItem
                {
                    TableName = script.TableName,
                    Success = false,
                    Message = $"{script.TableName}: {diagnostic} Referens: {supportId}."
                });
            }
        }

        var result = new ExcelImportTableInitializationResult
        {
            Success = items.All(x => x.Success),
            Items = items
        };

        _logger.LogInformation(
            "Excel import table initialization completed. Success={Success} FailedTables={FailedTables}.",
            items.All(x => x.Success),
            items.Count(item => !item.Success));

        return result;
    }

    private string GetOrCreateSupportId()
    {
        var supportId = _httpContextAccessor.HttpContext?.Items[PortalObservability.SupportIdItemKey]?.ToString();
        if (!string.IsNullOrWhiteSpace(supportId))
        {
            return supportId!;
        }

        supportId = Guid.NewGuid().ToString("N")[..8];
        if (_httpContextAccessor.HttpContext is { } httpContext)
        {
            httpContext.Items[PortalObservability.SupportIdItemKey] = supportId;
        }

        return supportId;
    }

    private static readonly (string TableName, string Sql)[] Scripts =
    {
        ("dbo.q_zu_StagingBudget", @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'q_zu_StagingBudget' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.q_zu_StagingBudget
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ImportBatchId UNIQUEIDENTIFIER NOT NULL,
        RowNo INT NOT NULL,
        RawJson NVARCHAR(MAX) NULL,
        ImportedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ImportedBy NVARCHAR(100) NULL,
        CompanyId UNIQUEIDENTIFIER NULL,
        ForetagKod INT NULL,
        UserId NVARCHAR(450) NULL
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CompanyId' AND Object_ID = Object_ID('dbo.q_zu_StagingBudget'))
        ALTER TABLE dbo.q_zu_StagingBudget ADD CompanyId UNIQUEIDENTIFIER NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ForetagKod' AND Object_ID = Object_ID('dbo.q_zu_StagingBudget'))
        ALTER TABLE dbo.q_zu_StagingBudget ADD ForetagKod INT NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'UserId' AND Object_ID = Object_ID('dbo.q_zu_StagingBudget'))
        ALTER TABLE dbo.q_zu_StagingBudget ADD UserId NVARCHAR(450) NULL;
END"),
            ("dbo.q_zu_StagingVoucher", @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'q_zu_StagingVoucher' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.q_zu_StagingVoucher (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Account NVARCHAR(50) NULL,
        Amount NVARCHAR(50) NULL,
        Autoregel NVARCHAR(50) NULL,
        Currency NVARCHAR(50) NULL,
        CurrencyRate NVARCHAR(50) NULL,
        Debbel NVARCHAR(50) NULL,
        ImportBatchId UNIQUEIDENTIFIER NOT NULL,
        ImportedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ImportedBy NVARCHAR(100) NULL,
        K4 NVARCHAR(50) NULL,
        K5 NVARCHAR(50) NULL,
        K6 NVARCHAR(50) NULL,
        K7 NVARCHAR(50) NULL,
        Kostbar NVARCHAR(50) NULL,
        Koststallekod NVARCHAR(50) NULL,
        Krebel NVARCHAR(50) NULL,
        Ktonr NVARCHAR(50) NULL,
        Momskod NVARCHAR(50) NULL,
        Projcode NVARCHAR(50) NULL,
        Rate NVARCHAR(50) NULL,
        RowNo INT NOT NULL,
        Valkod NVARCHAR(50) NULL,
        VoucherText NVARCHAR(255) NULL,
        Vbbelopp NVARCHAR(50) NULL,
        RawJson NVARCHAR(MAX) NULL,
        CompanyId UNIQUEIDENTIFIER NULL,
        ForetagKod INT NULL,
        UserId NVARCHAR(450) NULL,
        PostingDate DATE NULL,
        AterBokfDat DATE NULL
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Account' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Account NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Amount' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Amount NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Autoregel' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Autoregel NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Currency' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Currency NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CurrencyRate' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD CurrencyRate NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Debbel' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Debbel NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Kostbar' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Kostbar NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Koststallekod' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Koststallekod NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Krebel' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Krebel NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Ktonr' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Ktonr NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Momskod' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Momskod NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Projcode' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Projcode NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Rate' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Rate NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'RowNo' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD RowNo INT NOT NULL DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Valkod' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Valkod NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'VoucherText' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD VoucherText NVARCHAR(255) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Vbbelopp' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD Vbbelopp NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'RawJson' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD RawJson NVARCHAR(MAX) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CompanyId' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD CompanyId UNIQUEIDENTIFIER NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ForetagKod' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD ForetagKod INT NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'UserId' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD UserId NVARCHAR(450) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'PostingDate' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD PostingDate DATE NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'AterBokfDat' AND Object_ID = Object_ID('dbo.q_zu_StagingVoucher'))
        ALTER TABLE dbo.q_zu_StagingVoucher ADD AterBokfDat DATE NULL;
END"),
        ("dbo.q_zu_StagingPriceUpdate", @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'q_zu_StagingPriceUpdate' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.q_zu_StagingPriceUpdate (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ImportBatchId UNIQUEIDENTIFIER NOT NULL,
        RowNo INT NOT NULL,
        RawJson NVARCHAR(MAX) NULL,
        ImportedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ImportedBy NVARCHAR(100) NULL,
        CompanyId UNIQUEIDENTIFIER NULL,
        ForetagKod INT NULL,
        UserId NVARCHAR(450) NULL
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'RawJson' AND Object_ID = Object_ID('dbo.q_zu_StagingPriceUpdate'))
        ALTER TABLE dbo.q_zu_StagingPriceUpdate ADD RawJson NVARCHAR(MAX) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CompanyId' AND Object_ID = Object_ID('dbo.q_zu_StagingPriceUpdate'))
        ALTER TABLE dbo.q_zu_StagingPriceUpdate ADD CompanyId UNIQUEIDENTIFIER NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ForetagKod' AND Object_ID = Object_ID('dbo.q_zu_StagingPriceUpdate'))
        ALTER TABLE dbo.q_zu_StagingPriceUpdate ADD ForetagKod INT NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'UserId' AND Object_ID = Object_ID('dbo.q_zu_StagingPriceUpdate'))
        ALTER TABLE dbo.q_zu_StagingPriceUpdate ADD UserId NVARCHAR(450) NULL;
END"),
        ("dbo.q_zu_StagingPurchasePrice", @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'q_zu_StagingPurchasePrice' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.q_zu_StagingPurchasePrice
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ImportBatchId UNIQUEIDENTIFIER NOT NULL,
        RowNo INT NOT NULL,
        RawJson NVARCHAR(MAX) NULL,
        ImportedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ImportedBy NVARCHAR(100) NULL,
        CompanyId UNIQUEIDENTIFIER NULL,
        ForetagKod INT NULL,
        UserId NVARCHAR(450) NULL
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CompanyId' AND Object_ID = Object_ID('dbo.q_zu_StagingPurchasePrice'))
        ALTER TABLE dbo.q_zu_StagingPurchasePrice ADD CompanyId UNIQUEIDENTIFIER NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ForetagKod' AND Object_ID = Object_ID('dbo.q_zu_StagingPurchasePrice'))
        ALTER TABLE dbo.q_zu_StagingPurchasePrice ADD ForetagKod INT NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'UserId' AND Object_ID = Object_ID('dbo.q_zu_StagingPurchasePrice'))
        ALTER TABLE dbo.q_zu_StagingPurchasePrice ADD UserId NVARCHAR(450) NULL;
END"),
        ("dbo.q_zu_StagingExcelImportRowResult", @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'q_zu_StagingExcelImportRowResult' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.q_zu_StagingExcelImportRowResult
    (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ImportType NVARCHAR(50) NOT NULL,
        ImportBatchId UNIQUEIDENTIFIER NOT NULL,
        RowNo INT NOT NULL,
        IsValid BIT NOT NULL,
        DataJson NVARCHAR(MAX) NULL,
        ErrorMessage NVARCHAR(1000) NULL,
        ImportedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CompanyId UNIQUEIDENTIFIER NULL,
        UserId NVARCHAR(450) NULL
    );

    CREATE INDEX IX_q_zu_ExcelImportRowResult_Batch
        ON dbo.q_zu_StagingExcelImportRowResult (CompanyId, ImportType, ImportBatchId, IsValid, RowNo);
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ImportType' AND Object_ID = Object_ID('dbo.q_zu_StagingExcelImportRowResult'))
        ALTER TABLE dbo.q_zu_StagingExcelImportRowResult ADD ImportType NVARCHAR(50) NOT NULL DEFAULT '';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ImportBatchId' AND Object_ID = Object_ID('dbo.q_zu_StagingExcelImportRowResult'))
        ALTER TABLE dbo.q_zu_StagingExcelImportRowResult ADD ImportBatchId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'RowNo' AND Object_ID = Object_ID('dbo.q_zu_StagingExcelImportRowResult'))
        ALTER TABLE dbo.q_zu_StagingExcelImportRowResult ADD RowNo INT NOT NULL DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'IsValid' AND Object_ID = Object_ID('dbo.q_zu_StagingExcelImportRowResult'))
        ALTER TABLE dbo.q_zu_StagingExcelImportRowResult ADD IsValid BIT NOT NULL DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'DataJson' AND Object_ID = Object_ID('dbo.q_zu_StagingExcelImportRowResult'))
        ALTER TABLE dbo.q_zu_StagingExcelImportRowResult ADD DataJson NVARCHAR(MAX) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ErrorMessage' AND Object_ID = Object_ID('dbo.q_zu_StagingExcelImportRowResult'))
        ALTER TABLE dbo.q_zu_StagingExcelImportRowResult ADD ErrorMessage NVARCHAR(1000) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ImportedAt' AND Object_ID = Object_ID('dbo.q_zu_StagingExcelImportRowResult'))
        ALTER TABLE dbo.q_zu_StagingExcelImportRowResult ADD ImportedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CompanyId' AND Object_ID = Object_ID('dbo.q_zu_StagingExcelImportRowResult'))
        ALTER TABLE dbo.q_zu_StagingExcelImportRowResult ADD CompanyId UNIQUEIDENTIFIER NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'UserId' AND Object_ID = Object_ID('dbo.q_zu_StagingExcelImportRowResult'))
        ALTER TABLE dbo.q_zu_StagingExcelImportRowResult ADD UserId NVARCHAR(450) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_q_zu_ExcelImportRowResult_Batch' AND object_id = OBJECT_ID('dbo.q_zu_StagingExcelImportRowResult'))
    CREATE INDEX IX_q_zu_ExcelImportRowResult_Batch
        ON dbo.q_zu_StagingExcelImportRowResult (CompanyId, ImportType, ImportBatchId, IsValid, RowNo);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_q_zu_ExcelImportRowResult_Retention' AND object_id = OBJECT_ID('dbo.q_zu_StagingExcelImportRowResult'))
    CREATE INDEX IX_q_zu_ExcelImportRowResult_Retention
        ON dbo.q_zu_StagingExcelImportRowResult (ImportedAt);
"),
        ("dbo.q_zu_StagingTransAutoPrice", @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'q_zu_StagingTransAutoPrice' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.q_zu_StagingTransAutoPrice
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ImportBatchId UNIQUEIDENTIFIER NOT NULL,
        RowNo INT NOT NULL,
        Supplier NVARCHAR(100) NULL,
        SupplierArticleNo NVARCHAR(100) NULL,
        CustomerArticleNo NVARCHAR(100) NULL,
        Description NVARCHAR(500) NULL,
        CurrencyCode NVARCHAR(3) NULL,
        ListPrice DECIMAL(18,6) NULL,
        NetPrice DECIMAL(18,6) NULL,
        DiscountPercent DECIMAL(9,4) NULL,
        Uom NVARCHAR(50) NULL,
        MinimumOrderQuantity DECIMAL(18,6) NULL,
        PackageQuantity DECIMAL(18,6) NULL,
        WeightKg DECIMAL(18,6) NULL,
        CountryOfOrigin NVARCHAR(10) NULL,
        TariffCode NVARCHAR(50) NULL,
        ValidFrom DATE NULL,
        ValidTo DATE NULL,
        Category1 NVARCHAR(200) NULL,
        Category2 NVARCHAR(200) NULL,
        Category3 NVARCHAR(200) NULL,
        Category4 NVARCHAR(200) NULL,
        Category5 NVARCHAR(200) NULL,
        SourceFileName NVARCHAR(260) NULL,
        SourceSheetName NVARCHAR(128) NULL,
        SourceRowNo INT NULL,
        RawJson NVARCHAR(MAX) NULL,
        ImportedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ImportedBy NVARCHAR(100) NULL,
        CompanyId UNIQUEIDENTIFIER NULL,
        ForetagKod INT NULL,
        UserId NVARCHAR(450) NULL
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Supplier' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD Supplier NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'SupplierArticleNo' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD SupplierArticleNo NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CustomerArticleNo' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD CustomerArticleNo NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Description' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD Description NVARCHAR(500) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CurrencyCode' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD CurrencyCode NVARCHAR(3) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ListPrice' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD ListPrice DECIMAL(18,6) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'NetPrice' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD NetPrice DECIMAL(18,6) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'DiscountPercent' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD DiscountPercent DECIMAL(9,4) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Uom' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD Uom NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'MinimumOrderQuantity' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD MinimumOrderQuantity DECIMAL(18,6) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'PackageQuantity' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD PackageQuantity DECIMAL(18,6) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'WeightKg' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD WeightKg DECIMAL(18,6) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CountryOfOrigin' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD CountryOfOrigin NVARCHAR(10) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'TariffCode' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD TariffCode NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ValidFrom' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD ValidFrom DATE NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ValidTo' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD ValidTo DATE NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Category1' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD Category1 NVARCHAR(200) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Category2' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD Category2 NVARCHAR(200) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Category3' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD Category3 NVARCHAR(200) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Category4' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD Category4 NVARCHAR(200) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Category5' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD Category5 NVARCHAR(200) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'SourceFileName' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD SourceFileName NVARCHAR(260) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'SourceSheetName' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD SourceSheetName NVARCHAR(128) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'SourceRowNo' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD SourceRowNo INT NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'RawJson' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD RawJson NVARCHAR(MAX) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ImportedAt' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD ImportedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ImportedBy' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD ImportedBy NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CompanyId' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD CompanyId UNIQUEIDENTIFIER NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ForetagKod' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD ForetagKod INT NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'UserId' AND Object_ID = Object_ID('dbo.q_zu_StagingTransAutoPrice'))
        ALTER TABLE dbo.q_zu_StagingTransAutoPrice ADD UserId NVARCHAR(450) NULL;
END"),
        SupplierPriceStagingScript("dbo.q_zu_StagingPressKogyoPrice", "q_zu_StagingPressKogyoPrice")
    };

    private static (string TableName, string Sql) SupplierPriceStagingScript(string tableName, string objectName)
        => (tableName, $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{objectName}' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE {tableName}
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ImportBatchId UNIQUEIDENTIFIER NOT NULL,
        RowNo INT NOT NULL,
        Supplier NVARCHAR(100) NULL,
        SupplierArticleNo NVARCHAR(100) NULL,
        CustomerArticleNo NVARCHAR(100) NULL,
        Description NVARCHAR(500) NULL,
        CurrencyCode NVARCHAR(3) NULL,
        ListPrice DECIMAL(18,6) NULL,
        NetPrice DECIMAL(18,6) NULL,
        DiscountPercent DECIMAL(9,4) NULL,
        Uom NVARCHAR(50) NULL,
        MinimumOrderQuantity DECIMAL(18,6) NULL,
        PackageQuantity DECIMAL(18,6) NULL,
        WeightKg DECIMAL(18,6) NULL,
        CountryOfOrigin NVARCHAR(10) NULL,
        TariffCode NVARCHAR(50) NULL,
        ValidFrom DATE NULL,
        ValidTo DATE NULL,
        Category1 NVARCHAR(200) NULL,
        Category2 NVARCHAR(200) NULL,
        Category3 NVARCHAR(200) NULL,
        Category4 NVARCHAR(200) NULL,
        Category5 NVARCHAR(200) NULL,
        SourceFileName NVARCHAR(260) NULL,
        SourceSheetName NVARCHAR(128) NULL,
        SourceRowNo INT NULL,
        RawJson NVARCHAR(MAX) NULL,
        ImportedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ImportedBy NVARCHAR(100) NULL,
        CompanyId UNIQUEIDENTIFIER NULL,
        ForetagKod INT NULL,
        UserId NVARCHAR(450) NULL
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Supplier' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD Supplier NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'SupplierArticleNo' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD SupplierArticleNo NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CustomerArticleNo' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD CustomerArticleNo NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Description' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD Description NVARCHAR(500) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CurrencyCode' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD CurrencyCode NVARCHAR(3) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ListPrice' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD ListPrice DECIMAL(18,6) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'NetPrice' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD NetPrice DECIMAL(18,6) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'DiscountPercent' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD DiscountPercent DECIMAL(9,4) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Uom' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD Uom NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'MinimumOrderQuantity' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD MinimumOrderQuantity DECIMAL(18,6) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'PackageQuantity' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD PackageQuantity DECIMAL(18,6) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'WeightKg' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD WeightKg DECIMAL(18,6) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CountryOfOrigin' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD CountryOfOrigin NVARCHAR(10) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'TariffCode' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD TariffCode NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ValidFrom' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD ValidFrom DATE NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ValidTo' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD ValidTo DATE NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Category1' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD Category1 NVARCHAR(200) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Category2' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD Category2 NVARCHAR(200) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Category3' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD Category3 NVARCHAR(200) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Category4' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD Category4 NVARCHAR(200) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Category5' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD Category5 NVARCHAR(200) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'SourceFileName' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD SourceFileName NVARCHAR(260) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'SourceSheetName' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD SourceSheetName NVARCHAR(128) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'SourceRowNo' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD SourceRowNo INT NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'RawJson' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD RawJson NVARCHAR(MAX) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ImportedAt' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD ImportedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ImportedBy' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD ImportedBy NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CompanyId' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD CompanyId UNIQUEIDENTIFIER NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ForetagKod' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD ForetagKod INT NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'UserId' AND Object_ID = Object_ID('{tableName}'))
        ALTER TABLE {tableName} ADD UserId NVARCHAR(450) NULL;
END");
}
