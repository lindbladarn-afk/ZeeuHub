if exists(select 1 from sys.tables where name like 'q_zu_CustomerPortal_prl_list')
begin
	drop table q_zu_CustomerPortal_prl_list
end
go
CREATE TABLE [dbo].[q_zu_CustomerPortal_prl_list](
	[Id] [uniqueidentifier] NOT NULL,
	[ForetagKod] [smallint] NOT NULL,
	[prislista] [int] NOT NULL,
	[artnr] [dbo].[JEEVES_ItemNo] NOT NULL,
	[altenhetkod] [nvarchar](20) NOT NULL,
	[limitlowant] [float] NOT NULL,
	[EmailAddress] [nvarchar](512) NOT NULL,
	[EmailSent] [int] NULL,
	[RowCreatedDt] [datetime] NOT NULL,
	[perssign2] [nvarchar](60) NULL,
	[perssign] [nvarchar](60) NULL,
	[sprakkod] [int] NULL,
	[Aktiv] [smallint] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[q_zu_CustomerPortal_prl_list] ADD  DEFAULT (newid()) FOR [Id]
GO