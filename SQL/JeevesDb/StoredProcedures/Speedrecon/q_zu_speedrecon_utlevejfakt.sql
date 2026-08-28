/****** Object:  StoredProcedure [dbo].[q_zu_speedrecon_utlevejfakt]    Script Date: 2024-02-12 22:04:54 ******/
SET ANSI_NULLS OFF
GO
SET QUOTED_IDENTIFIER OFF
GO
/*
**------------------------------------------------------------------
** Function...: <Describe the functionality>
** Created by.: Mikael Asklid, ZeeU AB
** Date.......: <YYMMDD>
** Called by..: Macro: / Stored procedure: / Trigger: / Report: / SQL Job: <object name>
** Written for: <CUSTOMER>
** Changes....: <ÄB#/Activity/Support Request No.> Created (Applied to production: <YYMMDD>)
** <YYMMDD>	<SIGN>	<ÄB#/Activity/Support Request No.> <Description of change> (Applied to production: <YYMMDD>)
**------------------------------------------------------------------
exec q_zu_speedrecon_utlevejfakt 9900,'jis','180801'
select foretagkod,ftgnamn,* from sy1


execute jeeves_oru_sal_PerDen
Resultatet läggs i tabell woha
Där ska select göras för att använda i tabellen ovan.
Select woha.ktonr, sum(woha.delnotinvoice) from woha
Where woha.foretagkod=9900	---variabel
And woha.rowcreatedby='jis' ---sql-agentjobb användare
And woha.regdat='20170704'---(datum som sql-agentjobbet körs)
Group by woha.ktonr

Uppdatera tabell enl ovan;
Woha.ktonr=q..ktonr
Sum woha.dellnotinvoice=q..reconamount
Woha.recondate= q..recondate


*/
CREATE PROCEDURE [dbo].[q_zu_speedrecon_utlevejfakt]
			@c_IntrnCoNo			SMALLINT = NULL,
			@c_PersSign				NVARCHAR(30) = NULL,
			@c_date					DATETIME =NULL,
			@c_fromdat				DATETIME = NULL,
			@c_zLanguage			INTEGER = NULL,

			-- arguments

			@c_DebugMsg NVARCHAR(10) = NULL

WITH ENCRYPTION
AS

SET NOCOUNT ON
SET ROWCOUNT 0

DECLARE	@Enter_TranCount INTEGER
SET @Enter_TranCount = @@TRANCOUNT

DECLARE
	@Procedure_Name			SYSNAME,
	@PgmId					NVARCHAR (1000),
	@catch_ERROR_NUMBER				INTEGER,
	@catch_ERROR_MESSAGE			NVARCHAR(4000),
	@Redar				SMALLINT,
	@period					SMALLINT,
	@today				varchar(6),
	@fromdat			datetime

BEGIN TRY
	SELECT @Procedure_Name = OBJECT_NAME(@@PROCID);
	select @today = CONVERT(varchar,getdate(),12)

	-- This is a one line comment, below is a block comment

	execute jeeves_oru_sal_PerDen
			 @c_Datum = @c_date,
			 @c_ForetagKod = @c_IntrnCoNo,
	         @c_PersSign =  @c_PersSign


	Insert into q_zu_speedrecon_result(
			q_zu_speedrecon_ktonr,
			ktobeskr,
			Q_zu_speedrecon_reconamount,
			Q_zu_speedrecon_recondate,
			perssign,
			foretagkod
			,q_zu_speedrecon_descr)
	Select woha.ktonr,
		ko.ktobeskr,
		isnull(sum(woha.delnotinvoice),0),
		recondate,
		woha.perssign,
		woha.foretagkod,
		'UTLEVEJFAKT'
	from woha WITH(READUNCOMMITTED)
		left join ko WITH(READUNCOMMITTED) on woha.foretagkod = ko.foretagkod and woha.ktonr = ko.ktonr and woha.Redovisnar = ko.Redovisnar
	Where woha.foretagkod=@c_IntrnCoNo	---variabel
		And woha.rowcreatedby=@c_PersSign ---sql-agentjobb användare
		And woha.regdat=@today---(datum som sql-agentjobbet körs)
	Group by woha.ktonr,ko.ktobeskr,recondate,woha.ForetagKod,woha.perssign

	--select regdat from reav where foretagkod = 9910
	--select * from q_zu_speedrecon_result

END TRY
BEGIN CATCH
			--<TAG_Catch_Begin
		IF @@TranCount > 0
		BEGIN
			ROLLBACK TRANSACTION
		END
		EXECUTE Jeeves_Catch_SPR_Error
				@catch_ERROR_NUMBER OUTPUT,
				@catch_ERROR_MESSAGE OUTPUT,
				@Procedure_Name,
				@PgmId,
				NULL,
				@c_IntrnCoNo,
				@c_PersSign,
				@c_zLanguage,
				NULL
		WHILE @@TranCount < @Enter_TranCount
			BEGIN TRANSACTION
		SELECT @catch_ERROR_MESSAGE = REPLACE(@catch_ERROR_MESSAGE,'%','')
		RAISERROR(@catch_ERROR_MESSAGE,16,1)
		RETURN -100
		--<TAG_Catch_End

END CATCH

RETURN 0
