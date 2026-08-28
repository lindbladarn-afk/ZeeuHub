-- =============================================
-- Author:		Daniel Mellqvist, ZeeU AB
-- Create date: 2021-12-15
-- Called by:	ZeeU.CustomerPortal
-- Description:	SP to get all the approvals for the company
-- =============================================
CREATE PROCEDURE [dbo].[q_zu_CustomerPortal_WebApprovalSales]
	@SelectStatement	NVARCHAR(100)
	,@EmailAddress		NVARCHAR(512)		=NULL
	,@Status			INT					=NULL
	,@ForetagKod		SMALLINT			=NULL
	,@CompanyId			UNIQUEIDENTIFIER	=NULL
	,@ApprovedBy		NVARCHAR(60)		=NULL
	,@AttestStatus		NVARCHAR(250)		=NULL
	,@Message			NVARCHAR(MAX)		=NULL
	,@Id				UNIQUEIDENTIFIER	=NULL
	,@ErrorMessage		NVARCHAR(MAX)		=NULL
	,@c_perssign2		nvarchar(60)		=NULL
	,@c_perssign		nvarchar(60)		=NULL
	,@c_sprakkod		int					=0

AS

IF (@SelectStatement = 'GetWebApprovalOrders')
BEGIN
	SELECT a.Id
		,a.aktiv							AS IsActive
		,a.PersSign2						AS AttestantPersSign
		,pr.RespNamn						AS AttestantName
		,sy2.RespNamn						AS SalesReference
		,x6.ordtypbeskr						AS OrderType
		,x7.ordstatbeskr					AS OrderStatusDescription
		,oh.OrderNr							AS OrderNr
		,oh.OrdStat							AS OrderStatus
		,oh.ftgnr							AS FtgNr
		,fr.ftgnamn							AS FtgNamn
		,oh.regdat							AS RegDat
		,oh.OrdBerLevDat					AS OrdBerLevDat
		,oh.valkod							AS ValKod
		,oh.VbOrdSum						AS VbOrdSum
		,oh.OrdSum							AS OrdSum
		,a.sprakkod							AS SprakKod
		,oh.q_zu_approval_approvedby		AS ApprovedBy
		,oh.q_zu_approval_approveddt		AS ApprovedDate
		,ISNULL(oh.q_zu_approval_status,3)	AS ApprovalStatus
		--,ISNULL(oh.q_zu_approval_status,0)	AS ApprovalStatus
	FROM q_zu_CustomerPortal_sales_list a
		JOIN oh (NOLOCK) ON oh.ForetagKod=a.ForetagKod
			AND oh.OrderNr=a.ordernr
		JOIN fr (NOLOCK) on oh.foretagkod=fr.foretagkod 
			AND oh.ftgnr=fr.ftgnr
		LEFT JOIN pr (READUNCOMMITTED) ON
			pr.ForetagKod=a.ForetagKod
		AND pr.PersSign=a.perssign2
		LEFT JOIN sy2 (READUNCOMMITTED) ON
			sy2.PersSign=oh.RowCreatedBy
		LEFT JOIN x6 (NOLOCK) ON oh.ForetagKod = x6.ForetagKod
			and oh.OrdTyp = x6.OrdTyp
		LEFT JOIN x7 (NOLOCK) ON oh.ForetagKod = x7.ForetagKod
			AND oh.OrdStat = x7.OrdStat
	WHERE a.ForetagKod=@ForetagKod and (oh.q_zu_approval_status=@Status or @Status is null)
	ORDER BY oh.OrderNr ASC
END


