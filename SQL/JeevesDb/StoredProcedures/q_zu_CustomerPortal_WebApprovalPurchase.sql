-- =============================================
-- Author:		Daniel Mellqvist, ZeeU AB
-- Create date: 2021-12-29
-- Called by:	ZeeU.CustomerPortal
-- Description:	SP to get all the purchase approval orders
-- =============================================
CREATE PROCEDURE [dbo].[q_zu_CustomerPortal_WebApprovalPurchase]
	@SelectStatement	NVARCHAR(100)
	,@Status			INT					=NULL
	,@BestNr			BIGINT				=NULL
	,@ForetagKod		SMALLINT			=NULL
	,@CompanyId			UNIQUEIDENTIFIER	=NULL
	,@ApprovedBy		NVARCHAR(60)		=NULL
	,@PersSign2			NVARCHAR(60)		=NULL
	,@EmailAddress		NVARCHAR(500)		=NULL
	,@AttestStatus		NVARCHAR(250)		=NULL
	,@Message			NVARCHAR(MAX)		=NULL
	,@Id				UNIQUEIDENTIFIER	=NULL
	,@ErrorMessage		NVARCHAR(MAX)		=NULL
	,@c_perssign2		nvarchar(60)		=NULL
	,@c_perssign		nvarchar(60)		=NULL
	,@c_sprakkod		int					=0

AS
IF (@SelectStatement = 'GetWebApprovalPurchaseOrders')
BEGIN
	SELECT	@CompanyId CompanyId
		,a.Id
		,a.aktiv							AS IsActive
		,a.PersSign2						AS PersSign2 -- Skall attestera
		,pr.RespNamn						AS AttestantName -- Personnamn på attestant
		,sy2.RespNamn						AS Vref -- Person som skapat order
		,bh.bestnr							AS BestNr
		,bh.ftgnr							AS FtgNr
		,fr.ftgnamn							AS FtgNamn
		,bh.regdat							AS RegDat
		,bh.bestberlevdat					AS BestBerLevDat
		,bh.valkod							AS ValKod
		,bh.VbBestValue						AS VbBestValue
		,bh.BestValue						AS BestValue
		,a.sprakkod							AS SprakKod
		,bh.EditExt							AS EditExt
		,bh.q_zu_approval_approvedby		AS ApprovedBy
		,bh.q_zu_approval_approveddt		AS ApprovedDate
		,ISNULL(bh.q_zu_approval_status,0)	AS ApprovalStatus
	FROM q_zu_CustomerPortal_purchase_list a
		JOIN bh (NOLOCK) ON bh.ForetagKod=a.ForetagKod
			AND bh.BestNr=a.BestNr
		JOIN fr (NOLOCK) on bh.foretagkod=fr.foretagkod 
			AND bh.ftgnr=fr.ftgnr
		LEFT JOIN pr (READUNCOMMITTED) ON
			pr.ForetagKod=a.ForetagKod
		AND pr.PersSign=a.perssign2
		LEFT JOIN sy2 (READUNCOMMITTED) ON
			sy2.PersSign=bh.RowCreatedBy
	WHERE a.ForetagKod=@ForetagKod
	AND (bh.q_zu_approval_status=@status OR @status IS NULL)
	ORDER BY bh.bestnr ASC
END
	
IF (@SelectStatement = 'GetWebApprovalPurchaseOrder')
BEGIN
	SELECT 
		a.Id
		,a.aktiv							AS IsActive
		,a.PersSign2						AS PersSign2
		,pr.RespNamn						AS AttestantName
		,sy2.RespNamn						AS Vref
		,bh.bestnr							AS BestNr
		,bh.ftgnr							AS FtgNr
		,fr.ftgnamn							AS FtgNamn
		,bh.regdat							AS RegDat
		,bh.bestberlevdat					AS BestBerLevDat
		,bh.valkod							AS ValKod
		,bh.VbBestValue						AS VbBestValue
		,bh.BestValue						As BestValue
		,bh.EditExt							AS EditExt
		,a.sprakkod							AS SprakKod
		,bh.q_zu_approval_approvedby		AS ApprovedBy
		,bh.q_zu_approval_approveddt		AS ApprovedDate
		,isnull(bh.q_zu_approval_status,3)	AS ApprovalStatus
		--,isnull(bh.q_zu_approval_status,0)	AS ApprovalStatus
	FROM q_zu_CustomerPortal_purchase_list a
		JOIN bh (NOLOCK) ON bh.ForetagKod=a.ForetagKod
			AND bh.BestNr=a.BestNr
		JOIN fr (NOLOCK) on bh.foretagkod=fr.foretagkod 
			AND bh.ftgnr=fr.ftgnr
		LEFT JOIN pr (READUNCOMMITTED) ON
			pr.ForetagKod=a.ForetagKod
			AND pr.PersSign=a.perssign2
		LEFT JOIN sy2 (READUNCOMMITTED) ON
			sy2.PersSign=bh.RowCreatedBy
	WHERE a.Id=@Id
	ORDER BY bh.bestnr ASC

	-- Get all order rows

	SELECT bp.BestRadNr							AS BestRadNr
		,bp.ArtNr								AS ArtNr
		,ISNULL(bp.ArtBeskr, ar.ArtBeskr)		AS ArtBeskr
		,bp.bestantextqty						AS BestAntExtQty
		,bp.bestberlevdat						AS BestBerLevDat
		,bp.vb_inpris							AS Vb_Inpris
	FROM q_zu_CustomerPortal_purchase_list a
		JOIN bh (NOLOCK) ON bh.ForetagKod=a.ForetagKod
			AND bh.BestNr=a.BestNr
		JOIN bp (NOLOCK) ON bp.ForetagKod=bh.ForetagKod
			AND bp.BestNr=bh.BestNr
			AND bp.BestRestNr=0
		JOIN ar (NOLOCK) ON ar.ForetagKod=bp.ForetagKod
			AND ar.ArtNr=bp.ArtNr
	WHERE a.Id = @Id
	ORDER BY bp.BestRadNr ASC
END


IF (@SelectStatement = 'UpdateWebApprovalPurchaseOrder')
BEGIN
	SELECT @BestNr		= a.BestNr
		,@ForetagKod	= a.ForetagKod
		,@PersSign2		= a.perssign2
	FROM	q_zu_CustomerPortal_purchase_list a
	WHERE	a.Id = @Id;

	--Update the status on the purchase order
	UPDATE bh SET
		bh.q_zu_approval_message	= @Message
	WHERE	ForetagKod				= @ForetagKod 
		AND BestNr					= @BestNr;

	IF NOT @AttestStatus IN(N'2')
	BEGIN
		EXEC q_zu_CustomerPortal_po
		@c_foretagkod			= @ForetagKod
		,@c_bestnr				= @BestNr
		,@c_approval_flowid		= 0				-- Purchase
		,@c_perssign			= @ApprovedBy
		,@c_sprakkod			= 0
		,@Id=@Id
	END
	ELSE
	BEGIN
		UPDATE bh SET
			bh.q_zu_approval_status			= 2
			,bh.q_zu_approval_approvedby	= @ApprovedBy
			,bh.q_zu_approval_approveddt	= GETDATE()
		WHERE	ForetagKod					= @ForetagKod 
			AND BestNr						= @BestNr;

		update q_zu_CustomerPortal_purchase_list set Aktiv = 0 where Id = @Id;
	END
END
