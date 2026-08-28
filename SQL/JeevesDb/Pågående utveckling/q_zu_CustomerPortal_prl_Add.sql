set nocount on 
set ansi_warnings off 
set ansi_padding off 
set concat_null_yields_null off
set ANSI_NULL_DFLT_ON off 
set ANSI_NULL_DFLT_OFF off 
set arithabort off 
set numeric_roundabort off 
set ansi_nulls off 
set quoted_identifier off
go
CREATE PROCEDURE [dbo].[q_zu_CustomerPortal_prl_Add]
	@ForetagKod SMALLINT
,	@c_prislista int
,	@c_artnr jeeves_itemno
,	@c_altenhetkod nvarchar(20)
,	@c_limitlowant float
,	@c_perssign2 nvarchar(60)=null
,	@c_perssign nvarchar(60)=null
,	@c_sprakkod int =0
AS
BEGIN
	
	DECLARE @ErrorMessage NVARCHAR(MAX)
		, @EmailAddress NVARCHAR(512)
		, @Subject NVARCHAR(512)
		, @Body NVARCHAR(MAX)
		, @Purchaser NVARCHAR(255)
		, @Supplier NVARCHAR(255)
		, @CompanyId nvarchar(max) = '895C2620-1BF1-4BD4-A3BC-2FF00142555B' -- (Från Azure, skall vara kundens ID, används för att bygga upp deras sträng). --zuorn 230905, vrf ska det vara uniqueidentifier på denna?
		, @Id UNIQUEIDENTIFIER = NEWID()
		, @jvss1 nvarchar(255)

		select @jvss1 = dbo.jeeves_fn_getparam(@ForetagKod,N'custom_portal_1',N'jeevesparamstring','') --dbmail
		select @CompanyId = dbo.jeeves_fn_getparam(@ForetagKod,N'custom_portal_2',N'jeevesparamstring','') --azure bolagsid

	if nullif(@CompanyId,'') is null
	begin
		if @c_sprakkod<>0
		begin
			SET @ErrorMessage = 'No azure company ID set in jvsp! (see jvsparam custom_portal_2)';
		end
		else
		begin
			SET @ErrorMessage = 'Inget azure företags ID inställt i jvsp! (se jvsparam custom_portal_2)';
		end;
		THROW 50000, @ErrorMessage ,1;
	end

	select @EmailAddress=pr.wEmailAddress
	from pr (readuncommitted)
	where
		pr.ForetagKod=@ForetagKod
	and pr.PersSign=@c_perssign2

	IF EXISTS(SELECT 1 FROM q_zu_CustomerPortal_prl_list (NOLOCK) WHERE ForetagKod=@ForetagKod
	and prislista=	@c_prislista and artnr=@c_artnr and altenhetkod=@c_altenhetkod and limitlowant=@c_limitlowant
	and perssign2=@c_perssign2
	)
	BEGIN
		if @c_sprakkod<>0
		begin
			SET @ErrorMessage = 'Approval request was already sent for this price list row!';
		end
		else
		begin
			SET @ErrorMessage = 'Attest förfrågan är redan skickad för denna prislisterad!';
		end;
		THROW 50000, @ErrorMessage ,1;
	END
	
	INSERT INTO q_zu_CustomerPortal_prl_list VALUES (@Id, @ForetagKod, @c_prislista,@c_artnr,@c_altenhetkod,@c_limitlowant, @EmailAddress, 0, GETDATE()
	,@c_perssign2, @c_perssign, @c_sprakkod, 1);
END
GO