IF (@SelectStatement = 'GetWebApprovalOrderWithRows')
BEGIN
	SELECT a.Id
		,a.aktiv							AS IsActive
		,a.PersSign2						AS AttestantPersSign
		,pr.RespNamn						AS AttestantName
		,x6.ordtypbeskr						AS OrderType
		,x7.ordstatbeskr					AS OrderStatusDescription
		,sy2.RespNamn						AS SalesReference
		,oh.kundref2						AS CustomerReference
		,oh.OrderNr							AS OrderNumber
		,oh.OrdStat							AS OrderStatus
		,oh.FtgNr							AS FtgNr
		,fr.ftgnamn							AS FtgNamn
		,oh.delivaddr2						AS DeliveryAddress2
		,oh.delivaddr3						AS DeliveryAddress3
		,oh.delivaddr4						AS DeliveryAddress4
		,oh.RegDat							AS RegDat
		,oh.OrdBerLevDat					AS OrdBerLevDat 
		,oh.ValKod							AS ValKod
		,oh.VbOrdSum						AS VbOrdSum
		,oh.OrdSum							AS OrdSum
		,oh.q_zu_approval_approvedby		AS ApprovedBy
		,oh.q_zu_approval_approveddt		AS ApprovedDate
		,oh.q_oh_anteckning					AS Xvivo_q_oh_anteckning	--Unique field for Xvivo
		,a.sprakkod							AS SprakKod
		,ISNULL(oh.q_zu_approval_status,0)	AS ApprovalStatus
	FROM q_zu_CustomerPortal_sales_list a
		JOIN oh (NOLOCK) ON oh.ForetagKod=a.ForetagKod
			AND oh.OrderNr=a.OrderNr
		JOIN fr (NOLOCK) on oh.foretagkod=fr.foretagkod 
			AND oh.ftgnr=fr.ftgnr
		LEFT JOIN pr (readuncommitted) on
			pr.ForetagKod=a.ForetagKod
		AND pr.PersSign=a.perssign2
		LEFT JOIN sy2 (readuncommitted) on
			sy2.PersSign=oh.RowCreatedBy
		LEFT JOIN x6 (NOLOCK) ON oh.ForetagKod = x6.ForetagKod
			and oh.OrdTyp = x6.OrdTyp
		LEFT JOIN x7 (NOLOCK) ON oh.ForetagKod = x7.ForetagKod
			AND oh.OrdStat = x7.OrdStat
	WHERE a.Id=@Id
	ORDER BY oh.OrderNr ASC

	-- Get all rows 
	SELECT orp.OrdRadNr		AS OrdRadNr
		, orp.ArtNr			AS ArtNr
		, orp.ArtBeskr		AS ArtBeskr
		, orp.OrdAntal		AS OrdAntal
		, orp.OrdBerLevDat	AS OrdBerLevDat
		, orp.vb_pris		AS Vb_Pris
		, orp.ordradrab		AS OrdRadRab
	FROM q_zu_CustomerPortal_sales_list a
		JOIN oh (NOLOCK) ON oh.ForetagKod=a.ForetagKod
			AND oh.OrderNr=a.OrderNr
		JOIN orp (NOLOCK) ON orp.ForetagKod=oh.ForetagKod
			AND orp.OrderNr=oh.OrderNr
	WHERE a.Id=@Id
	ORDER BY orp.OrdRadNr
END


IF (@SelectStatement = 'UpdateWebApprovalOrder')
BEGIN
	DECLARE @OrderNr		BIGINT
			,@PersSign2		NVARCHAR(60)

	SELECT @OrderNr		= a.OrderNr
		,@ForetagKod	= a.ForetagKod
		,@PersSign2		= a.perssign2
	FROM q_zu_CustomerPortal_sales_list a
	WHERE a.Id=@Id;

	-- Update the order status
	UPDATE oh SET 
		oh.q_zu_approval_message	= @Message
	WHERE 
		ForetagKod					= @ForetagKod 
		AND OrderNr					= @OrderNr;

	IF NOT @AttestStatus IN(N'2')
	BEGIN
		EXEC q_zu_CustomerPortal_so
			@c_foretagkod		= @ForetagKod
			,@c_ordernr			= @OrderNr
			,@c_approval_flowid	= 1 --kundorder
			,@c_perssign		= @ApprovedBy
			,@c_sprakkod		= 0
			,@Id=@Id
	END
	ELSE
	BEGIN
		UPDATE oh SET
			oh.q_zu_approval_status			= 2
			,oh.q_zu_approval_approvedby	= @ApprovedBy
			,oh.q_zu_approval_approveddt	= GETDATE()
		WHERE oh.ForetagKod					= @ForetagKod
			AND oh.OrderNr					= @OrderNr;


		update q_zu_CustomerPortal_sales_list set Aktiv = 0 where Id = @Id;
	END
END