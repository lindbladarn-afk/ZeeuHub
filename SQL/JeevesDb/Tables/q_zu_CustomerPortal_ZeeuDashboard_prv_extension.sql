CREATE TABLE [dbo].[q_zu_CustomerPortal_ZeeuDashboard_prv_extension]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY, 
    [PersSign2] NVARCHAR(30) NOT NULL, 
    [ForetagKod] SMALLINT NOT NULL, 
    [NextWorkOrder] NVARCHAR(50) NULL, 
    [NextProductionGroup] NVARCHAR(50) NULL
)
