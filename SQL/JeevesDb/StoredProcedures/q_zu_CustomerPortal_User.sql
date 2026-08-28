-- =============================================
-- Author:		Daniel Mellqvist, ZeeU AB
-- Create date: 2021-12-30
-- Called by:	ZeeU.CustomerPortal
-- Description:	SP to get all the users connected jeeves companies
-- =============================================

CREATE PROCEDURE [dbo].[q_zu_CustomerPortal_User]
	@SelectStatement	NVARCHAR(100)
	,@EmailAddress		NVARCHAR(512)		=NULL
	,@PersSign			NVARCHAR(30)		=NULL
	,@ForetagKod		SMALLINT			=NULL
	,@ErrorMessage		NVARCHAR(MAX)		=NULL

AS
BEGIN
	IF (@SelectStatement = 'GetJeevesCompanies')
	BEGIN
		-- Validate: PersSign
		IF NOT EXISTS(SELECT 1 FROM sy2 WITH(READUNCOMMITTED) WHERE perssign = @perssign )
		BEGIN
			SET @ErrorMessage = 'Failed to find persign: ' +@PersSign+ ' in Jeeves.';
			THROW 50000, @ErrorMessage, 1;
		END;
	

		--SELECT 
		--	sy1.foretagkod								AS ForetagKod
		--	,sy1.ftgnamn								AS Name
		--	,CASE(sy2.DefForetagKod) 
		--		WHEN sy1.foretagkod THEN 1 ELSE 0 END	AS IsDefault
		--FROM sy1
		--LEFT OUTER JOIN sy2 ON 1=1
		--WHERE sy2.perssign = @perssign
		--ORDER BY sy1.foretagkod

		;WITH CompanyList AS (
			SELECT DISTINCT jvss.ForetagKod, sy1.ftgnamn
			FROM jvss (readuncommitted)
			JOIN sy1 ON jvss.ForetagKod = sy1.ForetagKod
			WHERE JeevesParamName IN ('custom_portal_3', 'custom_portal_4', 'custom_portal_5')
			  AND JeevesParamBoolean = '1'
		),
		DefaultCheck AS (
			SELECT
				CASE
					WHEN EXISTS (
						SELECT 1
						FROM CompanyList cl
						JOIN sy2 ON sy2.perssign = @perssign AND cl.ForetagKod = sy2.DefForetagKod
					)
					THEN (SELECT DefForetagKod FROM sy2 WHERE perssign=@perssign)
					ELSE (SELECT TOP 1 ForetagKod FROM CompanyList ORDER BY ForetagKod)
				END AS ActiveForetagKod
		)
		SELECT
			c.ForetagKod,
			c.ftgnamn AS Name,
			CASE WHEN c.ForetagKod = d.ActiveForetagKod THEN 1 ELSE 0 END AS IsDefault
		FROM CompanyList c
		CROSS JOIN DefaultCheck d
		ORDER BY c.ForetagKod;

	END
END