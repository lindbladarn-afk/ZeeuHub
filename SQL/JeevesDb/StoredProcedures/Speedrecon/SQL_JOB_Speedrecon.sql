--ändra
--@ownder_login_name
--foretagkod på 2 ställen
--@database_name

USE [msdb]
GO

/****** Object:  Job [ZeeU SpeedRecon]    Script Date: 2022-02-03 23:50:50 ******/
BEGIN TRANSACTION
DECLARE @ReturnCode INT
SELECT @ReturnCode = 0
/****** Object:  JobCategory [[Uncategorized (Local)]]    Script Date: 2022-02-03 23:50:51 ******/
IF NOT EXISTS (SELECT name FROM msdb.dbo.syscategories WHERE name=N'[Uncategorized (Local)]' AND category_class=1)
BEGIN
EXEC @ReturnCode = msdb.dbo.sp_add_category @class=N'JOB', @type=N'LOCAL', @name=N'[Uncategorized (Local)]'
IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback

END

DECLARE @jobId BINARY(16)
EXEC @ReturnCode =  msdb.dbo.sp_add_job @job_name=N'ZeeU SpeedRecon',
		@enabled=1,
		@notify_level_eventlog=0,
		@notify_level_email=0,
		@notify_level_netsend=0,
		@notify_level_page=0,
		@delete_level=0,
		@description=N'No description available.',
		@category_name=N'[Uncategorized (Local)]',
		@owner_login_name=N'JvsDBO', @job_id = @jobId OUTPUT
IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback
/****** Object:  Step [Exekvera procedure]    Script Date: 2022-02-03 23:50:52 ******/
EXEC @ReturnCode = msdb.dbo.sp_add_jobstep @job_id=@jobId, @step_name=N'Exekvera procedure',
		@step_id=1,
		@cmdexec_success_code=0,
		@on_success_action=1,
		@on_success_step_id=0,
		@on_fail_action=2,
		@on_fail_step_id=0,
		@retry_attempts=0,
		@retry_interval=0,
		@os_run_priority=0, @subsystem=N'TSQL',
		@command=N'
declare
	@foretagkod smallint,
	@perssign  nvarchar (30),
	@q_zu_speedrecon_recondate datetime

set nocount on
if exists (select  1 from q_zu_speedrecon where q_zu_speedrecon_execdate = convert(varchar,getdate(),112) and foretagkod = 100)
BEGIN
	Declare crsData cursor Fast_forward for
	select foretagkod,perssign,convert(varchar,q_zu_speedrecon_recondate,112)
	from q_zu_speedrecon
	where foretagkod = 100
	and q_zu_speedrecon_execdate = convert(varchar,getdate(),112)
  open crsData
  fetch next from crsData into @foretagkod,@perssign, @q_zu_speedrecon_recondate
  While @@fetch_status=0
  begin

	exec q_zu_speedrecon_START @foretagkod, @perssign, @q_zu_speedrecon_recondate, 1
	fetch next from crsData into @foretagkod,@perssign, @q_zu_speedrecon_recondate
  End
  Close crsData deallocate crsData

END',
		@database_name=N'Apack',
		@flags=0
IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback
EXEC @ReturnCode = msdb.dbo.sp_update_job @job_id = @jobId, @start_step_id = 1
IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback
EXEC @ReturnCode = msdb.dbo.sp_add_jobschedule @job_id=@jobId, @name=N'Every day at 23:00',
		@enabled=1,
		@freq_type=4,
		@freq_interval=1,
		@freq_subday_type=1,
		@freq_subday_interval=0,
		@freq_relative_interval=0,
		@freq_recurrence_factor=0,
		@active_start_date=20190130,
		@active_end_date=99991231,
		@active_start_time=230000,
		@active_end_time=235959,
		@schedule_uid=N'4c5559c5-b3c8-49bb-a359-94c33c62a04b'
IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback
EXEC @ReturnCode = msdb.dbo.sp_add_jobserver @job_id = @jobId, @server_name = N'(local)'
IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback
COMMIT TRANSACTION
GOTO EndSave
QuitWithRollback:
    IF (@@TRANCOUNT > 0) ROLLBACK TRANSACTION
EndSave:
GO
