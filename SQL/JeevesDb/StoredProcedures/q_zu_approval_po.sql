
SET ANSI_NULLS OFF
GO
SET QUOTED_IDENTIFIER OFF
GO

create proc [dbo].[q_zu_approval_po]
(
	@c_foretagkod smallint
,	@c_bestnr jeeves_pono
,	@c_approval_flowid smallint
,	@c_perssign nvarchar(60)
,	@c_sprakkod smallint = 0
) 
as

declare 
	@enter_trancount integer
,	@catch_error_number integer
,	@catch_error_message Jeeves_StrVarChar4000

set @enter_trancount = @@trancount

set rowcount 0;
set nocount on;
 
if @@options & 8    = 8 set ansi_warnings off 
if @@options & 16   = 16 set ansi_padding off 
if @@options & 64   = 64 set arithabort off 
if @@options & 1024 = 1024 set ansi_null_dflt_on off 
if @@options & 2048 = 2048 set ansi_null_dflt_off off 
if @@options & 4096 = 4096 set concat_null_yields_null off 
if @@options & 8192 = 8192 set numeric_roundabort off 
if @@options & 32   = 32 set ansi_nulls off 
if @@options & 256  = 256 set quoted_identifier off

if dbo.Jeeves_FN_GetParam(@c_foretagkod,N'custom_portal_4','JeevesParamBoolean',N'0')=N'0' return 0;

begin try

if @enter_trancount = 0 begin tran

declare
	@dbc integer
,	@dbe integer
,	@dbp integer = @@procid
,	@Procedure_Name sysName = object_name( @@procid )
,	@NewLine Jeeves_StrVarChar2 = nchar(13)+nchar(10)
,	@NewLineTab Jeeves_StrVarChar3 = nchar(13)+nchar(10)+nchar(9)
,	@wr Jeeves_StrVarChar4000
,	@timestamp datetime = getdate()

,	@bestvalue money
,	@attestsign nvarchar(60)
,	@attestlimit money
,	@subject nvarchar(max)
,	@body nvarchar(max)

select @dbc=count(1)
from pr (readuncommitted)
where
	pr.ForetagKod=@c_foretagkod
and	pr.PersSign like @c_perssign
select /*@dbc=@@RowCount,*/ @dbe=@@Error
if @dbc<>1 or @dbe<>0
begin
	while @@TranCount > @Enter_TranCount ROLLBACK TRANSACTION;
	while @@TranCount < @Enter_TranCount BEGIN TRANSACTION;
	set @wr = @Procedure_Name+'; pr; Invalid signature <'+ISNULL(convert(varchar,@c_perssign),'NULL')+'>';
	raiserror(@wr, 16, 1)
	return -101;
end

select @dbc=count(1)
from sy1 (readuncommitted)
where
	sy1.ForetagKod = @c_foretagkod
select @dbe=@@Error
if @dbc<>1 or @dbe<>0
begin
	while @@TranCount > @Enter_TranCount ROLLBACK TRANSACTION;
	while @@TranCount < @Enter_TranCount BEGIN TRANSACTION;
	set @wr = @Procedure_Name+'; sy1; Invalid my_own_companycode <'+ISNULL(convert(varchar,@c_foretagkod),'NULL')+'>';
	raiserror(@wr, 16, 1)
	return -101;
end;

select 
	@attestsign =q.attestsign
,	@attestlimit =q.attestlimit
--,	@bestvalue =bh.bestvalue
,	@bestvalue =abs(bh.bestvalue) --zuorn 230309
from q_zu_approval_chains q (readuncommitted)
join bh (readuncommitted) on
	bh.ForetagKod=q.ForetagKod
and bh.BestTyp=q.besttyp
join pr (readuncommitted) on
	pr.ForetagKod=q.ForetagKod
and pr.PersSign=q.attestsign
where
	q.ForetagKod = @c_foretagkod
and q.q_zu_approval_flowid = @c_approval_flowid
and q.perssign2=@c_perssign
and bh.BestNr=@c_bestnr
select @dbc=@@RowCount

