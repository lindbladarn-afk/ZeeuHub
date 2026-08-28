CREATE TABLE [dbo].[q_zu_CustomerPortal_sales_list](
	[Id] [uniqueidentifier] NOT NULL,
	[ForetagKod] [smallint] NOT NULL,
	[OrderNr] [nvarchar](50) NOT NULL,
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

ALTER TABLE [dbo].[q_zu_CustomerPortal_sales_list] ADD  DEFAULT (newid()) FOR [Id]
GO