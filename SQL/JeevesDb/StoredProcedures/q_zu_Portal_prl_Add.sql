
SET ANSI_NULLS OFF
GO
SET QUOTED_IDENTIFIER OFF
GO


create PROCEDURE [dbo].[q_zu_Portal_prl_Add]
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
		--, @CompanyId UNIQUEIDENTIFIER = '895C2620-1BF1-4BD4-A3BC-2FF00142555B' -- (Från Azure, skall vara kundens ID, används för att bygga upp deras sträng).
		, @CompanyId  NVARCHAR(MAX) = '895C2620-1BF1-4BD4-A3BC-2FF00142555B' -- (Från Azure, skall vara kundens ID, används för att bygga upp deras sträng).
		, @Id UNIQUEIDENTIFIER = NEWID()
		, @jvss1 nvarchar(255)

		select @jvss1 = dbo.jeeves_fn_getparam(@ForetagKod,N'custom_portal_1',N'jeevesparamstring','') --dbmail
		select @CompanyId = dbo.jeeves_fn_getparam(@ForetagKod,N'custom_portal_2',N'jeevesparamstring','') --azure bolagsid

	if nullif(cast(@CompanyId as nvarchar(255)),'') is null
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

	IF EXISTS(SELECT 1 FROM q_zu_portal_prl_list (NOLOCK) WHERE ForetagKod=@ForetagKod
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
	
	--exec JEEVES_Add_Column 'q_zu_portal_sales_list','sprakkod','integer'
	INSERT INTO q_zu_portal_prl_list VALUES (@Id, @ForetagKod, @c_prislista,@c_artnr,@c_altenhetkod,@c_limitlowant, @EmailAddress, 0, GETDATE()
	,@c_perssign2, @c_perssign, @c_sprakkod, 1);
END
