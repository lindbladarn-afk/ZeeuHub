SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

ALTER PROCEDURE [dbo].[spr_Company]
    @SelectStatement           NVARCHAR(100),
    @CompanyId                 UNIQUEIDENTIFIER = NULL,
    @CompanyName               NVARCHAR(250) = NULL,
    @CompanyPermissionId       UNIQUEIDENTIFIER = NULL,
    @ModuleId                  UNIQUEIDENTIFIER = NULL,
    @SubModuleId               UNIQUEIDENTIFIER = NULL,

    -- Optional: create/update connection string row
    @ConnectionStringId        UNIQUEIDENTIFIER = NULL,
    @ConnectionStringTypeId    UNIQUEIDENTIFIER = NULL,
    @DatabaseName              NVARCHAR(250) = NULL,
    @IsActive                  BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY

        /* =========================
           CreateCompany (+ optional ConnectionString)
           ========================= */
        IF (@SelectStatement = 'CreateCompany')
        BEGIN
            IF (@CompanyId IS NULL)
            BEGIN
                ;THROW 50000, 'CreateCompany: CompanyId is required', 1;
            END

            IF (NULLIF(LTRIM(RTRIM(@CompanyName)), '') IS NULL)
            BEGIN
                ;THROW 50000, 'CreateCompany: CompanyName is required', 1;
            END

            BEGIN TRAN;

            IF EXISTS (SELECT 1 FROM [Identity].[Companies] WHERE Id = @CompanyId)
            BEGIN
                ;THROW 50000, 'CreateCompany: Company already exists (same CompanyId)', 1;
            END

            INSERT INTO [Identity].[Companies] (Id, Name)
            VALUES (@CompanyId, @CompanyName);

            -- Optional insert connection string row
            IF (@ConnectionStringId IS NOT NULL)
            BEGIN
                IF (@ConnectionStringTypeId IS NULL)
                BEGIN
                    ;THROW 50000, 'CreateCompany: ConnectionStringTypeId is required when ConnectionStringId is provided', 1;
                END

                -- Optional safety: if IsActive=1, deactivate other active rows for same company + type
                IF (@IsActive = 1)
                BEGIN
                    UPDATE [Identity].[ConnectionStrings]
                    SET IsActive = 0
                    WHERE CompanyId = @CompanyId
                      AND ConnectionStringTypeId = @ConnectionStringTypeId
                      AND IsActive = 1;
                END

                INSERT INTO [Identity].[ConnectionStrings] (Id, CompanyId, ConnectionStringTypeId, DatabaseName, IsActive)
                VALUES (@ConnectionStringId, @CompanyId, @ConnectionStringTypeId, @DatabaseName, @IsActive);
            END

            COMMIT;

            SELECT @CompanyId AS CompanyId, @ConnectionStringId AS ConnectionStringId;
            RETURN;
        END

        /* =========================
           UpdateCompany
           ========================= */
        IF (@SelectStatement = 'UpdateCompany')
        BEGIN
            IF (@CompanyId IS NULL)
            BEGIN
                ;THROW 50000, 'UpdateCompany: CompanyId is required', 1;
            END

            UPDATE [Identity].[Companies]
            SET Name = @CompanyName
            WHERE Id = @CompanyId;

            RETURN;
        END

        /* =========================
           AddCompanyPermission
           ========================= */
        IF (@SelectStatement = 'AddCompanyPermission')
        BEGIN
            IF (@CompanyPermissionId IS NULL OR @CompanyId IS NULL OR @ModuleId IS NULL)
            BEGIN
                ;THROW 50000, 'AddCompanyPermission: CompanyPermissionId, CompanyId, and ModuleId are required', 1;
            END

            INSERT INTO [Identity].[CompanyPermissions] (Id, CompanyId, ModuleId, SubModuleId)
            VALUES (@CompanyPermissionId, @CompanyId, @ModuleId, @SubModuleId);

            RETURN;
        END

        /* =========================
           RemoveCompanyPermission
           ========================= */
        IF (@SelectStatement = 'RemoveCompanyPermission')
        BEGIN
            IF (@CompanyPermissionId IS NULL)
            BEGIN
                ;THROW 50000, 'RemoveCompanyPermission: CompanyPermissionId is required', 1;
            END

            DELETE FROM [Identity].[CompanyPermissions]
            WHERE Id = @CompanyPermissionId;

            RETURN;
        END

        -- Unknown statement
        ;THROW 50000, 'spr_Company: Unknown SelectStatement', 1;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK;
        END

        ;THROW;
    END CATCH
END
GO