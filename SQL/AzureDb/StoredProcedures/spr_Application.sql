-- =============================================
-- Author:		Daniel Mellqvist
-- Create date: 2021-12-03
-- Description:	SP to get all the modules that should be 
-- displayed in the ZeeU customer portal
-- =============================================


CREATE PROCEDURE [dbo].[spr_Application]
	@SelectStatement	NVARCHAR(100)
	,@CompanyId			UNIQUEIDENTIFIER	= NULL
	,@UserId			UNIQUEIDENTIFIER	= NULL
AS
IF (@SelectStatement = 'GetUser')
BEGIN
	SELECT 
		U.Id						AS Id
		,U.FirstName				AS FirstName
		,U.LastName					AS LastName
		,U.PersSign					AS PersSign
		,U.Language					AS Language
		,U.CompanyId				AS CompanyId
		,U.ProfilePicture			AS ProfilePicture
		,U.Email					AS Email
		,U.PhoneNumber				AS PhoneNumber
		,U.ActiveConnectionStringId	AS ActiveConnectionStringId
	FROM [Identity].Users U
	WHERE U.Id = @UserId

	SELECT
		C.Id
		,C.Name
	FROM [Identity].Companies C
	INNER JOIN [Identity].Users U ON C.Id = U.CompanyId
	WHERE U.Id = @UserId
END