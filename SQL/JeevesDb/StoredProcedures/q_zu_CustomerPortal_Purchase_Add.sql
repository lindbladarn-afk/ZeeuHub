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
if not exists(select 1 from sys.objects where name like 'q_zu_CustomerPortal_Purchase_Add')
begin
	exec('create proc q_zu_CustomerPortal_Purchase_Add as select 123')
end
go
ALTER PROCEDURE [dbo].[q_zu_CustomerPortal_Purchase_Add]
	@ForetagKod SMALLINT,
	@BestNr BIGINT,
	@c_perssign2 nvarchar(60)=null,
	@c_perssign nvarchar(60)=null,
	@c_sprakkod int =0
AS
--zuorn 2024
BEGIN
	
	DECLARE @ErrorMessage NVARCHAR(MAX)
		, @EmailAddress NVARCHAR(512)
		, @Subject NVARCHAR(512)
		, @Body NVARCHAR(MAX)
		, @Purchaser NVARCHAR(255)
		, @Supplier NVARCHAR(255)
		, @CompanyId nvarchar(max) = '895C2620-1BF1-4BD4-A3BC-2FF00142555B' -- (Från Azure, skall vara kundens ID, används för att bygga upp deras sträng).
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

	SELECT @EmailAddress=sy2.YndEmailAddr
		, @Purchaser=bh.VRef
		, @Supplier=bh.FtgNr + ' - ' + fr.FtgNamn
	FROM bh 
		JOIN sy2 (NOLOCK) on bh.perssign=sy2.perssign
		JOIN fr (NOLOCK) on fr.ForetagKod=bh.ForetagKod
			AND fr.FtgNr=bh.FtgNr
	WHERE bh.foretagkod=@ForetagKod and bh.bestnr=@BestNr

	select @EmailAddress=pr.wEmailAddress
	from pr (readuncommitted)
	where
		pr.ForetagKod=@ForetagKod
	and pr.PersSign=@c_perssign2

	IF EXISTS(SELECT 1 FROM q_zu_CustomerPortal_purchase_list (NOLOCK) WHERE ForetagKod=@ForetagKod AND BestNr=@BestNr
	and perssign2=@c_perssign2 and Aktiv=1
	)
	BEGIN
		if @c_sprakkod<>0
		begin
			SET @ErrorMessage = 'Approval request was already sent for this Purchase order!';
		end
		else
		begin
			SET @ErrorMessage = 'Attest förfrågan är redan skickad för denna beställning!';
		end;
		THROW 50000, @ErrorMessage ,1;
	END
	
	INSERT INTO q_zu_CustomerPortal_purchase_list VALUES (@Id, @ForetagKod, @BestNr, @EmailAddress, 0, GETDATE()
	,@c_perssign2, @c_perssign, @c_sprakkod, 1);

	if @c_sprakkod<>0
	begin
		SET @Subject = 'Approval of purchase order: ' +CAST(@BestNr AS NVARCHAR(255));
		SET @Body = '<html><body><p>You have a new purchase order to approve.</p>';
		SET @Body = @Body + '<table><tr><td>Buyer:</td><td>' +@Purchaser+ '</td></tr>';
		SET @Body = @Body + '<tr><td>Purchase order:</td><td>' +CAST(@BestNr AS nvarchar(255))+  '</td></tr>';
		SET @Body = @Body + '<tr><td>Supplier:</td><td>' +@Supplier+ '</td></tr></table>';
		SET @Body = @Body + '<p><a href="https://customer.zeeu.se/WebApproval/PurchaseApprovalDetails/' +CAST(@CompanyId AS NVARCHAR(255))+ '//' + CAST(@Id AS nvarchar(255)) + '">Approve purchase order</a></p>';
		SET @Body = @Body + '</body></html>';
	end
	else
	begin
		SET @Subject = 'Attest av inköp: ' +CAST(@BestNr AS NVARCHAR(255));
		SET @Body = '<html><body><p>Du har fått en ny beställning att attestera.</p>';
		SET @Body = @Body + '<table><tr><td>Inköpare:</td><td>' +@Purchaser+ '</td></tr>';
		SET @Body = @Body + '<tr><td>Beställning:</td><td>' +CAST(@BestNr AS nvarchar(255))+  '</td></tr>';
		SET @Body = @Body + '<tr><td>Leverantör:</td><td>' +@Supplier+ '</td></tr></table>';
		SET @Body = @Body + '<p><a href="https://customer.zeeu.se/WebApproval/PurchaseApprovalDetails/' +CAST(@CompanyId AS NVARCHAR(255))+ '//' + CAST(@Id AS nvarchar(255)) + '">Attestera beställningen</a></p>';
		SET @Body = @Body + '</body></html>';
	end

	EXEC msdb.dbo.sp_send_dbmail
		 @profile_name = @jvss1,
		 @recipients = @EmailAddress,
		 @subject = @Subject,
		 @body = @Body,
		 @body_format = 'HTML';
		 
END
