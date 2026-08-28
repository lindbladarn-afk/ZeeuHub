-- =============================================
-- Author:		Daniel Mellqvist
-- Create date: 2021-12-03
-- Description:	SP to get all the modules that should be 
-- displayed in the ZeeU customer portal
-- =============================================


CREATE PROCEDURE [dbo].[spr_ModulesAndSubModules]
	@SelectStatement	NVARCHAR(100)
	,@ModuleId			UNIQUEIDENTIFIER	= NULL
	,@SubModuleId		UNIQUEIDENTIFIER	= NULL
	,@CompanyId			UNIQUEIDENTIFIER	= NULL
AS
	IF (@SelectStatement = 'AllModules')
	BEGIN
		SELECT 
			Id
			,ZeeuProductId
			,Name
			,Description
			,MenuSectionController
			,MenuSectionAction
			,MenuSectionIcon
			,MenuSectionText
			,MenuSectionEnabled
		FROM [Identity].[Modules]
	END

	IF (@SelectStatement = 'SubModulesByModuleId')
	BEGIN
		SELECT 
			Id
			,ModuleId
			,Name
			,Description
			,MenuItemController
			,MenuItemAction
			,MenuItemText
			,MenuItemEnabled
		FROM [Identity].[SubModules]
		WHERE ModuleId = @ModuleId
	END

	IF (@SelectStatement = 'ModulesAndPermissions')
	BEGIN
		SELECT 
			A.Id
			,A.ZeeuProductId
			,A.Name
			,A.Description
			,A.MenuSectionController
			,A.MenuSectionAction
			,A.MenuSectionIcon
			,A.MenuSectionText
			,A.MenuSectionEnabled
			,A.MenuSectionSortOrder
			,CASE WHEN EXISTS (SELECT * FROM [Identity].[CompanyPermissions] P WHERE P.CompanyId = @CompanyId AND A.Id = P.ModuleId)
				THEN 1
				ELSE 0
				END AS CompanyHasPermission
		FROM [Identity].[Modules] A
		WHERE A.MenuSectionEnabled = 1
		ORDER BY A.MenuSectionSortOrder ASC

		SELECT 
			A.Id
			,A.ModuleId
			,A.Name
			,A.Description
			,A.MenuItemController
			,A.MenuItemAction
			,A.MenuItemText
			,A.MenuItemSortOrder
			,A.MenuItemEnabled
		FROM [Identity].[SubModules] A
		WHERE A.MenuItemEnabled = 1
		ORDER BY A.ModuleId, A.MenuItemSortOrder
	END