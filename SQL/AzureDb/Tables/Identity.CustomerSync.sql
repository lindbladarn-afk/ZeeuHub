IF OBJECT_ID(N'[Identity].[CustomerSyncMappings]', N'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[CustomerSyncMappings]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_CustomerSyncMappings] PRIMARY KEY
            CONSTRAINT [DF_CustomerSyncMappings_Id] DEFAULT NEWID(),
        [CompanyId] UNIQUEIDENTIFIER NOT NULL,
        [JeevesCompanyCode] INT NOT NULL,
        [JeevesCustomerNumber] NVARCHAR(64) NULL,
        [HubSpotCompanyId] NVARCHAR(64) NULL,
        [HubSpotContactId] NVARCHAR(64) NULL,
        [OrganizationNumber] NVARCHAR(64) NULL,
        [NormalizedName] NVARCHAR(256) NULL,
        [LastSyncedFromJeevesAtUtc] DATETIME2(7) NULL,
        [LastSyncedFromHubSpotAtUtc] DATETIME2(7) NULL,
        [CreatedAtUtc] DATETIME2(7) NOT NULL
            CONSTRAINT [DF_CustomerSyncMappings_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
        [UpdatedAtUtc] DATETIME2(7) NOT NULL
            CONSTRAINT [DF_CustomerSyncMappings_UpdatedAtUtc] DEFAULT SYSUTCDATETIME()
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CustomerSyncMappings_JeevesCustomer' AND object_id = OBJECT_ID(N'[Identity].[CustomerSyncMappings]', N'U'))
BEGIN
    CREATE UNIQUE INDEX [UX_CustomerSyncMappings_JeevesCustomer]
        ON [Identity].[CustomerSyncMappings] ([CompanyId], [JeevesCompanyCode], [JeevesCustomerNumber])
        WHERE [JeevesCustomerNumber] IS NOT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CustomerSyncMappings_HubSpotCompany' AND object_id = OBJECT_ID(N'[Identity].[CustomerSyncMappings]', N'U'))
BEGIN
    CREATE UNIQUE INDEX [UX_CustomerSyncMappings_HubSpotCompany]
        ON [Identity].[CustomerSyncMappings] ([CompanyId], [HubSpotCompanyId])
        WHERE [HubSpotCompanyId] IS NOT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerSyncMappings_OrganizationNumber' AND object_id = OBJECT_ID(N'[Identity].[CustomerSyncMappings]', N'U'))
BEGIN
    CREATE INDEX [IX_CustomerSyncMappings_OrganizationNumber]
        ON [Identity].[CustomerSyncMappings] ([CompanyId], [OrganizationNumber]);
END;
GO

IF COL_LENGTH(N'[Identity].[CustomerSyncMappings]', N'Domain') IS NULL
BEGIN
    ALTER TABLE [Identity].[CustomerSyncMappings]
        ADD [Domain] NVARCHAR(256) NULL;
END;
GO

IF COL_LENGTH(N'[Identity].[CustomerSyncMappings]', N'Email') IS NULL
BEGIN
    ALTER TABLE [Identity].[CustomerSyncMappings]
        ADD [Email] NVARCHAR(256) NULL;
END;
GO

IF COL_LENGTH(N'[Identity].[CustomerSyncMappings]', N'Phone') IS NULL
BEGIN
    ALTER TABLE [Identity].[CustomerSyncMappings]
        ADD [Phone] NVARCHAR(64) NULL;
END;
GO

IF COL_LENGTH(N'[Identity].[CustomerSyncMappings]', N'HubSpotUpdatedAtUtc') IS NULL
BEGIN
    ALTER TABLE [Identity].[CustomerSyncMappings]
        ADD [HubSpotUpdatedAtUtc] DATETIME2(7) NULL;
END;
GO

IF OBJECT_ID(N'[Identity].[CustomerSyncCheckpoints]', N'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[CustomerSyncCheckpoints]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_CustomerSyncCheckpoints] PRIMARY KEY
            CONSTRAINT [DF_CustomerSyncCheckpoints_Id] DEFAULT NEWID(),
        [CompanyId] UNIQUEIDENTIFIER NOT NULL,
        [JeevesCompanyCode] INT NOT NULL,
        [Direction] NVARCHAR(64) NOT NULL,
        [CheckpointValue] NVARCHAR(256) NULL,
        [CheckpointUtc] DATETIME2(7) NULL,
        [UpdatedAtUtc] DATETIME2(7) NOT NULL
            CONSTRAINT [DF_CustomerSyncCheckpoints_UpdatedAtUtc] DEFAULT SYSUTCDATETIME()
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CustomerSyncCheckpoints_Scope' AND object_id = OBJECT_ID(N'[Identity].[CustomerSyncCheckpoints]', N'U'))
BEGIN
    CREATE UNIQUE INDEX [UX_CustomerSyncCheckpoints_Scope]
        ON [Identity].[CustomerSyncCheckpoints] ([CompanyId], [JeevesCompanyCode], [Direction]);
END;
GO

IF OBJECT_ID(N'[Identity].[CustomerSyncRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[CustomerSyncRuns]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_CustomerSyncRuns] PRIMARY KEY
            CONSTRAINT [DF_CustomerSyncRuns_Id] DEFAULT NEWID(),
        [CompanyId] UNIQUEIDENTIFIER NOT NULL,
        [JeevesCompanyCode] INT NOT NULL,
        [Direction] NVARCHAR(64) NOT NULL,
        [Trigger] NVARCHAR(64) NOT NULL,
        [Status] NVARCHAR(64) NOT NULL,
        [StartedAtUtc] DATETIME2(7) NOT NULL,
        [FinishedAtUtc] DATETIME2(7) NULL,
        [CreatedCount] INT NOT NULL CONSTRAINT [DF_CustomerSyncRuns_CreatedCount] DEFAULT ((0)),
        [UpdatedCount] INT NOT NULL CONSTRAINT [DF_CustomerSyncRuns_UpdatedCount] DEFAULT ((0)),
        [SkippedCount] INT NOT NULL CONSTRAINT [DF_CustomerSyncRuns_SkippedCount] DEFAULT ((0)),
        [FailedCount] INT NOT NULL CONSTRAINT [DF_CustomerSyncRuns_FailedCount] DEFAULT ((0)),
        [CorrelationId] NVARCHAR(128) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerSyncRuns_Company_StartedAtUtc' AND object_id = OBJECT_ID(N'[Identity].[CustomerSyncRuns]', N'U'))
BEGIN
    CREATE INDEX [IX_CustomerSyncRuns_Company_StartedAtUtc]
        ON [Identity].[CustomerSyncRuns] ([CompanyId], [StartedAtUtc]);
END;
GO

IF OBJECT_ID(N'[Identity].[CustomerSyncRunItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[CustomerSyncRunItems]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_CustomerSyncRunItems] PRIMARY KEY
            CONSTRAINT [DF_CustomerSyncRunItems_Id] DEFAULT NEWID(),
        [RunId] UNIQUEIDENTIFIER NOT NULL,
        [CompanyId] UNIQUEIDENTIFIER NOT NULL,
        [ExternalKey] NVARCHAR(128) NULL,
        [JeevesCustomerNumber] NVARCHAR(64) NULL,
        [HubSpotObjectId] NVARCHAR(64) NULL,
        [Status] NVARCHAR(64) NOT NULL,
        [ErrorCode] NVARCHAR(64) NULL,
        [ErrorMessage] NVARCHAR(1000) NULL,
        [CreatedAtUtc] DATETIME2(7) NOT NULL
            CONSTRAINT [DF_CustomerSyncRunItems_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [FK_CustomerSyncRunItems_CustomerSyncRuns_RunId]
            FOREIGN KEY ([RunId]) REFERENCES [Identity].[CustomerSyncRuns] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerSyncRunItems_RunId' AND object_id = OBJECT_ID(N'[Identity].[CustomerSyncRunItems]', N'U'))
BEGIN
    CREATE INDEX [IX_CustomerSyncRunItems_RunId]
        ON [Identity].[CustomerSyncRunItems] ([RunId]);
END;
GO

IF OBJECT_ID(N'[Identity].[CustomerSyncEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[CustomerSyncEvents]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_CustomerSyncEvents] PRIMARY KEY
            CONSTRAINT [DF_CustomerSyncEvents_Id] DEFAULT NEWID(),
        [CompanyId] UNIQUEIDENTIFIER NOT NULL,
        [HubSpotEventId] NVARCHAR(128) NOT NULL,
        [HubSpotObjectId] NVARCHAR(64) NULL,
        [EventType] NVARCHAR(128) NULL,
        [PayloadHash] NVARCHAR(128) NULL,
        [ReceivedAtUtc] DATETIME2(7) NOT NULL,
        [ProcessedAtUtc] DATETIME2(7) NULL,
        [Status] NVARCHAR(64) NOT NULL,
        [ErrorMessage] NVARCHAR(1000) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CustomerSyncEvents_HubSpotEvent' AND object_id = OBJECT_ID(N'[Identity].[CustomerSyncEvents]', N'U'))
BEGIN
    CREATE UNIQUE INDEX [UX_CustomerSyncEvents_HubSpotEvent]
        ON [Identity].[CustomerSyncEvents] ([CompanyId], [HubSpotEventId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerSyncEvents_Company_ReceivedAtUtc' AND object_id = OBJECT_ID(N'[Identity].[CustomerSyncEvents]', N'U'))
BEGIN
    CREATE INDEX [IX_CustomerSyncEvents_Company_ReceivedAtUtc]
        ON [Identity].[CustomerSyncEvents] ([CompanyId], [ReceivedAtUtc]);
END;
GO

IF OBJECT_ID(N'[Identity].[CustomerSyncRuntimeConfiguration]', N'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[CustomerSyncRuntimeConfiguration]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_CustomerSyncRuntimeConfiguration] PRIMARY KEY
            CONSTRAINT [DF_CustomerSyncRuntimeConfiguration_Id] DEFAULT NEWID(),
        [ConfigurationName] NVARCHAR(64) NOT NULL,
        [ConfigurationJson] NVARCHAR(MAX) NOT NULL,
        [CreatedAtUtc] DATETIME2(7) NOT NULL
            CONSTRAINT [DF_CustomerSyncRuntimeConfiguration_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
        [UpdatedAtUtc] DATETIME2(7) NOT NULL
            CONSTRAINT [DF_CustomerSyncRuntimeConfiguration_UpdatedAtUtc] DEFAULT SYSUTCDATETIME()
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CustomerSyncRuntimeConfiguration_Name' AND object_id = OBJECT_ID(N'[Identity].[CustomerSyncRuntimeConfiguration]', N'U'))
BEGIN
    CREATE UNIQUE INDEX [UX_CustomerSyncRuntimeConfiguration_Name]
        ON [Identity].[CustomerSyncRuntimeConfiguration] ([ConfigurationName]);
END;
GO
