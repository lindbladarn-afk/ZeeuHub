namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the Speedrecon internal supplier ledger calculation into a hub-owned module.
public sealed class InternLeverantorsreskontraSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public InternLeverantorsreskontraSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.InternLeverantorsreskontra, repository)
    {
    }

    protected override string Sql => """
DECLARE @PerDenDatum datetime = @ReconDate;
DECLARE @Redar int;
DECLARE @Period smallint;
DECLARE @LrAkdFtgNr nvarchar(256);
DECLARE @LrAkdBestRkgNr nvarchar(256);
DECLARE @AnkSum money;
DECLARE @SlutSum money;

EXEC dbo.CalcPeriod
    @InpDat = @PerDenDatum,
    @CalledFromProcedure = NULL,
    @Just_orddat = NULL,
    @c_intrnCoNo = @CompanyCode,
    @Redovisnar = @Redar OUTPUT,
    @Period = @Period OUTPUT;

IF @PerDenDatum IS NULL
BEGIN
    SET @PerDenDatum = GETDATE();
END;

SET @PerDenDatum = CONVERT(char(8), @PerDenDatum, 112);

DECLARE @Rows TABLE
(
    FtgNr nvarchar(40) COLLATE database_default NOT NULL,
    BestRkgNr nvarchar(64) COLLATE database_default NOT NULL,
    LevFaktDat datetime NOT NULL,
    LrFaktFFDat datetime NULL,
    BokfDat datetime NULL,
    KtoNrInterimsBokning nvarchar(30) COLLATE database_default NULL,
    KstInterimsBokning nvarchar(30) COLLATE database_default NULL,
    KBarInterimsBokning nvarchar(30) COLLATE database_default NULL,
    K4InterimsBokning nvarchar(30) COLLATE database_default NULL,
    K5InterimsBokning nvarchar(30) COLLATE database_default NULL,
    K6InterimsBokning nvarchar(30) COLLATE database_default NULL,
    K7InterimsBokning nvarchar(30) COLLATE database_default NULL,
    KtoNrReskSaldo nvarchar(30) COLLATE database_default NULL,
    KstReskSaldo nvarchar(30) COLLATE database_default NULL,
    LrAvrKtoBel money DEFAULT 0,
    LrFaktBelBas money DEFAULT 0,
    urk float DEFAULT 0.0,
    EventRpEndDat datetime NULL,
    Ank_Ver_Dat datetime NULL,
    Ank_Ar smallint NULL,
    Ank_Ver int NULL,
    Ank_Sum money DEFAULT 0,
    Slut_Ver_Dat datetime NULL,
    Slut_Ar smallint NULL,
    Slut_Ver int NULL,
    Slut_Sum money DEFAULT 0,
    AutoRegel nvarchar(20) COLLATE database_default NULL,
    LevKategori smallint NULL,
    IntField smallint DEFAULT 0,
    ValKod nvarchar(6) COLLATE database_default NULL,
    vb_faktsum money DEFAULT 0,
    Lr_AttestSign nvarchar(60) COLLATE database_default NULL,
    PRIMARY KEY (FtgNr, BestRkgNr)
);

INSERT INTO @Rows
(
    FtgNr,
    BestRkgNr,
    LevFaktDat,
    LrFaktFFDat,
    BokfDat,
    KtoNrInterimsBokning,
    KstInterimsBokning,
    KBarInterimsBokning,
    K4InterimsBokning,
    K5InterimsBokning,
    K6InterimsBokning,
    K7InterimsBokning,
    KtoNrReskSaldo,
    KstReskSaldo,
    LrAvrKtoBel,
    LrFaktBelBas,
    urk,
    EventRpEndDat,
    Ank_Ver_Dat,
    Ank_Ar,
    Ank_Ver,
    Ank_Sum,
    Slut_Ver_Dat,
    Slut_Ar,
    Slut_Ver,
    Slut_Sum,
    AutoRegel,
    LevKategori,
    IntField,
    ValKod,
    vb_faktsum,
    Lr_AttestSign
)
SELECT
    FtgNr,
    BestRkgNr,
    LevFaktDat,
    LrFaktFFDat,
    BokfDat,
    KtoNrInterimsBokning,
    KstInterimsBokning,
    KBarInterimsBokning,
    K4InterimsBokning,
    K5InterimsBokning,
    K6InterimsBokning,
    K7InterimsBokning,
    KtoNrReskSaldo,
    KstReskSaldo,
    LrAvrKtoBel,
    LrFaktBelBas,
    CONVERT(float, 0),
    EventRpEndDat,
    BokfDat,
    RedovisnAr,
    CONVERT(int, NULL),
    CONVERT(money, 0),
    CONVERT(datetime, NULL),
    CONVERT(smallint, NULL),
    CONVERT(int, NULL),
    CONVERT(money, 0),
    AutoRegel,
    LevKategori,
    CONVERT(smallint, 0),
    ValKod,
    vb_faktsum,
    AttestSign
FROM dbo.lr WITH (NOLOCK)
WHERE LevFaktMakKod = '0'
  AND NOT (ISNULL(UtbSlag, ' ') = 'MAK' AND EventRPEndDat <= @PerDenDatum)
  AND LevFaktStatKod <> '9'
  AND ForetagKod = @CompanyCode
  AND (
        EventRPEndDat IS NULL
        OR EventRPEndDat > @PerDenDatum
        OR (EventRpEndDat <= @PerDenDatum AND levfaktstatkod = 7)
  );

INSERT INTO @Rows
(
    FtgNr,
    BestRkgNr,
    LevFaktDat,
    LrFaktFFDat,
    BokfDat,
    KtoNrInterimsBokning,
    KstInterimsBokning,
    KBarInterimsBokning,
    K4InterimsBokning,
    K5InterimsBokning,
    K6InterimsBokning,
    K7InterimsBokning,
    KtoNrReskSaldo,
    KstReskSaldo,
    LrAvrKtoBel,
    LrFaktBelBas,
    urk,
    EventRpEndDat,
    Ank_Ver_Dat,
    Ank_Ar,
    Ank_Ver,
    Ank_Sum,
    Slut_Ver_Dat,
    Slut_Ar,
    Slut_Ver,
    Slut_Sum,
    AutoRegel,
    LevKategori,
    IntField,
    ValKod,
    vb_faktsum,
    Lr_AttestSign
)
SELECT
    lr.FtgNr,
    lr.BestRkgNr,
    lr.LevFaktDat,
    lr.LrFaktFFDat,
    lr.BokfDat,
    lr.KtoNrInterimsBokning,
    lr.KstInterimsBokning,
    lr.KBarInterimsBokning,
    lr.K4InterimsBokning,
    lr.K5InterimsBokning,
    lr.K6InterimsBokning,
    lr.K7InterimsBokning,
    lr.KtoNrReskSaldo,
    lr.KstReskSaldo,
    lr.LrAvrKtoBel,
    lr.LrFaktBelBas,
    CONVERT(float, 0),
    lr.EventRpEndDat,
    lr.BokfDat,
    lr.RedovisnAr,
    CONVERT(int, NULL),
    CONVERT(money, 0),
    CONVERT(datetime, NULL),
    CONVERT(smallint, NULL),
    CONVERT(int, NULL),
    CONVERT(money, 0),
    lr.AutoRegel,
    lr.LevKategori,
    CONVERT(smallint, 0),
    lr.ValKod,
    lr.vb_faktsum,
    lr.AttestSign
FROM dbo.lr lr WITH (NOLOCK)
INNER JOIN dbo.att att WITH (NOLOCK)
    ON att.foretagkod = lr.foretagkod
   AND att.ftgnr = lr.ftgnr
   AND att.bestrkgnr = lr.bestrkgnr
WHERE att.attestkod1 = 5
  AND att.bokfdat > @PerDenDatum
  AND lr.LevFaktMakKod = '0'
  AND NOT (ISNULL(lr.UtbSlag, ' ') = 'MAK' AND lr.EventRPEndDat <= @PerDenDatum)
  AND lr.LevFaktStatKod <> '9'
  AND lr.ForetagKod = @CompanyCode
  AND NOT (
        lr.EventRPEndDat IS NULL
        OR lr.EventRPEndDat > @PerDenDatum
        OR (lr.EventRpEndDat <= @PerDenDatum AND lr.levfaktstatkod = 7)
  );

UPDATE r
SET
    Ank_Ver_Dat = vt.BokfDat,
    Ank_Ar = vt.RedovisnAr,
    Ank_Ver = vt.VerNr
FROM @Rows r
INNER JOIN dbo.att att
    ON r.FtgNr = att.FtgNr
   AND r.BestRkgNr = att.BestRkgNr
INNER JOIN dbo.vt vt
    ON att.RedovisnAr = vt.RedovisnAr
   AND att.VerNr = vt.VerNr
   AND att.ForetagKod = vt.ForetagKod
WHERE att.AttestKod1 = 0
  AND vt.vtstatus = 8
  AND att.ForetagKod = @CompanyCode;

UPDATE r
SET
    Slut_Ver_Dat = vt.BokfDat,
    Slut_Ar = vt.RedovisnAr,
    Slut_Ver = vt.VerNr
FROM @Rows r
INNER JOIN dbo.att att
    ON r.FtgNr = att.FtgNr
   AND r.BestRkgNr = att.BestRkgNr
INNER JOIN dbo.vt vt
    ON att.RedovisnAr = vt.RedovisnAr
   AND att.VerNr = vt.VerNr
   AND att.ForetagKod = vt.ForetagKod
WHERE vt.vtstatus = 8
  AND att.ForetagKod = @CompanyCode
  AND (att.attestkod1 = 1 OR att.AttestKod1 = 5)
  AND att.attestKod1 = (
        SELECT MAX(att2.AttestKod1)
        FROM dbo.att att2
        INNER JOIN dbo.vr vr2
            ON att2.redovisnar = vr2.redovisnar
           AND att2.vernr = vr2.Vernr
        WHERE r.FtgNr = att2.FtgNr
          AND r.BestRkgNr = att2.BestRkgNr
          AND r.FtgNr = vr2.FtgNr
          AND r.BestRkgnr = vr2.BestRkgNr
          AND vr2.ForetagKod = @CompanyCode
          AND (att2.attestkod1 = 1 OR att2.AttestKod1 = 5)
          AND vr2.KtoTyp = 201
          AND (vr2.debbel + vr2.kreBel) <> 0
          AND att2.ForetagKod = @CompanyCode
  )
  AND att.RadPos = (
        SELECT MAX(att2.RadPos)
        FROM dbo.att att2
        INNER JOIN dbo.vr vr2
            ON att2.redovisnar = vr2.redovisnar
           AND att2.vernr = vr2.Vernr
        WHERE r.FtgNr = att2.FtgNr
          AND r.BestRkgNr = att2.BestRkgNr
          AND r.FtgNr = vr2.FtgNr
          AND r.BestRkgnr = vr2.BestRkgNr
          AND vr2.ForetagKod = @CompanyCode
          AND (att2.attestkod1 = 1 OR att2.AttestKod1 = 5)
          AND vr2.KtoTyp = 201
          AND (vr2.debbel + vr2.kreBel) <> 0
          AND att2.ForetagKod = @CompanyCode
  );

DELETE FROM @Rows
WHERE (
        EventRpEndDat < @PerDenDatum
        AND LrAvrKtoBel = 0
        AND Ank_Ver_Dat < @PerDenDatum
        AND Slut_Ver_Dat < @PerDenDatum
)
OR (
        (EventRpEndDat IS NULL OR EventRpEndDat > @PerDenDatum)
        AND LrAvrktoBel = 0
        AND Ank_Ver_Dat > @PerDenDatum
        AND (Slut_Ver_Dat IS NULL OR Slut_Ver_Dat > @PerDenDatum)
)
OR (
        Ank_Ver_Dat > @PerDenDatum
        AND (Slut_Ver_Dat IS NULL OR Slut_Ver_Dat > @PerDenDatum)
);

DECLARE lr_akd_cursor CURSOR LOCAL STATIC FORWARD_ONLY READ_ONLY FOR
    SELECT FtgNr, BestRkgNr
    FROM @Rows;

OPEN lr_akd_cursor;

WHILE 1 = 1
BEGIN
    FETCH NEXT FROM lr_akd_cursor INTO @LrAkdFtgNr, @LrAkdBestRkgNr;

    IF @@ERROR <> 0 BREAK;
    IF @@FETCH_STATUS <> 0 BREAK;

    SELECT @AnkSum = COALESCE((
        SELECT SUM(vr1.DebBel - vr1.KreBel)
        FROM @Rows r
        INNER JOIN dbo.vr vr1 WITH (READUNCOMMITTED)
            ON r.Ank_Ar = vr1.Redovisnar
           AND r.Ank_Ver = vr1.VerNr
        WHERE r.FtgNr = @LrAkdFtgNr
          AND r.BestRkgNr = @LrAkdBestRkgNr
          AND vr1.ForetagKod = @CompanyCode
          AND vr1.BokfDat <= @PerDenDatum
          AND vr1.KtoTyp = 201
    ), 0);

    SELECT @SlutSum = COALESCE((
        SELECT SUM(vr2.DebBel - vr2.KreBel)
        FROM @Rows r
        INNER JOIN dbo.vr vr2 WITH (READUNCOMMITTED)
            ON r.Slut_Ar = vr2.Redovisnar
           AND r.Slut_Ver = vr2.VerNr
        WHERE r.FtgNr = @LrAkdFtgNr
          AND r.BestRkgNr = @LrAkdBestRkgNr
          AND vr2.ForetagKod = @CompanyCode
          AND vr2.KtoTyp = 201
          AND vr2.BokfDat <= @PerDenDatum
    ), 0);

    SELECT @SlutSum = SUM(vr.Debbel - vr.Krebel)
    FROM @Rows r
    INNER JOIN dbo.att att
        ON r.FtgNr = att.FtgNr
       AND r.BestRkgNr = att.BestRkgNr
    INNER JOIN dbo.vt vt
        ON att.RedovisnAr = vt.RedovisnAr
       AND att.VerNr = vt.VerNr
       AND att.ForetagKod = vt.ForetagKod
    INNER JOIN dbo.vr vr
        ON vr.ForetagKod = vt.ForetagKod
       AND vr.RedovisnAr = vt.RedovisnAr
       AND vr.VerNr = vt.VerNr
    WHERE r.FtgNr = @LrAkdFtgNr
      AND r.BestRkgNr = @LrAkdBestRkgNr
      AND vt.vtstatus = 8
      AND vr.BokfDat <= @PerDenDatum
      AND att.ForetagKod = @CompanyCode
      AND (att.attestkod1 = 1 OR att.AttestKod1 = 5)
      AND vr.KtoTyp = 201
      AND (vr.debbel - vr.kreBel) <> 0;

    IF @SlutSum IS NULL SET @SlutSum = 0;
    IF @AnkSum IS NULL SET @AnkSum = 0;

    UPDATE @Rows
    SET
        Ank_Sum = @AnkSum,
        Slut_Sum = @SlutSum
    WHERE FtgNr = @LrAkdFtgNr
      AND BestRkgNr = @LrAkdBestRkgNr;
END;

CLOSE lr_akd_cursor;
DEALLOCATE lr_akd_cursor;

DELETE FROM @Rows
WHERE Ank_Sum + Slut_Sum = 0;

UPDATE @Rows
SET LrAvrKtoBel = Ank_Sum + Slut_Sum;

INSERT INTO dbo.q_zu_speedrecon_result
(
    q_zu_speedrecon_ktonr,
    Q_zu_speedrecon_reconamount,
    Q_zu_speedrecon_recondate,
    q_zu_speedrecon_descr,
    ktobeskr,
    perssign,
    foretagkod
)
SELECT
    r.KtoNrInterimsBokning,
    ISNULL(SUM(r.LrAvrKtoBel), 0),
    @PerDenDatum,
    'INTLEVRESK',
    ko.KtoBeskr,
    @PersSign,
    @CompanyCode
FROM @Rows r
INNER JOIN dbo.ko ko
    ON ko.ForetagKod = @CompanyCode
   AND ko.Redovisnar = @Redar
   AND ko.ktonr = r.KtoNrInterimsBokning
GROUP BY r.KtoNrInterimsBokning, ko.ktobeskr;
""";
}
