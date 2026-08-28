CREATE PROCEDURE [dbo].[q_zu_CustomerPortal_ZeeuDashboard]
	@SelectStatement		NVARCHAR(100)
	,@PersSign2				NVARCHAR(30)	= NULL
	,@ForetagKod			SMALLINT		= NULL
	,@NextWorkOrder			NVARCHAR(50)	= NULL
	,@NextProductionGroup	NVARCHAR(50)	= NULL
AS
	
IF (@SelectStatement = 'ProductionScreen')
BEGIN 
	CREATE TABLE #tempTablePresentEmployees
	(
		PersSign2					NVARCHAR(30)
		,Present					BIT
		,RespNamn					NVARCHAR(50)  
		,KomDatum					DATETIME
		,KomTid						DATETIME
		,AONR						BIGINT
		,OPNR						INT
		,Startat					DATETIME
		,ProdGrp					NVARCHAR(5)
		,StycktidTimmar				FLOAT
		,StycktidRapporteratTimmar	FLOAT
		,NextWorkOrder				NVARCHAR(50)
		,NextProductionGroup		NVARCHAR(50)
	);

	DECLARE 
		@Present					BIT
		,@RespNamn					NVARCHAR(50)  
		,@KomDatum					DATETIME
		,@KomTid					DATETIME
		,@AONR						BIGINT
		,@OPNR						INT
		,@Startat					DATETIME
		,@ProdGrp					NVARCHAR(5)
		,@StycktidTimmar			FLOAT
		,@StycktidRapporteratTimmar	FLOAT
  
	DECLARE present_employees_cursor CURSOR FOR   
	SELECT 
			PersSign2	AS PersSign2
			,CASE 
				WHEN (KomDatum > GickDatum AND KomDatum <= GETDATE()) OR (KomDatum = GickDatum AND KomTid > GickTid) THEN 1
				ELSE 0
				END AS Present
			,RespNamn	AS RespNamn
			,KomDatum	AS KomDatum
			,KomTid		AS KomTid

	FROM q_zu_prv AS prv
	WHERE ForetagKod = @ForetagKod
  
	OPEN present_employees_cursor  
  
	FETCH NEXT FROM present_employees_cursor   
		INTO @PersSign2
			,@Present
			,@RespNamn
			,@KomDatum
			,@KomTid
  
	WHILE @@FETCH_STATUS = 0  
	BEGIN  
		SELECT 
			@AONR						= NULL
			,@OPNR						= NULL
			,@Startat					= NULL
			,@ProdGrp					= NULL
			,@StycktidTimmar			= NULL
			,@StycktidRapporteratTimmar = NULL
			,@NextWorkOrder				= NULL
			,@NextProductionGroup		= NULL

		SELECT
				@AONR						= SFR_Ao.AoNr
				,@OPNR						= SFR_Ao.opnr
				,@Startat					= SFR_AoP.SFRWORepStartDt
				,@ProdGrp					= SFR_Ao.prodgr
				,@ProdGrp					= SFR_Ao.prodgr
				,@StycktidTimmar			= Ao.stycktid
				,@StycktidRapporteratTimmar = SFR_Ao.aorappstycktid

		FROM q_zu_SFR_Ao AS SFR_Ao
		JOIN q_zu_SFR_AoP AS SFR_AoP WITH (READUNCOMMITTED) ON 
			SFR_Ao.SQLIdentity = SFR_AoP.sfrconnaoid
		JOIN q_zu_ao AS ao WITH (READUNCOMMITTED) ON
			SFR_Ao.AoNr = ao.AoNr AND
			SFR_Ao.aopos = ao.AoPos AND
			SFR_Ao.opnr = ao.OpNr
		WHERE 
			SFR_Ao.SFRWORepEndDt is null
			AND SFR_Ao.SFRRepType = 20
			AND SFR_Ao.ForetagKod = @ForetagKod
			and SFR_AoP.perssign2 = @PersSign2

			SELECT
				@NextWorkOrder				= prv_extension.NextWorkOrder
				,@NextProductionGroup		= prv_extension.NextProductionGroup
			FROM q_zu_CustomerPortal_ZeeuDashboard_prv_extension AS prv_extension WITH (READUNCOMMITTED)
			WHERE prv_extension.ForetagKod = @ForetagKod AND
					prv_extension.PersSign2 = @PersSign2

		INSERT INTO #tempTablePresentEmployees
		(PersSign2, Present, RespNamn, KomDatum, KomTid, AONR, OPNR, ProdGrp, Startat, StycktidTimmar, StycktidRapporteratTimmar, NextWorkOrder, NextProductionGroup)
		VALUES
		(@PersSign2, @Present, @RespNamn, @KomDatum, @KomTid, @AONR, @OPNR, @ProdGrp, @Startat, @StycktidTimmar, @StycktidRapporteratTimmar, @NextWorkOrder, @NextProductionGroup)

		FETCH NEXT FROM present_employees_cursor   
		INTO @PersSign2
			,@Present
			,@RespNamn
			,@KomDatum
			,@KomTid 
	END   
	CLOSE present_employees_cursor;  
	DEALLOCATE present_employees_cursor; 

	SELECT 
		PersSign2					AS PersSign2
		,Present					AS Present
		,RespNamn					AS RespNamn
		,KomDatum					AS KomDatum
		,KomTid						AS KomTid
		,AONR						AS AoNr
		,OPNR						AS OpNr
		,Startat					AS Startat
		,ProdGrp					AS ProdGrp
		,StycktidTimmar				AS StycktidTimmar
		,StycktidRapporteratTimmar	AS StycktidRapporteratTimmar
		,NextWorkOrder				AS NextWorkOrder
		,NextProductionGroup		AS NextProductionGroup
	FROM #tempTablePresentEmployees
END;


IF (@SelectStatement = 'ProductionScreen_UpdateNextWorkOrder')
BEGIN 
	IF NOT EXISTS (SELECT 1 FROM q_zu_CustomerPortal_ZeeuDashboard_prv_extension AS prv_extension
					WHERE prv_extension.ForetagKod = @ForetagKod AND
					prv_extension.PersSign2 = @PersSign2)
	BEGIN
		-- The record does not exist
		INSERT INTO q_zu_CustomerPortal_ZeeuDashboard_prv_extension
		(PersSign2, ForetagKod, NextWorkOrder, NextProductionGroup)
		VALUES (@PersSign2, @ForetagKod, @NextWorkOrder, @NextProductionGroup)
	END
	ELSE
	BEGIN
		-- When the record exists
		UPDATE q_zu_CustomerPortal_ZeeuDashboard_prv_extension
		SET NextWorkOrder = @NextWorkOrder, NextProductionGroup = @NextProductionGroup
		WHERE PersSign2 = @PersSign2 AND ForetagKod = @ForetagKod
	END
END;