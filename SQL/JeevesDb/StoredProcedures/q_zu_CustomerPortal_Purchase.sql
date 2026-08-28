-- ============================================
-- Author:		Daniel Mellqvist, ZeeU AB
-- Create date: 2022-01-06
-- Called by:	ZeeU.CustomerPortal
-- Description:	SP to handle all purchase related transactions
-- =============================================

CREATE PROCEDURE [dbo].[q_zu_CustomerPortal_Purchase]
	@SelectStatement		NVARCHAR(100)
	,@ErrorMessage			NVARCHAR(MAX)			=NULL
	,@ForetagKod			SMALLINT				=NULL
	,@CompanyNumber			SMALLINT				=NULL
	,@OrderNumber			BIGINT					=NULL

	-- Purchase order head
	,@c_ftgnr				jeeves_companyno		=NULL -- Required for creating order
	,@c_perssign			jeeves_strvarchar32		=NULL -- Required for creating order, order rows and stock delivery
	,@c_ordlevadr1			Jeeves_StrVarChar128	=NULL -- company
	,@c_ordlevadr2			Jeeves_StrVarChar64		=NULL
	,@c_ordlevadr3			Jeeves_StrVarChar64		=NULL
	,@c_ordlevadr4			Jeeves_StrVarChar64		=NULL
	,@c_ftgpostnr			Jeeves_StrVarChar10		=NULL
	,@c_ordlevadrbstort		Jeeves_StrVarChar64		=NULL
	,@c_ordlevadrlandskod	Jeeves_StrVarChar3		=NULL

	-- Purchase order row
	,@c_artbeskr			NVARCHAR(60)			=NULL
	,@c_vb_inpris			DECIMAL(22,8)			=NULL
	,@c_rabatt1				DECIMAL(16,8)			=NULL
	,@c_ktonr				NVARCHAR(8)				=NULL
	,@c_koststallekod		NVARCHAR(8)				=NULL
	,@c_bestant				Jeeves_Qty				=1.00

	-- Add to stock
	,@c_bestradnr			int						=NULL -- Required for creating stock delivery
	,@c_bestlevdat			datetime				=NULL -- Required for creating stock delivery
	,@c_bestlevant			Jeeves_Qty				=1.00 -- Required for creating stock delivery
	,@c_makulerarestantal	Jeeves_Boolean			=N'0'

	-- All
	,@c_bestnr				bigint					=NULL -- Required for creating order rows and stock delivery
	,@c_bestberlevdat		datetime				=NULL
	,@c_besttyp				smallint				=NULL
	,@c_beststatkod			smallint				=NULL
	,@c_bestlevsattkod		smallint				=NULL
	,@c_bestlevvilkkd		smallint				=NULL
	,@c_artnr				Jeeves_itemNo			=NULL -- Required for creating order rows and stock delivery
	,@c_lagstalle			jeeves_strvarchar8		=NULL
	,@c_levbetvillk			jeeves_strvarchar2		=NULL
	,@c_valkod				jeeves_strvarchar3		=NULL
	,@c_vref				jeeves_strvarchar64		=NULL
	,@c_edit				jeeves_strvarcharmax	=NULL


