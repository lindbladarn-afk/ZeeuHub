-- =============================================
-- Author:		Daniel Mellqvist, ZeeU AB
-- Create date: 2022-01-02
-- Called by:	ZeeU.CustomerPortal
-- Description:	SP to handle all customer related transactions
-- =============================================

CREATE PROCEDURE [dbo].[q_zu_CustomerPortal_Customer]
	@SelectStatement	NVARCHAR(100)
	,@ErrorMessage		NVARCHAR(MAX)	=NULL
	,@ForetagKod		SMALLINT		=NULL
	,@Country			NVARCHAR(3)		='SE'

AS
BEGIN
	IF (@SelectStatement = 'GetAllCustomers')
	BEGIN
		IF isnull(@ForetagKod,'') = ''
		BEGIN
				SET @ErrorMessage = 'No company code provided'
				EXEC Jeeves_raiserror 50000, @ErrorMessage;
				THROW 50000, @ErrorMessage, 0;
		END

		SELECT
			fr.ftgnr			AS ForetagKod
			,ftgnamn			AS FtgNamn
			,ftgpostadr1		AS FtgPostAdr1
			,ftgpostadr2		AS FtgPostAdr2
			,ftgpostadr3		AS FtgPostAdr3
			,ftgpostnr			AS ZipCode
			,fr.landskod		AS Country
			,orgnr				AS OrgNr
			,kus.kundoms		AS KundOms
			,kus.kundomsfa		AS KundOmsFa
			,salj.SaljareNamn	AS SaljareNamn
		 FROM fr WITH(READUNCOMMITTED)
			JOIN KUS ON fr.foretagkod = kus.foretagkod AND fr.ftgnr = kus.ftgnr
			LEFT JOIN salj ON fr.ForetagKod = salj.ForetagKod AND kus.Saljare = salj.Saljare
		 WHERE fr.foretagkod = @foretagkod
			AND ISNULL(kus.Makulerad,'0') = '0'
	END
END