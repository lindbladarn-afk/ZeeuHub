CREATE PROCEDURE [dbo].[q_zu_CustomerPortal_WebApprovalPriceList]
	@SelectStatement	NVARCHAR(100)
	,@Id				UNIQUEIDENTIFIER	= NULL
	,@PriceListId		INT					= NULL
	,@EmailAddress		NVARCHAR(512)		= NULL
	,@Status			INT					= NULL
	,@ForetagKod		SMALLINT			= NULL
	,@ArticleNumber		NVARCHAR(30)		= NULL
	,@UnitOfMeasure		NVARCHAR(20)		= NULL
	,@LowLimit			DECIMAL(25, 10)		= NULL
	,@PersSign2			NVARCHAR(60)		= NULL
	,@CompanyId			UNIQUEIDENTIFIER	= NULL
	,@ApprovedBy		NVARCHAR(60)		= NULL
	,@AttestStatus		NVARCHAR(250)		= NULL
	,@Message			NVARCHAR(MAX)		= NULL

	
	,@ErrorMessage		NVARCHAR(MAX)		= NULL
	,@c_perssign2		nvarchar(60)		= NULL
	,@c_perssign		nvarchar(60)		= NULL
	,@c_sprakkod		int					= 0

AS
IF (@SelectStatement = 'GetWebApprovalPriceListsAndRows')
BEGIN
	SELECT
		prh.PrisLista				AS PriceListId
		,prh.PrisListaBeskr			AS PriceListDescription
		,prh.ForetagKod				AS CompanyCode
		,prh.PrislGiltigFromDat		AS ValidFrom
		,prh.PrisLGiltigDat			AS ValidTo
		,prh.ValKod					AS Currency
	FROM prh (READUNCOMMITTED)
	WHERE prh.ForetagKod=@ForetagKod
		AND (@PriceListId IS NULL OR prh.PrisLista = @PriceListId)
	ORDER BY prh.PrisLista ASC

	SELECT
		PL.Id														AS Id
		,PL.PrisLista												AS PriceListId
		,PL.artnr													AS ArticleNumber
		,PL.aktiv													AS IsActive --NEW
		,PL.PersSign2												AS AttestantPersSign -- NEW
		,ar.ArtBeskr												AS ArticleDescription
		,ar.EnhetsKod												AS UnitOfMeasure
		,prl.LimitLowAnt											AS LowLimit
		,prl.ForetagKod												AS ForetagKod
		,prl.vb_pris												AS Price
		,prl.vb_PrisNytt											AS NewPrice
		,prl.vb_PrisNyttDatum										AS NewPriceDate
		,prl.Proc1													AS Discount
		,prl.Rabatt1												AS Discount1
		,prl.Rabatt2												AS Discount2
		,prl.Rabatt3												AS Discount3
		,prl.q_zu_approval_approvedby								AS ApprovedBy
		,prl.q_zu_approval_approveddt								AS ApprovedDate
		,prl.q_zu_approval_message									AS Message
		,prl.q_zu_approval_status									AS ApprovalStatus
		,CASE WHEN prl.q_zu_approval_status = 1 THEN 1 ELSE 0 END	AS IsApproved
		,CASE WHEN prl.q_zu_approval_status = 2 THEN 1 ELSE 0 END	AS IsRejected
	FROM q_zu_CustomerPortal_prl_list PL
		LEFT JOIN prl (READUNCOMMITTED) ON prl.ForetagKod = PL.ForetagKod 
			AND prl.PrisLista = PL.PrisLista 
			AND prl.ArtNr = PL.artnr
			AND prl.AltEnhetKod = PL.AltEnhetKod
			AND prl.LimitLowAnt = PL.LimitLowAnt
		LEFT JOIN ar (READUNCOMMITTED) ON ar.ForetagKod = PL.ForetagKod 
			AND ar.ArtNr = PL.ArtNr
	WHERE PL.ForetagKod=@ForetagKod
		AND (@PriceListId IS NULL OR PL.prislista = @PriceListId)
		AND (q_zu_approval_status != 1 AND q_zu_approval_status != 2)
		AND (PL.perssign2 = @PersSign2 OR PL.perssign2 IS NULL)
		AND (PL.aktiv <> 0) --NEW
	ORDER BY PL.ArtNr ASC, PL.LimitLowAnt
END


IF (@SelectStatement = 'UpdatePriceListRow')
BEGIN
	SELECT
		@PriceListId	= PL.PrisLista
		,@ArticleNumber	= PL.ArtNr
		,@UnitOfMeasure	= PL.AltEnhetKod
		,@LowLimit		= PL.LimitLowAnt
		,@ForetagKod	= PL.ForetagKod
		,@PersSign2		= PL.PersSign2
	FROM q_zu_CustomerPortal_prl_list PL
	WHERE PL.Id = @Id

	-- Update the price list rows with the apporpiate status
	UPDATE prl SET
		prl.q_zu_approval_message		= @Message
	WHERE prl.ForetagKod				= @ForetagKod
		AND prl.PrisLista				= @PriceListId
		AND prl.ArtNr					= @ArticleNumber
		AND prl.AltEnhetKod				= @UnitOfMeasure
		AND prl.LimitLowAnt				= @LowLimit

	-- Should I run the flow again if the AttestStatus = 2?
	IF NOT @Status IN (N'2')
	BEGIN
		EXEC q_zu_CustomerPortal_prl
			@c_foretagkod		= @ForetagKod
			,@c_prislista		= @PriceListId
			,@c_artnr			= @ArticleNumber
			,@c_altenhetkod		= @UnitOfMeasure
			,@c_limitlowant		= @LowLimit
			,@c_approval_flowid = 2				-- Price list flow
			,@c_perssign		= @ApprovedBy --zuorn 231123
			,@c_sprakkod		= 0
			,@Id=@Id
	END
	ELSE
	BEGIN
		UPDATE prl SET
			prl.q_zu_approval_status		= 2
		,	prl.q_zu_approval_approvedby	= @ApprovedBy
		,	prl.q_zu_approval_approveddt	= GETDATE()
		WHERE prl.ForetagKod				= @ForetagKod
			AND prl.PrisLista				= @PriceListId
			AND prl.ArtNr					= @ArticleNumber
			AND prl.AltEnhetKod				= @UnitOfMeasure
			AND prl.LimitLowAnt				= @LowLimit

		update q_zu_CustomerPortal_prl_list set Aktiv = 0 where Id = @Id;
	END
END