if @dbc=0
begin
	select top(1)
		@attestsign =q.attestsign
	,	@attestlimit =q.attestlimit
	--,	@bestvalue =bh.bestvalue
	,	@bestvalue =abs(bh.bestvalue) --zuorn 230309
	from q_zu_approval_chains q (readuncommitted)
	join bh (readuncommitted) on
		bh.ForetagKod=q.ForetagKod
	--and bh.BestTyp=q.besttyp
	join pr (readuncommitted) on
		pr.ForetagKod=q.ForetagKod
	and pr.PersSign=q.attestsign
	where
		q.ForetagKod = @c_foretagkod
	and q.q_zu_approval_flowid = @c_approval_flowid
	and q.q_zu_approval_default = N'1'
	and bh.BestNr=@c_bestnr
	select @dbc=@@RowCount
end;


if @attestlimit<@bestvalue and @attestsign<>@c_perssign
begin
	exec q_zu_Portal_Purchase_Add
		@ForetagKod=@c_foretagkod
	,	@BestNr=@c_bestnr
	,	@c_perssign2=@attestsign
	,	@c_perssign=@c_perssign
	,	@c_sprakkod=@c_sprakkod

	update bh set 
		bh.q_zu_approval_status=N'0' 
	,   bh.q_zu_approval_date = CONVERT(CHAR(8),GetDate(),112)+SPACE(1)+CONVERT(CHAR(5),GetDate(),108) --ZuKge 2023-01-24
	,	No_Trigger_Exec=1-No_Trigger_Exec
	where 
		ForetagKod=@c_foretagkod 
	and BestNr=@c_bestnr;
end
else
begin
	if @dbc<>0
	begin
		update bh set 
			bh.q_zu_approval_status=N'1'
		,   bh.q_zu_approval_date = CONVERT(CHAR(8),GetDate(),112)+SPACE(1)+CONVERT(CHAR(5),GetDate(),108) --ZuKge 2023-01-24
		,	bh.No_Trigger_Exec=1-No_Trigger_Exec
		where 
			ForetagKod=@c_foretagkod 
		and BestNr=@c_bestnr;
	end
	else
	begin
		update bh set 
			bh.q_zu_approval_status=N'0'
		,	bh.q_zu_approval_message=N'No Approval chain record exist! Check program q_zu_approval_chains'
		,	bh.No_Trigger_Exec=1-No_Trigger_Exec
		where 
			ForetagKod=@c_foretagkod 
		and BestNr=@c_bestnr;
	end
end

--set @subject='PO '+cast(@c_bestnr as nvarchar(20))+' pending approval'
--set @body=N'Use the link below to approve this PO'+@NewLineTab
--set @body=@body+N'http://portal.zeeu.se/purchase/attestorder/'+cast(isnull(@c_foretagkod,0) as nvarchar(8))+'/'+cast(isnull(@c_bestnr,0) as nvarchar(40))+@NewLine+@NewLine
--set @body=@body+N'<table>'
--+N'<tr><th>Row</th><th>Signature</th><th>Value</th></tr>'+
--cast
--(
--	(
--		select 
--			bp.BestRadNr as td
--		,	@c_perssign as td
--		,	bp.BestValue as td
--		from bp (readuncommitted)
--		where
--			bp.ForetagKod=@c_foretagkod 
--		and bp.BestNr=@c_bestnr
--		and bp.BestStatKod=9
--		for xml raw('tr'), elements
--	) as nvarchar(max)
--)+N'</table>'

--exec msdb.dbo.sp_send_dbmail
--	@profile_name='Zeeu Gmail'
--,	@recipients=@wemailaddress
--,	@subject=@subject
--,	@body=@body
--,	@body_format='html'

while @@TranCount > @Enter_TranCount COMMIT TRANSACTION;
while @@TranCount > @Enter_TranCount ROLLBACK TRANSACTION;
while @@TranCount < @Enter_TranCount BEGIN TRANSACTION

end try
begin catch
	if @@TranCount > 0 ROLLBACK TRANSACTION;
	execute Jeeves_Catch_SPR_Error @catch_ERROR_NUMBER OUTPUT, @catch_ERROR_MESSAGE OUTPUT, @Procedure_Name, null, null, @c_foretagkod, @c_perssign, @c_sprakkod, NULL;
	while @@TranCount < @Enter_TranCount BEGIN TRANSACTION;
	Execute Jeeves_RaisError @catch_ERROR_NUMBER, @catch_ERROR_MESSAGE;
	return -100;
end catch;
