-- Adds production correlation, latency, model, and verification fields to AI telemetry.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF COL_LENGTH('Identity.AiQueryLogs', 'ResponseId') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [ResponseId] uniqueidentifier NULL;

IF COL_LENGTH('Identity.AiQueryLogs', 'PromptVersion') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [PromptVersion] nvarchar(100) NULL;

IF COL_LENGTH('Identity.AiQueryLogs', 'ModelDeployment') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [ModelDeployment] nvarchar(200) NULL;

IF COL_LENGTH('Identity.AiQueryLogs', 'ErrorCode') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [ErrorCode] nvarchar(100) NULL;

IF COL_LENGTH('Identity.AiQueryLogs', 'VerificationStatus') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [VerificationStatus] nvarchar(50) NULL;

IF COL_LENGTH('Identity.AiQueryLogs', 'DurationMs') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [DurationMs] bigint NULL;

IF COL_LENGTH('Identity.AiQueryLogs', 'PlanningDurationMs') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [PlanningDurationMs] bigint NULL;

IF COL_LENGTH('Identity.AiQueryLogs', 'SqlDurationMs') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [SqlDurationMs] bigint NULL;

IF COL_LENGTH('Identity.AiQueryLogs', 'SummaryDurationMs') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [SummaryDurationMs] bigint NULL;

IF COL_LENGTH('Identity.AiQueryLogs', 'ModelRetryCount') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [ModelRetryCount] int NULL;

IF COL_LENGTH('Identity.AiQueryLogs', 'RowCount') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [RowCount] int NULL;

IF COL_LENGTH('Identity.AiQueryLogs', 'WasTruncated') IS NULL
    ALTER TABLE [Identity].[AiQueryLogs] ADD [WasTruncated] bit NULL;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_AiQueryLogs_ResponseId'
      AND [object_id] = OBJECT_ID('[Identity].[AiQueryLogs]')
)
    EXEC(N'
        CREATE INDEX [IX_AiQueryLogs_ResponseId]
            ON [Identity].[AiQueryLogs] ([ResponseId])
            WHERE [ResponseId] IS NOT NULL;
    ');
