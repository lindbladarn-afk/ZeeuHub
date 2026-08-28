CREATE PROCEDURE [dbo].[spr_GetCompanyPermissions]
	@CompanyId UNIQUEIDENTIFIER
AS
	SELECT 
		Id
		,CompanyId
		,ModuleId
		,SubModuleId
	FROM [Identity].[CompanyPermissions]
	WHERE CompanyId = @CompanyId