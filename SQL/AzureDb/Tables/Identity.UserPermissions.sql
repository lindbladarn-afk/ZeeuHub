/* Adds opt-in, per-user module visibility within the user's owning company. */
IF COL_LENGTH('[Identity].[Users]', 'UseCustomPermissions') IS NULL
BEGIN
    ALTER TABLE [Identity].[Users]
        ADD [UseCustomPermissions] bit NOT NULL
            CONSTRAINT [DF_Users_UseCustomPermissions] DEFAULT (0);
END;
GO

IF OBJECT_ID('[Identity].[UserPermissions]', 'U') IS NULL
BEGIN
    CREATE TABLE [Identity].[UserPermissions]
    (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [CompanyId] uniqueidentifier NOT NULL,
        [ModuleId] uniqueidentifier NOT NULL,
        [SubModuleId] uniqueidentifier NULL,
        CONSTRAINT [PK_UserPermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPermissions_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [Identity].[Users] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_UserPermissions_UserId_ModuleId_SubModuleId]
        ON [Identity].[UserPermissions] ([UserId], [ModuleId], [SubModuleId]);
    CREATE INDEX [IX_UserPermissions_CompanyId_UserId]
        ON [Identity].[UserPermissions] ([CompanyId], [UserId]);
END;
GO