AS
BEGIN
	DECLARE
		@dbc					integer
		,@dbe					integer
		,@dbp					integer					= @@procid
		,@SprakKod				int
		,@PersSign				nvarchar(60)
		,@bestradnr				int
		,@bestrestnr			int
		,@ftgnr					Jeeves_CompanyNo
		,@vb_inpris				money


	IF (@SelectStatement = 'GetAllSuppliers')
	BEGIN
		IF ISNULL(@ForetagKod,'') = ''
		BEGIN
				SET @ErrorMessage = 'No company code provided'
				EXEC Jeeves_raiserror 50000, @ErrorMessage;
				THROW 50000, @ErrorMessage, 0;
		END

		SELECT
			fr.ftgnr					AS FtgNr
			,fr.ftgnamn					AS FtgNamn
			,fr.ftgpostadr1				AS FtgPostAdr1
			,fr.ftgpostadr2				AS FtgPostAdr2
			,fr.ftgpostadr3				AS FtgPostAdr3
			,fr.ftgpostnr				AS ZipCode
			,fr.landskod				AS Country
			,fr.orgnr					AS OrgNr
			,le.kundnrhoslev			AS KundNrHosLev
			,le.valkod					AS ValKod
			,CAST(le.utbsparr AS INT)	AS UtbSparr
			,sy1.FtgNamn				AS DeliveryFtgNamn
			,sy1.FtgPostAdr1			AS DeliveryFtgPostAdr1
			,sy1.FtgPostAdr2			AS DeliveryFtgPostAdr2
			,sy1.FtgPostnr				AS DeliveryFtgPostNr
			,sy1.FtgPostAdr3			AS DeliveryFtgPostAdr3
			,sy1.LandsKod				AS DeliveryLandsKod
		 FROM fr WITH (READUNCOMMITTED)
			JOIN le ON fr.foretagkod = le.foretagkod AND fr.ftgnr = le.ftgnr
			JOIN sy1 ON sy1.ForetagKod = @ForetagKod
		 WHERE fr.foretagkod = @ForetagKod
			AND ISNULL(le.Makulerad,'0') = '0'
			AND le.q_zu_expence_supplier = '1'
	END

	IF (@SelectStatement = 'GetAutoCompleteSuppliers')
	BEGIN
		IF ISNULL(@ForetagKod,'') = ''
		BEGIN
				SET @ErrorMessage = 'No company code provided'
				EXEC Jeeves_raiserror 50000, @ErrorMessage;
				THROW 50000, @ErrorMessage, 0;
		END

		SELECT
			fr.ftgnr			AS FtgNr
			,fr.ftgnamn			AS FtgNamn
			,fr.ftgpostadr3		AS FtgPostAdr3
			,fr.ftgpostnr		AS ZipCode
			,fr.landskod		AS Country
			,fr.orgnr			AS OrgNr
			,le.utbsparr		AS UtbSparr
		 FROM fr WITH (READUNCOMMITTED)
			JOIN le ON fr.foretagkod = le.foretagkod AND fr.ftgnr = le.ftgnr
		 WHERE fr.foretagkod = @foretagkod
			AND ISNULL(le.Makulerad,'0') = '0'
			AND le.q_zu_expence_supplier = '1'
	END

	IF (@SelectStatement = 'GetSupplierContacts')
	BEGIN
		SELECT 
			kp.FtgPerson	AS FtgPerson
			,cr.ComNr		AS ComNr
			,x4.ComBeskr	AS ComBeskr
			,cr.FtgNr		AS FtgNr
		FROM cr
		JOIN kp ON cr.ForetagKod = kp.ForetagKod AND
			cr.FtgKontaktNr = kp.FtgKontaktNr AND
			cr.FtgNr = kp.FtgNr
		JOIN x4 ON cr.ForetagKod = x4.ForetagKod AND
			cr.ComKod = X4.ComKod
		WHERE cr.foretagkod = @ForetagKod AND
			cr.ftgnr = @CompanyNumber AND
			cr.comnr IS NOT NULL
	END

	IF (@SelectStatement = 'GetAllContacts')
	BEGIN
		SELECT 
			kp.FtgPerson	AS FtgPerson
			,cr.ComNr		AS ComNr
			,x4.ComBeskr	AS ComBeskr
			,cr.FtgNr		AS FtgNr
		FROM cr
		JOIN kp ON cr.ForetagKod = kp.ForetagKod AND
			cr.FtgKontaktNr = kp.FtgKontaktNr AND
			cr.FtgNr = kp.FtgNr
		JOIN x4 ON cr.ForetagKod = x4.ForetagKod AND
			cr.ComKod = X4.ComKod
		WHERE cr.foretagkod = @ForetagKod AND
			cr.comnr IS NOT NULL
	END

	IF (@SelectStatement = 'GetExpenceArticles')
	BEGIN 
		IF ISNULL(@ForetagKod,'') = ''
		BEGIN
				SET @ErrorMessage = 'No company code provided'
				EXEC Jeeves_raiserror 50000, @ErrorMessage;
				THROW 50000, @ErrorMessage, 0;
		END

		SELECT 
			ar.artnr AS ArtNr
			,ar.artbeskr AS ArtBeskr
			,ar.enhetskod AS EnhetsKod
			,ar.varugruppkod AS VaruGruppKod
			,ar.q_zu_default_acccount AS q_zu_default_acccount
			,ar.q_zu_default_costcenter AS q_zu_default_costcenter
			,CAST(ar.q_zu_expence_item AS INT) AS q_zu_expence_item
			,ar.foretagkod AS ForetagKod
		FROM ar
		WHERE ar.q_zu_expence_item = 1 AND
			ar.ForetagKod = @ForetagKod
	END

	IF (@SelectStatement = 'GetPurchaseOrders')
	BEGIN
		SELECT 
			bh.ftgnr							AS FtgNr
			,fr.ftgnamn							AS FtgNamn
			,fr.orgnr							AS OrgNr
			,bh.bestnr							AS BestNr
			,bh.beststatkod						AS BestStatKod
			,xl.BestStatBeskr					AS BestStatBeskr
			,bh.ordlevadr1						AS FtgPostAdr1
			,bh.ordlevadr2						AS FtgPostAdr2
			,bh.ordlevadr3						AS FtgPostAdr3
			,bh.FtgPostnr						AS FtgPostNr
			,bh.ordlevadrbstort					AS DeliveryCity
			,bh.ordlevadrlandskod				AS Country
			--,CAST(bh.AnkomstRapporterad	AS INT)	AS IsReceived
			,bh.regdat							AS RegDat
			,bh.BestBegLevDat					AS BestBegLevDat
			,bh.VbBestValue						AS OrderValue
			,bh.valkod							AS Currency
		FROM bh
			JOIN fr ON bh.ftgnr = fr.ftgnr AND bh.foretagkod = fr.foretagkod
			JOIN xl ON bh.BestStatKod = xl.BestStatKod AND bh.ForetagKod = xl.ForetagKod
		WHERE bh.perssign = @c_perssign AND
			bh.besttyp = 900 AND
			bh.foretagkod = @ForetagKod
	END

	IF (@SelectStatement = 'GetPurchaseOrder')
	BEGIN
		IF (NOT EXISTS(select 1 from bh (readuncommitted) WHERE bh.perssign = @c_perssign AND bh.besttyp = 900 AND bh.foretagkod = @ForetagKod AND bh.bestnr = @OrderNumber))
		BEGIN
			SET @ErrorMessage = 'Purchase order ' + CAST(@OrderNumber as nvarchar) + ' does not exist';
			THROW 50001, @ErrorMessage, 1;
		END

		SELECT 
			bh.ftgnr							AS FtgNr
			,fr.ftgnamn							AS FtgNamn
			,fr.orgnr							AS OrgNr
			,bh.bestnr							AS BestNr
			,bh.beststatkod						AS BestStatKod
			,xl.BestStatBeskr					AS BestStatBeskr
			,bh.ordlevadr1						AS DeliveryFtgNamn
			,bh.ordlevadr2						AS DeliveryFtgPostAdr1
			,bh.ordlevadr3						AS DeliveryFtgPostAdr2
			,bh.FtgPostnr						AS DeliveryFtgPostNr
			,bh.ordlevadrbstort					AS DeliveryCity
			,bh.ordlevadrlandskod				AS DeliveryLandsKod
			--,CAST(bh.AnkomstRapporterad	AS INT)	AS IsReceived
			,bh.regdat							AS RegDat
			,bh.BestBegLevDat					AS BestBegLevDat
			,bh.VbBestValue						AS OrderValue
			,bh.valkod							AS Currency
		FROM bh (READUNCOMMITTED)
			JOIN fr ON bh.ftgnr = fr.ftgnr AND bh.foretagkod = fr.foretagkod
			JOIN xl ON bh.BestStatKod = xl.BestStatKod AND bh.ForetagKod = xl.ForetagKod
		WHERE bh.perssign = @c_perssign AND
			bh.besttyp = 900 AND
			bh.foretagkod = @ForetagKod AND
			bh.bestnr = @OrderNumber

		SELECT
			bp.ArtNr			AS ArticleNumber
			,bp.ArtBeskr		AS ArticleDescription
			,bp.BestAnt			AS Quantity
			,bp.BestLevAnt		AS RecievedQuantity
			,bp.BestRadNr		AS RowNumber
			,bp.EnhetsKod		AS Unit
			,bp.vb_inpris		AS Price
			,bp.Rabatt1			AS Discount
			,bp.KtoNr			AS Account
			,bp.KostStalleKod	AS CostCenter
		FROM bp (READUNCOMMITTED)
		WHERE bp.BestNr = @OrderNumber AND 
			bp.ForetagKod = @ForetagKod
		ORDER BY bp.BestRadNr
	END


	IF (@SelectStatement = 'CreatePurchaseOrder')
	BEGIN

		set @SprakKod	= 0
		set @PersSign	= @c_perssign

		if @c_bestberlevdat is null set @c_bestberlevdat = cast(getdate() as date)

		exec @dbc					= Jeeves_Init_Insert_Bh
			@c_ForetagKod			= @ForetagKod
			,@c_FtgNr				= @c_ftgnr
			,@c_BestNr				= @c_bestnr output
			,@c_LagStalle			= @c_lagstalle
			,@c_BestBerLevDat		= @c_bestberlevdat
			,@c_BestTyp				= @c_besttyp
			,@c_BestStatKod			= @c_beststatkod
			,@c_LevBetVillK			= @c_levbetvillk
			,@c_BestLevSattKod		= @c_bestlevsattkod
			,@c_BestLevVilkKd		= @c_bestlevvilkkd
			,@c_ValKod				= @c_valkod
			,@c_Vref				= @c_vref
			,@c_Edit				= @c_edit
			,@c_PersSign			= @PersSign
			,@c_OrdLevAdr1			= @c_ordlevadr1
			,@c_OrdLevAdr2			= @c_ordlevadr2
			,@c_OrdLevAdr3			= @c_ordlevadr3
			,@c_OrdLevAdr4			= @c_ordlevadr4
			,@c_FtgPostnr			= @c_ftgpostnr
			,@c_OrdLevAdrBstOrt		= @c_ordlevadrbstort
			,@c_OrdLevAdrLandsKod	= @c_ordlevadrlandskod
		select @dbe=@@error
		if @dbc<>0 or @dbe<>0 
		begin
			SET @ErrorMessage = 'Purchase order could not be created';
			throw 50001, @ErrorMessage, 1;
		end

		select @c_bestnr as BestNr, 1 AS Success, 'Purchase order created' AS Message
	END


	IF (@SelectStatement = 'CreatePurchaseOrderRow')
	BEGIN
		set @SprakKod	= 0
		set @PersSign	= @c_perssign

		-- Check that Account exists and is available
		IF (NOT EXISTS(select 1 from ko (readuncommitted) where ko.Redovisnar = YEAR(GETDATE()) and KtoNr = @c_ktonr and ForetagKod = @ForetagKod and FarKonterasManuellt = 1))
		BEGIN
			SET @ErrorMessage = 'Account ' + CAST(@c_ktonr as nvarchar) + ' does not exist in the current year [ko]';
			THROW 50002, @ErrorMessage, 1;
		END

		-- Check if Account is valid for purchase orders
		IF (NOT EXISTS(SELECT 1 FROM beko (READUNCOMMITTED) WHERE beko.ForetagKod = @ForetagKod AND beko.KtoNr = @c_ktonr))
		BEGIN
			SET @ErrorMessage = 'Account ' + CAST(@c_ktonr as nvarchar) + ' does not exist in [beko]';
			THROW 50003, @ErrorMessage, 1;
		END

		-- Check if Cost Centre exists and is available
		IF (NOT EXISTS(SELECT 1 FROM kt (READUNCOMMITTED) WHERE kt.Redovisnar = YEAR(GETDATE()) AND KostStalleKod = @c_koststallekod AND ForetagKod = @ForetagKod AND FarKonterasPa = 1))
		BEGIN
			SET @ErrorMessage = 'Cost centre ' + CAST(@c_koststallekod as nvarchar) + ' does not exist [kt]';
			THROW 50004, @ErrorMessage, 1;
		END

		-- Check if Cost centre is valid for purchase orders
		IF (NOT EXISTS(SELECT 1 FROM bekt (READUNCOMMITTED) WHERE bekt.ForetagKod = @ForetagKod AND bekt.KostStalleKod = @c_koststallekod))
		BEGIN
			SET @ErrorMessage = 'Cost centre ' + CAST(@c_koststallekod as nvarchar) + ' does not exist in [bekt]';
			THROW 50005, @ErrorMessage, 1;
		END

		-- assign from bh
		select
			@c_bestberlevdat = coalesce(@c_bestberlevdat,bh.BestBerLevDat)
		,	@ftgnr=bh.FtgNr
		from bh (readuncommitted)
		where
			bh.ForetagKod	= @ForetagKod and 
			bh.BestNr		= @c_bestnr

		-- assign from al
		exec @dbc		= JEEVES_Fetch_Al
			@Foretagkod	= @ForetagKod
		,	@Call_Type	= 0 --manual entry
		,	@ArtNr		= @c_artnr
		,	@FtgNr		= @ftgnr
		,	@O_Vb_InPris= @vb_inpris output
		select @dbe=@@error
		if @dbc<>0 or @dbe<>0 
		begin
			SET @ErrorMessage = 'Article ' + CAST(@c_artnr AS NVARCHAR) + ' does not exist in [al] for the supplier ' + CAST(@FtgNr AS NVARCHAR);
			Throw 50006, @ErrorMessage, 1;
		end

		exec @dbc				= Jeeves_Init_Insert_Bp
			@c_ForetagKod		= @ForetagKod
			,@c_BestNr			= @c_bestnr
			,@c_ArtNr			= @c_artnr
			,@c_ArtBeskr		= @c_artbeskr
			,@c_BestAnt			= @c_bestant
			,@c_vb_inpris		= @c_vb_inpris
			,@c_Rabatt1			= @c_rabatt1
			,@c_BestBegLevDat	= @c_bestberlevdat
			,@c_LagStalle		= @c_lagstalle
			,@c_BestTyp			= @c_besttyp
			,@c_ValKod			= @c_valkod
			,@c_Edit			= @c_edit
			,@c_PersSign		= @c_perssign
			,@c_BestRadNr		= @bestradnr output
			,@c_BestRestNr		= @bestrestnr output
			,@c_KtoNr			= @c_ktonr
			,@c_KostStalleKod	= @c_koststallekod
			,@c_beststatkod		= 10
		select @dbe = @@error
		if @dbc<>0 or @dbe<>0 
		begin
			SET @ErrorMessage = 'Article ' + CAST(@c_artnr AS NVARCHAR) + ' could not be added into [bp]';
			throw 50007, @ErrorMessage, 1;
		end

		if @bestrestnr is null set @bestrestnr = 0
			select @c_bestnr as BestNr, @bestrestnr as BestRestNr, @bestradnr as BestRadNr, 1 AS Success, 'Purchase order row created' AS Message

	END




	IF (@SelectStatement = 'CreateStockDeliveryRow')
	BEGIN
		DECLARE
			@bp_lagstalle		nvarchar(16)
			,@bp_bestant		Jeeves_Qty
			,@bp_paketartikel	Jeeves_Boolean
			,@bp_valkod			nvarchar(6)
			,@xx_valkurs		float
			,@new_dummyuniqueid varchar(38)
			,@uniktid			bigint

		SET @SprakKod	= 0
		SET @PersSign	= @c_perssign

		IF (@c_bestberlevdat > GETDATE())
		BEGIN
			SET @ErrorMessage = 'Date provided is in the future.';
			THROW 50002, @ErrorMessage, 1;
		END

		-- assign from bp
		SELECT
			@bp_lagstalle		= bp.LagStalle
			,@bp_bestant		= bp.BestAnt
			,@bp_paketartikel	= bp.PaketArtikel
			,@bp_valkod			= bp.ValKod
		FROM bp (READUNCOMMITTED)
		WHERE
			bp.ForetagKod	= @ForetagKod AND
			bp.BestNr		= @c_bestnr AND
			bp.BestRadNr	= @c_bestradnr AND
			bp.BestRestNr	= 0

		IF (@bp_bestant < @c_bestlevant)
		BEGIN
			SET @ErrorMessage = 'Added quantity (' + CAST(@c_bestlevant AS NVARCHAR) + ') is larger than ordered quantity (' + CAST(@bp_bestant AS NVARCHAR) + ')';
			THROW 50003, @ErrorMessage, 1;
		END

		--assign from xx
		SELECT
			@xx_valkurs	=	xx.ValKurs
		FROM xx (READUNCOMMITTED)
		WHERE
			xx.ForetagKod	= @ForetagKod AND
			xx.ValKod		= @bp_valkod

		--assign DummyUniqueId
		EXEC @dbc=JEEVES_Unique_BaseValue_p2p
			@Unique_BaseValue = @new_dummyuniqueid OUTPUT

		EXEC @dbc=JEEVES_Init_Insert_Bpi 
			@c_ForetagKod				= @ForetagKod
			,@c_BestNr					= @c_bestnr
			,@c_BestRestNr				= 0
			,@c_BestRadNr				= @c_bestradnr
			,@c_DummyUniqueId			= @new_dummyuniqueid
			,@c_ArtNr					= @c_artnr
			,@c_BestLevAnt				= @c_bestlevant
			,@c_BestLevDat				= @c_bestlevdat
			,@c_LagStalle				= @bp_lagstalle
			,@c_MakuleraRestAntal		= @c_makulerarestantal
			,@c_MakuleraRestAntalOrder	= @c_makulerarestantal
			,@c_PaketArtikel			= @bp_paketartikel
			,@c_ValKod					= @bp_valkod
			,@c_ValKurs					= @xx_valkurs
			,@c_perssign				= @PersSign
		SELECT @dbe=@@error
		IF @dbc <> 0 or @dbe <> 0 
		BEGIN
			SET @ErrorMessage = 'Could not insert ' + CAST(@c_artnr AS NVARCHAR) + ' into [bpi]';
			THROW 50001, @ErrorMessage, 1;
		END

		SELECT @ForetagKod		AS ForetagKod
			,@c_bestnr			AS BestNr
			,0					AS BestRestNr
			,@c_bestradnr		AS BestRadNr
			,@new_dummyuniqueid AS DummyUniqueId

	END
END