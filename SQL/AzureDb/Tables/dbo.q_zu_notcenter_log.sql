IF OBJECT_ID('dbo.q_zu_notcenter_log', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[q_zu_notcenter_log](
        [q_zu_notcenter_log_id] [bigint] IDENTITY(1,1) NOT NULL,
        [foretagkod] [smallint] NOT NULL,
        [q_zu_notcenter_nr] [int] NOT NULL,
        [regdat] [datetime] NULL,
        [q_zu_notcenter_beskrivning] [nvarchar](255) NULL,
        [q_zu_notcenter_typ] [nvarchar](35) NULL,
        [q_zu_notcenter_prio] [nvarchar](35) NULL,
        [q_zu_notcenter_varntext] [nvarchar](255) NULL,
        [q_zu_notcenter_kommentar] [nvarchar](255) NULL,
        [q_zu_notcenter_mailadress1] [nvarchar](255) NULL,
        [q_zu_notcenter_mailadress2] [nvarchar](255) NULL,
        [q_zu_notcenter_cc] [nvarchar](255) NULL,
        [q_zu_notcenter_bcc] [nvarchar](255) NULL,
        [q_zu_notcenter_schema] [nvarchar](35) NULL,
        [q_zu_notcenter_in_use] [char](1) NULL,
        [q_zu_notcenter_antvarning] [int] NULL,
        [q_zu_notcenter_execdat] [datetime] NULL,
        [q_zu_notcenter_varndat] [datetime] NULL,
        [q_zu_notcenter_select2] [nvarchar](max) NULL,
        [q_zu_notcenter_sysl] [nvarchar](50) NULL,
        [q_zu_notcenter_startdat] [datetime] NULL,
        [q_zu_notcenter_schedule] [nvarchar](30) NULL,
        [q_zu_notcenter_recipients] [nvarchar](255) NULL,
        [q_zu_notcenter_subject] [nvarchar](255) NULL,
        [q_zu_notcenter_html] [nvarchar](max) NULL,
        CONSTRAINT [PK_q_zu_notcenter_log] PRIMARY KEY CLUSTERED ([q_zu_notcenter_log_id] ASC)
    );

    CREATE INDEX [IX_q_zu_notcenter_log_company_notification_regdat]
        ON [dbo].[q_zu_notcenter_log]([foretagkod], [q_zu_notcenter_nr], [regdat] DESC);
END
