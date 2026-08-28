CREATE PROCEDURE [dbo].[spr_GetModules]
AS
	SELECT 
		Id
		,ZeeuProductId
		,Name
		,Description
		,MenuSectionController
		,MenuSectionIcon
		,MenuSectionText
		,MenuSectionEnabled
	FROM [Identity].[Modules]