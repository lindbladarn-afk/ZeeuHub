
/****** Object:  StoredProcedure [dbo].[q_zu_speedrecon_createyear]    Script Date: 6/9/2022 9:35:09 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE Procedure [dbo].[q_zu_speedrecon_createyear]
@Foretagkod smallint,
@sign nvarchar(30),
@redovisnar int

WITH ENCRYPTION

AS
declare
	@q_zu_speedrecon_execdate datetime
,	@q_zu_speedrecon_recondate datetime

declare [cursor] cursor local static forward_only read_only for
select
	dateadd(day, 1, convert(date, periodend)) as q_zu_speedrecon_execdate
,	convert(date, periodend) as q_zu_speedrecon_recondate
from per (readuncommitted)
where
	foretagkod = @foretagkod
and redovisnar = @redovisnar
and bokslutsperiod = 0
open [cursor]
while(1=1)
begin
	fetch next from [cursor] into
		@q_zu_speedrecon_execdate
	,	@q_zu_speedrecon_recondate
	if @@fetch_status <> 0
	begin
		break
	end;

	if not exists
	(
	select 1 from q_zu_speedrecon (readuncommitted)
	where
		foretagkod=@Foretagkod
	and PersSign=@sign
	and q_zu_speedrecon_recondate=@q_zu_speedrecon_recondate
	)
	begin
		INSERT INTO q_zu_speedrecon
			(foretagkod,
			q_zu_speedrecon_execdate,
			q_zu_speedrecon_recondate,
			PersSign,
			RowCreatedBy,
			RowCreatedDt,
			q_zu_speedrecon_kundresk,
			q_zu_speedrecon_levresk,
			q_zu_speedrecon_intlevresk,
			q_zu_speedrecon_per,
			q_zu_speedrecon_anlag,
			q_zu_speedrecon_lager,
			q_zu_speedrecon_orderunikt,
			q_zu_speedrecon_inlevejfakt,
			q_zu_speedrecon_utlevejfakt,
			q_zu_speedrecon_lagerflytt,
			q_zu_speedrecon_pia)
		select
			@Foretagkod,
			@q_zu_speedrecon_execdate,
			@q_zu_speedrecon_recondate,
			@sign,
			@sign,
			GETDATE(),
			'1',
			'1',
			'1',
			'1',
			'1',
			'1',
			'1',
			'1',
			'1',
			'1',
			'1'
		end
end
close [cursor]
deallocate [cursor]

/* gammal set baserad insert, går ej att kombinera med if exists
INSERT INTO q_zu_speedrecon
	(foretagkod,
	q_zu_speedrecon_execdate,
	q_zu_speedrecon_recondate,
	PersSign,
	RowCreatedBy,
	RowCreatedDt,
	q_zu_speedrecon_kundresk,
	q_zu_speedrecon_levresk,
	q_zu_speedrecon_intlevresk,
	q_zu_speedrecon_per,
	q_zu_speedrecon_anlag,
	q_zu_speedrecon_lager,
	q_zu_speedrecon_orderunikt,
	q_zu_speedrecon_inlevejfakt,
	q_zu_speedrecon_utlevejfakt,
	q_zu_speedrecon_lagerflytt,
	q_zu_speedrecon_pia)
select
	@Foretagkod,
	DATEADD(day, 1, CONVERT(DATE, PeriodEnd)),
	CONVERT(DATE, PeriodEnd),
	@sign,
	@sign,
	GETDATE(),
	'1',
	'1',
	'1',
	'1',
	'1',
	'1',
	'1',
	'1',
	'1',
	'1',
	'1'
-- Date
from per where
	 foretagkod = @Foretagkod and
	 Redovisnar = (select (YEAR(MAX(q_zu_speedrecon.q_zu_speedrecon_recondate))+1) from q_zu_speedrecon) and
	 Bokslutsperiod = 0
*/

go