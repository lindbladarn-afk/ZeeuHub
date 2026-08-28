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
if not exists(select 1 from sys.objects where name like 'q_zu_CustomerPortal_so')
begin
	exec('create proc q_zu_CustomerPortal_so as select 123')
end
go
ALTER proc [dbo].[q_zu_CustomerPortal_so]
(
	@c_foretagkod smallint
,	@c_ordernr jeeves_sono
,	@c_approval_flowid smallint
,	@c_perssign nvarchar(60)
,	@c_sprakkod smallint = 0
,	@Id uniqueidentifier = NULL
) 
as
--zuorn 2024
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

if dbo.Jeeves_FN_GetParam(@c_foretagkod,N'custom_portal_3','JeevesParamBoolean',N'0')=N'0' return 0;

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

,	@ordsum money
,	@attestsign nvarchar(60)
,	@attestlimit money
,	@attestlimit_minus money
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

--kontroll på ordertyp samt perssign2
select 
	@attestsign =q.attestsign
,	@attestlimit =q.attestlimit
,	@attestlimit_minus = q.q_zu_attestlimit_2
,	@ordsum =oh.OrdSum
from q_zu_approval_chains q (readuncommitted)
join oh (readuncommitted) on
	oh.ForetagKod=q.ForetagKod
and oh.OrdTyp=q.ordtyp
join pr (readuncommitted) on
	pr.ForetagKod=q.ForetagKod
and pr.PersSign=q.attestsign
where
	q.ForetagKod = @c_foretagkod
and q.q_zu_approval_flowid = @c_approval_flowid
and q.perssign2=@c_perssign
and oh.OrderNr=@c_ordernr
select @dbc=@@RowCount

--kontroll enbart på default - struntar i perssign2
if @dbc = 0
begin
	select top(1)
		@attestsign =q.attestsign
	,	@attestlimit =q.attestlimit
	,	@attestlimit_minus = q.q_zu_attestlimit_2
	,	@ordsum =oh.OrdSum
	from q_zu_approval_chains q (readuncommitted)
	join oh (readuncommitted) on
		oh.ForetagKod=q.ForetagKod
	join pr (readuncommitted) on
		pr.ForetagKod=q.ForetagKod
	and pr.PersSign=q.attestsign
	where
		q.ForetagKod = @c_foretagkod
	and q.q_zu_approval_flowid = @c_approval_flowid
	and q.q_zu_approval_default = N'1'
	and oh.OrderNr=@c_ordernr
	select @dbc=@@RowCount
end

if @attestlimit_minus>0 set @attestlimit_minus=-@attestlimit_minus

if ((@ordsum>0 and @attestlimit <= @ordsum and @attestlimit<>0) or (@ordsum<0 and @attestlimit_minus >= @ordsum and @attestlimit_minus<>0)) and @attestsign<>@c_perssign
begin
	exec q_zu_CustomerPortal_Sales_Add
		@ForetagKod=@c_foretagkod
	,	@OrderNr=@c_ordernr
	,	@c_perssign2=@attestsign
	,	@c_perssign=@c_perssign
	,	@c_sprakkod=@c_sprakkod

	if @Id is not null -- Dokumentet gick nu vidare till en attestant, flagga den gamla attestposten som inaktiv, om det fanns en.
	begin
		update q_zu_CustomerPortal_sales_list set Aktiv=0 where Id = @Id
	end

	update oh set 
		oh.q_zu_approval_status=3
	,	No_Trigger_Exec=1-No_Trigger_Exec
	where 
		ForetagKod=@c_foretagkod 
	and OrderNr=@c_ordernr;
end
else
begin
	if @dbc<>0
	begin
		update oh set 
			oh.q_zu_approval_status=1
		,	oh.q_zu_approval_approvedby=@c_perssign
		,	oh.q_zu_approval_approveddt=cast(getdate() as date) 
		,	oh.No_Trigger_Exec=1-oh.No_Trigger_Exec
		where 
			oh.ForetagKod=@c_foretagkod 
		and oh.OrderNr=@c_ordernr;

		if @Id is not null -- Dokumentet blev nu godkänt av attestanten, flagga attestposten som inaktiv, om det fanns en.
		begin
			update q_zu_CustomerPortal_sales_list set Aktiv=0 where Id = @Id
		end
	end
	else
	begin
		update oh set 
			oh.q_zu_approval_status=0
		,	oh.q_zu_approval_message=N'No Approval chain record exist! Check program q_zu_approval_chains'
		,	oh.No_Trigger_Exec=1-oh.No_Trigger_Exec
		where 
			ForetagKod=@c_foretagkod 
		and OrderNr=@c_ordernr;
	end
end

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
go