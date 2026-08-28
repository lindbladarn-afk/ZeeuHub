IF OBJECT_ID('dbo.q_zu_notcenter_varningstyp', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[q_zu_notcenter_varningstyp](
        [q_zu_notcenter_typ] [nvarchar](35) NOT NULL,
        [q_zu_notcenter_typbeskr] [nvarchar](255) NULL,
        CONSTRAINT [PK_q_zu_notcenter_varningstyp] PRIMARY KEY CLUSTERED ([q_zu_notcenter_typ] ASC)
    );
END
