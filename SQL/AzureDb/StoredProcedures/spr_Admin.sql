SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

ALTER PROCEDURE [dbo].[spr_Admin]
    @SelectStatement    NVARCHAR(100),
    @CompanyId          UNIQUEIDENTIFIER = NULL,
    @UserId             NVARCHAR(450) = NULL   -- FIX: Identity UserId är string
AS
BEGIN
    SET NOCOUNT ON;

    IF (@SelectStatement = 'GetAllCompanies')
    BEGIN
        SELECT C.Id, C.Name
        FROM [Identity].[Companies] C
        ORDER BY C.Name ASC;

        SELECT P.Id, P.CompanyId, P.ModuleId, P.SubModuleId
        FROM [Identity].[CompanyPermissions] P;

        SELECT M.Id, M.ZeeuProductId, M.Name, M.Description, M.MenuSectionText
        FROM [Identity].[Modules] M;

        SELECT S.Id, S.ModuleId, S.Name, S.Description, S.MenuItemText
        FROM [Identity].[SubModules] S;

        SELECT CS.Id, CS.CompanyId, CS.ConnectionStringTypeId, CS.DatabaseName, CS.IsActive
        FROM [Identity].[ConnectionStrings] CS;

        SELECT CST.Id, CST.Name, CST.SuffixName
        FROM [Identity].[ConnectionStringTypes] CST;
    END

    IF (@SelectStatement = 'GetCompanyById')
    BEGIN
        SELECT C.Id, C.Name
        FROM [Identity].[Companies] C
        WHERE C.Id = @CompanyId;

        SELECT P.Id, P.CompanyId, P.ModuleId, P.SubModuleId
        FROM [Identity].[CompanyPermissions] P
        WHERE P.CompanyId = @CompanyId;

        SELECT M.Id, M.ZeeuProductId, M.Name, M.Description, M.MenuSectionText
        FROM [Identity].[Modules] M;

        SELECT S.Id, S.ModuleId, S.Name, S.Description, S.MenuItemText
        FROM [Identity].[SubModules] S;

        SELECT CS.Id, CS.CompanyId, CS.ConnectionStringTypeId, CS.DatabaseName, CS.IsActive
        FROM [Identity].[ConnectionStrings] CS
        WHERE CS.CompanyId = @CompanyId;

        SELECT CST.Id, CST.Name, CST.SuffixName
        FROM [Identity].[ConnectionStringTypes] CST;
    END

    IF (@SelectStatement = 'GetAllSelectListCompanies')
    BEGIN
        SELECT C.Id, C.Name
        FROM [Identity].[Companies] C
        ORDER BY C.Name ASC;
    END

    IF (@SelectStatement = 'GetAllSelectListConnectionStrings')
    BEGIN
        SELECT CS.Id AS Id,
               CS.CompanyId AS CompanyId,
               CS.ConnectionStringTypeId AS ConnectionStringTypeId,
               CS.DatabaseName AS DatabaseName,
               CST.Name AS ConnectionStringTypeName,
               CS.IsActive AS IsActive
        FROM [Identity].[ConnectionStrings] CS
        INNER JOIN [Identity].[ConnectionStringTypes] CST ON CS.ConnectionStringTypeId = CST.Id
        WHERE (@CompanyId IS NULL OR CS.CompanyId = @CompanyId);
    END

    IF (@SelectStatement = 'GetConnectionStringTypes')
    BEGIN
        SELECT CST.Id, CST.Name, CST.SuffixName
        FROM [Identity].[ConnectionStringTypes] CST;
    END

    IF (@SelectStatement = 'GetUserCompany')
    BEGIN
        SELECT C.Id, C.Name
        FROM [Identity].[Users] U
        INNER JOIN [Identity].[Companies] C ON U.CompanyId = C.Id
        WHERE U.Id = @UserId;
    END

    IF (@SelectStatement = 'GetUserCompaniesLookup')
    BEGIN
        SELECT U.Id AS UserId, C.Name AS CompanyName
        FROM [Identity].[Users] AS U
        INNER JOIN [Identity].[Companies] AS C ON U.CompanyId = C.Id;
    END
END
GO