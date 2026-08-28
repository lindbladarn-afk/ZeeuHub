-- Stores each user's dashboard layout independently for every company they can access.
IF OBJECT_ID(N'[Identity].[DashboardWidgetPreferences]', N'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[DashboardWidgetPreferences]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK_DashboardWidgetPreferences] PRIMARY KEY
            CONSTRAINT [DF_DashboardWidgetPreferences_Id] DEFAULT NEWID(),
        [UserId] NVARCHAR(450) NOT NULL,
        [CompanyId] UNIQUEIDENTIFIER NOT NULL,
        [WidgetId] NVARCHAR(64) NOT NULL,
        [SortOrder] INT NOT NULL,
        [Size] NVARCHAR(16) NOT NULL
            CONSTRAINT [DF_DashboardWidgetPreferences_Size] DEFAULT N'Compact',
        [IsVisible] BIT NOT NULL
            CONSTRAINT [DF_DashboardWidgetPreferences_IsVisible] DEFAULT ((1)),
        [UpdatedAtUtc] DATETIME2(7) NOT NULL
            CONSTRAINT [DF_DashboardWidgetPreferences_UpdatedAtUtc] DEFAULT SYSUTCDATETIME()
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_DashboardWidgetPreferences_Scope' AND object_id = OBJECT_ID(N'[Identity].[DashboardWidgetPreferences]', N'U'))
BEGIN
    CREATE UNIQUE INDEX [UX_DashboardWidgetPreferences_Scope]
        ON [Identity].[DashboardWidgetPreferences] ([UserId], [CompanyId], [WidgetId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DashboardWidgetPreferences_SortOrder' AND object_id = OBJECT_ID(N'[Identity].[DashboardWidgetPreferences]', N'U'))
BEGIN
    CREATE INDEX [IX_DashboardWidgetPreferences_SortOrder]
        ON [Identity].[DashboardWidgetPreferences] ([UserId], [CompanyId], [SortOrder]);
END;
GO
