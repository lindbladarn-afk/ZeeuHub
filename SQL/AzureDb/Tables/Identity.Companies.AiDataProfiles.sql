/* Stores the server-enforced Intelligence schema profile for each portal company. */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'Identity.Companies', N'AiDataProfile') IS NULL
BEGIN
    ALTER TABLE [Identity].[Companies]
        ADD [AiDataProfile] nvarchar(32) NOT NULL
            CONSTRAINT [DF_Companies_AiDataProfile] DEFAULT N'JeevesDirect';
END;

IF COL_LENGTH(N'Identity.Companies', N'AiAllowDataSourceSwitching') IS NULL
BEGIN
    ALTER TABLE [Identity].[Companies]
        ADD [AiAllowDataSourceSwitching] bit NOT NULL
            CONSTRAINT [DF_Companies_AiAllowDataSourceSwitching] DEFAULT (0);
END;

IF COL_LENGTH(N'Identity.Companies', N'AiPrimaryConnectionStringId') IS NULL
BEGIN
    ALTER TABLE [Identity].[Companies]
        ADD [AiPrimaryConnectionStringId] uniqueidentifier NULL;
END;

IF COL_LENGTH(N'Identity.ConnectionStrings', N'AiDataProfile') IS NULL
BEGIN
    ALTER TABLE [Identity].[ConnectionStrings]
        ADD [AiDataProfile] nvarchar(32) NOT NULL
            CONSTRAINT [DF_ConnectionStrings_AiDataProfile] DEFAULT N'JeevesDirect';
END;

IF COL_LENGTH(N'Identity.ConnectionStrings', N'IsAiEnabled') IS NULL
BEGIN
    ALTER TABLE [Identity].[ConnectionStrings]
        ADD [IsAiEnabled] bit NOT NULL
            CONSTRAINT [DF_ConnectionStrings_IsAiEnabled] DEFAULT (0);
END;

EXEC sys.sp_executesql N'
    UPDATE [Identity].[Companies]
    SET [AiDataProfile] = N''JeevesDirect''
    WHERE [AiDataProfile] IS NULL
       OR [AiDataProfile] NOT IN (N''JeevesDirect'', N''DataWarehouse'');';

COMMIT TRANSACTION;
