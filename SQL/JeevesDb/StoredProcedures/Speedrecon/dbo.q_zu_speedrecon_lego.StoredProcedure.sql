/****** Object:  StoredProcedure [dbo].[q_zu_speedrecon_lego]    Script Date: 3/19/2020 7:56:30 PM ******/
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
exec q_zu_speedrecon_inlevejfakt 30,'sys','190331'
select foretagkod,ftgnamn,* from sy1
select * from q_zu_speedrecon_result order by rowcreateddt desc

execute jeeves_wbpa_PerDen
Ange hårdkodat histfromdatum=’20100101’
Resultatet läggs i tabell wbpa
Där ska select göras för att använda i tabellen ovan.
Select wbpa.ktonr, sum(wbpa.debcresumamt) from wbpa
Where wbpa.foretagkod=9900	---variabel
And wbpa.rowcreatedby='jis' ---sql-agentjobb användare
And wbpa.regdat='20170704'---(datum som sql-agentjobbet körs)
And wbpa.ktonr=q_zeeu_speedrecon.Q_zeeu_speedrecon_acctgrni
Group by wbpa.ktonr

Uppdatera tabell enl ovan;
Wbpa.ktonr=q..ktonr
Sum wbpa.debcresumamt=q..reconamount
Wbpa.recondate= q..recondate



*/
CREATE PROCEDURE [dbo].[q_zu_speedrecon_lego]
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
	@period					SMALLINT,
	@akop_ktonr				NVARCHAR(8)	--181012
	,@today datetime --181124
	,@redar smallint


BEGIN TRY
	SELECT @Procedure_Name = OBJECT_NAME(@@PROCID);

	exec CalcPeriod @inpdat = @c_date,
					@Redovisnar = @redar OUTPUT,
					@Period = @Period OUTPUT,
					@c_intrnCoNo = @c_intrnCoNo,
					@calledfromprocedure = NULL,
					@just_orddat = NULL
	-- This is a one line comment, below is a block comment

	execute jeeves_wbpa_PerDen
			@c_IntrnCoNo  = @c_IntrnCoNo,
			@c_PersSign = @c_PersSign,
			@c_PerDenDatum = @c_date,
			@c_HistFromDatum = '100101'  --SKALL vara hårdkodat

	select @today =  CONVERT(varchar,getdate(),12) --181124
	--181018, nytt uppslag kontonr
	IF EXISTS(select 1 from akop where akop.foretagkod = @c_IntrnCoNo and akop.RedovisnAr=@redar and akop.arbtranstyp = 20)
	BEGIN

		select @akop_ktonr = ktonr
		from akop with(readuncommitted)
		where	akop.foretagkod = @c_IntrnCoNo and
				akop.RedovisnAr=@redar and
				akop.arbtranstyp = 20
    END ELSE
	BEGIN
		Set @akop_ktonr = 0
	END
--	if  @aklt_ktonr = '1502' print '1502'
	Insert into q_zu_speedrecon_result(
			q_zu_speedrecon_ktonr,
			Q_zu_speedrecon_reconamount,
			Q_zu_speedrecon_recondate,
			perssign,
			foretagkod,
			ktobeskr,
			q_zu_speedrecon_descr )
	Select
		wbpa.ktonr,
		isnull(sum(wbpa.debcresumamt),0) ,
		recondate,
		wbpa.perssign,
		wbpa.foretagkod,
		ko.ktobeskr, --181124
		'LEGO'
	from wbpa
		--join q_zu_speedrecon q on wbpa.foretagkod = q.foretagkod and wbpa.ktonr = @aklt_ktonr
		join ko on wbpa.foretagkod = ko.foretagkod and wbpa.redovisnar = ko.redovisnar and wbpa.ktonr = ko.ktonr
	Where wbpa.foretagkod=@c_IntrnCoNo	---variabel
		And wbpa.rowcreatedby=@c_PersSign ---sql-agentjobb användare
		--And wbpa.regdat=@today---(datum som sql-agentjobbet körs)		--181124
		And wbpa.regdat=@today---(datum som sql-agentjobbet körs)		--190404
		--And wbpa.ktonr=q_zeeu_speedrecon.Q_zeeu_speedrecon_acctgrni	--181012
		And wbpa.ktonr = @akop_ktonr									--181012

		Group by wbpa.ktonr,wbpa.recondate,		wbpa.perssign,		wbpa.foretagkod,ko.ktobeskr


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
GO
