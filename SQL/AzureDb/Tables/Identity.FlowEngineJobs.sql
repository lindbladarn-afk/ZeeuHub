IF OBJECT_ID(N'[Identity].[FlowEngineJobs]', N'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[FlowEngineJobs]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_FlowEngineJobs] PRIMARY KEY
            CONSTRAINT [DF_FlowEngineJobs_Id] DEFAULT NEWID(),
        [CompanyId] UNIQUEIDENTIFIER NOT NULL,
        [UserId] NVARCHAR(450) NULL,
        [UserName] NVARCHAR(256) NULL,
        [Name] NVARCHAR(128) NULL,
        [UiLabel] NVARCHAR(128) NULL,
        [IsScheduled] BIT NOT NULL
            CONSTRAINT [DF_FlowEngineJobs_IsScheduled] DEFAULT ((0)),
        [Status] NVARCHAR(32) NOT NULL
            CONSTRAINT [DF_FlowEngineJobs_Status] DEFAULT (N'Queued'),
        [ArgumentsJson] NVARCHAR(MAX) NOT NULL
            CONSTRAINT [DF_FlowEngineJobs_ArgumentsJson] DEFAULT (N'[]'),
        [RequestJson] NVARCHAR(MAX) NULL,
        [CreatedAtUtc] DATETIME2(7) NOT NULL
            CONSTRAINT [DF_FlowEngineJobs_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
        [StartedAtUtc] DATETIME2(7) NULL,
        [FinishedAtUtc] DATETIME2(7) NULL,
        [ResultCommandLine] NVARCHAR(512) NULL,
        [ResultExitCode] INT NULL,
        [ResultSucceeded] BIT NULL,
        [ResultStandardOutput] NVARCHAR(MAX) NULL,
        [ResultStandardError] NVARCHAR(MAX) NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_FlowEngineJobs_CompanyId_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'[Identity].[FlowEngineJobs]', N'U')
)
BEGIN
    CREATE INDEX [IX_FlowEngineJobs_CompanyId_CreatedAtUtc]
        ON [Identity].[FlowEngineJobs] ([CompanyId], [CreatedAtUtc]);
END;
GO
