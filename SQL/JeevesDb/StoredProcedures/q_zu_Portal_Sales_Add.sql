SET ANSI_NULLS OFF
GO
SET QUOTED_IDENTIFIER OFF
GO

CREATE PROCEDURE [dbo].[q_zu_Portal_Sales_Add]
	@ForetagKod SMALLINT,
	@OrderNr BIGINT,
	@c_perssign2 nvarchar(60)=null,
	@c_perssign nvarchar(60)=null,
	@c_sprakkod int =0
AS
BEGIN
	
	DECLARE @ErrorMessage NVARCHAR(MAX)
		, @EmailAddress NVARCHAR(512)
		, @Subject NVARCHAR(512)
		, @Body NVARCHAR(MAX)
		, @Purchaser NVARCHAR(255)
		, @Supplier NVARCHAR(255)
		, @CompanyId UNIQUEIDENTIFIER = '895C2620-1BF1-4BD4-A3BC-2FF00142555B' -- (Från Azure, skall vara kundens ID, används för att bygga upp deras sträng).
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

	-- Hämtar epostadress.
	SELECT @EmailAddress=sy2.YndEmailAddr
		, @Purchaser=oh.VRef
		, @Supplier=oh.FtgNr + ' - ' + fr.FtgNamn
	FROM oh 
		JOIN sy2 (NOLOCK) on oh.perssign=sy2.perssign
		JOIN fr (NOLOCK) on fr.ForetagKod=oh.ForetagKod
			AND fr.FtgNr=oh.FtgNr
	WHERE oh.foretagkod=@ForetagKod and oh.ordernr=@OrderNr

	-- Validera epostadress...
	--SET @EmailAddress = 'ja@zeeu.se';

	select @EmailAddress=pr.wEmailAddress
	from pr (readuncommitted)
	where
		pr.ForetagKod=@ForetagKod
	and pr.PersSign=@c_perssign2

	IF EXISTS(SELECT 1 FROM q_zu_portal_sales_list (NOLOCK) WHERE ForetagKod=@ForetagKod AND OrderNr=@OrderNr
	and perssign2=@c_perssign2
	)
	BEGIN
		if @c_sprakkod<>0
		begin
			SET @ErrorMessage = 'Approval request was already sent for this sales order!';
		end
		else
		begin
			SET @ErrorMessage = 'Attest förfrågan är redan skickad för denna kundorder!';
		end;
		THROW 50000, @ErrorMessage ,1;
	END
	
	--exec JEEVES_Add_Column 'q_zu_portal_sales_list','sprakkod','integer'
	INSERT INTO q_zu_portal_sales_list VALUES (@Id, @ForetagKod, @OrderNr, @EmailAddress, 0, GETDATE()
	,@c_perssign2, @c_perssign, @c_sprakkod);
	
	if @c_sprakkod<>0
	begin
		SET @Subject = 'Approval of sales order: ' +CAST(@OrderNr AS NVARCHAR(255));
		SET @Body = '<html><body><p>You have a new sales order to approve.</p>';
		SET @Body = @Body + '<table><tr><td>Salesperson:</td><td>' +@Purchaser+ '</td></tr>';
		SET @Body = @Body + '<tr><td>Sales order:</td><td>' +CAST(@OrderNr AS nvarchar(255))+  '</td></tr>';
		SET @Body = @Body + '<tr><td>Customer:</td><td>' +@Supplier+ '</td></tr></table>';
		--SET @Body = @Body + '<p><a href="http://portal.zeeu.se/sales/details/' +CAST(@CompanyId AS NVARCHAR(255))+ '//' + CAST(@Id AS nvarchar(255)) + '/en' +'">Approve sales order</a></p>';
		--SET @Body = @Body + '<p><a href="https://zeeuportalv2.azurewebsites.net/WebApproval/SalesApprovalDetails/' +CAST(@CompanyId AS NVARCHAR(255))+ '//' + CAST(@Id AS nvarchar(255)) +'">Approve sales order</a></p>';
		SET @Body = @Body + '<p><a href="https://customer.zeeu.se/WebApproval/SalesApprovalDetails/' +CAST(@CompanyId AS NVARCHAR(255))+ '//' + CAST(@Id AS nvarchar(255)) + '">Approve sales order</a></p>';
		SET @Body = @Body + '</body></html>';
	end
	else
	begin
		SET @Subject = 'Attest av kundorder: ' +CAST(@OrderNr AS NVARCHAR(255));
		SET @Body = '<html><body><p>Du har fått en ny order att attestera.</p>';
		SET @Body = @Body + '<table><tr><td>Säljare:</td><td>' +@Purchaser+ '</td></tr>';
		SET @Body = @Body + '<tr><td>Order:</td><td>' +CAST(@OrderNr AS nvarchar(255))+  '</td></tr>';
		SET @Body = @Body + '<tr><td>Kund:</td><td>' +@Supplier+ '</td></tr></table>';
		--SET @Body = @Body + '<p><a href="http://portal.zeeu.se/sales/details/' +CAST(@CompanyId AS NVARCHAR(255))+ '//' + CAST(@Id AS nvarchar(255)) + '/sv' +'">Attestera kundordern</a></p>';
		--SET @Body = @Body + '<p><a href="https://zeeuportalv2.azurewebsites.net/WebApproval/SalesApprovalDetails/' +CAST(@CompanyId AS NVARCHAR(255))+ '//' + CAST(@Id AS nvarchar(255)) +'">Attestera kundordern</a></p>';
		SET @Body = @Body + '<p><a href="https://customer.zeeu.se/WebApproval/SalesApprovalDetails/' +CAST(@CompanyId AS NVARCHAR(255))+ '//' + CAST(@Id AS nvarchar(255)) + '">Attestera kundordern</a></p>';
		SET @Body = @Body + '</body></html>';
	end

	-- Skicka e-post.
	EXEC msdb.dbo.sp_send_dbmail
		 @profile_name = @jvss1,
		 @recipients = @EmailAddress,
		 @subject = @Subject,
		 @body = @Body,
		 @body_format = 'HTML';
		 
END

--select * from q_zu_Portal_Consent_Setting
GO


