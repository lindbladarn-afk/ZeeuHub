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
if not exists(select 1 from sys.objects where name like 'q_zu_CustomerPortal_prl_mail')
begin
	exec('create proc q_zu_CustomerPortal_prl_mail as select 123')
end
go
ALTER proc [dbo].[q_zu_CustomerPortal_prl_mail]
as
--zuorn 2024
declare
	@ForetagKod smallint
,	@perssign2 nvarchar(60)
,	@body nvarchar(max)
,	@jvss1 nvarchar(max)
,	@EmailAddress nvarchar(512)
,	@Subject nvarchar(512)

declare [cursor] cursor local static forward_only read_only for
select
	q.ForetagKod
,	q.perssign2
from q_zu_CustomerPortal_prl_list q
group by
	q.ForetagKod
,	q.perssign2
open [cursor]
while(1=1)
begin
	fetch next from [cursor] into
	@ForetagKod
,	@perssign2

	if @@fetch_status <> 0
	begin			
		break
	end;

	if exists
	(
		select 1
		from q_zu_CustomerPortal_prl_list q (readuncommitted)
		join prl (readuncommitted) on
			prl.ForetagKod=q.ForetagKod
		and prl.PrisLista=q.prislista
		and prl.ArtNr=q.artnr
		and prl.AltEnhetKod=q.altenhetkod
		and prl.LimitLowAnt=q.limitlowant
		where
			q.ForetagKod=@ForetagKod
		and q.perssign2=@perssign2
		and prl.q_zu_approval_status=3
	)
	begin
		set @body = 
		cast
		(
		(
		select td = 
			PrisLista + '</td><td>' +
			ArtNr + '</td><td>' +
			LimitLowAnt + '</td><td>' +
			vb_PrisNytt + '</td><td>' +
			vb_PrisNyttDatum + '</td><td>' +
			perssign
		from
		(
			select 
				cast(prl.PrisLista as nvarchar(30)) as PrisLista
			,	prl.ArtNr
			,	cast(prl.LimitLowAnt as nvarchar(30)) as LimitLowAnt
			,	cast(prl.vb_PrisNytt as nvarchar(30)) as vb_PrisNytt
			,	convert(nchar(8),prl.vb_PrisNyttDatum,112) as vb_PrisNyttDatum
			,	q.perssign
			from q_zu_CustomerPortal_prl_list q (readuncommitted)
			join prl (readuncommitted) on
				prl.ForetagKod=q.ForetagKod
			and prl.PrisLista=q.prislista
			and prl.ArtNr=q.artnr
			and prl.AltEnhetKod=q.altenhetkod
			and prl.LimitLowAnt=q.limitlowant
			where
				q.ForetagKod=@ForetagKod
			and q.perssign2=@perssign2
			and prl.q_zu_approval_status=3
		) as d
		order by
			d.PrisLista
		,	d.ArtNr
		,	d.LimitLowAnt
		for xml path( 'tr' ), type 
		) as nvarchar(max) 
		)

		set @body = '<table cellpadding="2" cellspacing="2" border="1">'
				  + '<tr><th>Price list</th><th>Item</th><th>Qty limit</th><th>New price</th><th>New price fr date</th><th>Signature</th></tr>'
				  + replace( replace( @body, '&lt;', '<' ), '&gt;', '>' )
				  + '</table>'
		set @Body = @Body + '<p><a href="https://customer.zeeu.se/WebApproval/PriceListApprovalDetails/' + /* CAST(@CompanyId AS NVARCHAR(255))+ '//' + CAST(@NewId AS nvarchar(255)) +*/ '">Approve sales price list</a></p>';

		set @Subject = 'Approval of sales price lists'
		select @jvss1 = dbo.jeeves_fn_getparam(@ForetagKod,N'custom_portal_1',N'jeevesparamstring','') --dbmail

		select @EmailAddress=pr.wEmailAddress
		from pr (readuncommitted)
		where
			pr.ForetagKod=@ForetagKod
		and pr.PersSign=@perssign2
	
		exec msdb.dbo.sp_send_dbmail
			@profile_name = @jvss1
		,	@recipients = @EmailAddress
		,	@subject = @Subject
		,	@body = @Body
		,	@body_format = 'HTML'
	end
end
close [cursor]
deallocate [cursor]

--Överför värdet i kolumnen vb_prisnytt -> vb_pris för de poster där vb_prisnyttdatum <= dagens datum
declare
	@prl_ArtNr jeeves_itemno
,	@prl_AltEnhetKod nvarchar(20)
,	@prl_PrisLista smallint
,	@prl_LimitLowAnt float
,	@prl_vb_PrisNytt money

declare [cursor] cursor local static forward_only read_only for
select
	prl.ArtNr
,	prl.AltEnhetKod
,	prl.PrisLista
,	prl.LimitLowAnt
,	prl.ForetagKod
,	prl.vb_PrisNytt
from prl (readuncommitted)
where
	prl.vb_PrisNytt is not null
and prl.vb_PrisNyttDatum is not null
and prl.vb_PrisNyttDatum <= cast(getdate() as date)

open [cursor]
while(1=1)
begin
	fetch next from [cursor] into 
	@prl_ArtNr
,	@prl_AltEnhetKod
,	@prl_PrisLista
,	@prl_LimitLowAnt
,	@ForetagKod
,	@prl_vb_PrisNytt

	if @@fetch_status <> 0
	begin			
		break
	end;

	update prl
	set 
		prl.vb_pris = @prl_vb_PrisNytt
	,	prl.vb_PrisNytt = null
	,	prl.vb_PrisNyttDatum = null
	where
		prl.ArtNr=@prl_ArtNr
	and prl.AltEnhetKod=@prl_AltEnhetKod
	and	prl.PrisLista=@prl_PrisLista
	and	prl.LimitLowAnt=@prl_LimitLowAnt
	and	prl.ForetagKod=@ForetagKod

end
close [cursor]
deallocate [cursor]
