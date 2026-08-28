namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the original q_zu_speedrecon_lagervarde calculation into a hub-owned module.
public sealed class LagervardeSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public LagervardeSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.Lagervarde, repository)
    {
    }

    protected override string Sql => """
DECLARE @Redar smallint;
DECLARE @Period smallint;
DECLARE @Lagstalle nvarchar(8);

EXEC dbo.CalcPeriod
    @InpDat = @ReconDate,
    @CalledFromProcedure = NULL,
    @Just_orddat = NULL,
    @c_intrnCoNo = @CompanyCode,
    @Redovisnar = @Redar OUTPUT,
    @Period = @Period OUTPUT;

DECLARE crsXB CURSOR FAST_FORWARD FOR
    SELECT xb.lagstalle
    FROM dbo.xb WITH (READUNCOMMITTED)
    WHERE xb.foretagkod = @CompanyCode
    ORDER BY xb.lagstalle;

OPEN crsXB;
FETCH NEXT FROM crsXB INTO @Lagstalle;

WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC dbo.lagvarde @Lagstalle, @Redar, @Period, @PersSign, NULL, @CompanyCode;

    FETCH NEXT FROM crsXB INTO @Lagstalle;
END;

CLOSE crsXB;
DEALLOCATE crsXB;

DECLARE @LagervardeRows TABLE
(
    artnr varchar(35),
    ktonr varchar(8),
    ktobeskr varchar(50),
    reconamount money,
    recondate datetime,
    foretagkod smallint,
    descr varchar(50),
    perssign varchar(8),
    vsar_prodkonto varchar(8),
    aklt_prodkonto varchar(8),
    vsar_lagstalle varchar(8),
    aklt_lagstalle varchar(8),
    prio smallint
);

INSERT INTO @LagervardeRows
SELECT DISTINCT
    vsar.ArtNr,
    aklt.ktonr,
    ko.ktobeskr,
    CASE arpk.mark_fifo
        WHEN 1 THEN vsar.PerUtgbatchvarde
        ELSE vsar.perutglagvardekalk
    END,
    @ReconDate,
    vsar.ForetagKod,
    'LAGVARDE',
    @PersSign,
    vsar.artprodkonto,
    aklt.artprodkonto,
    vsar.lagstalle,
    aklt.lagstalle,
    0
FROM dbo.vsar vsar WITH (READUNCOMMITTED)
INNER JOIN dbo.ar ar WITH (READUNCOMMITTED)
    ON vsar.foretagkod = ar.foretagkod
   AND vsar.artnr = ar.artnr
INNER JOIN dbo.aklt aklt WITH (READUNCOMMITTED)
    ON (
        vsar.foretagkod = aklt.foretagkod
        AND vsar.redovisnar = aklt.redovisnar
        AND vsar.lagstalle = aklt.lagstalle
        AND vsar.artprodkonto = aklt.artprodkonto
        AND aklt.lagtranstyp = 0
        AND aklt.intfield = 0
    )
    OR (
        vsar.foretagkod = aklt.foretagkod
        AND vsar.redovisnar = aklt.redovisnar
        AND vsar.artprodkonto = aklt.artprodkonto
        AND aklt.lagtranstyp = 0
        AND aklt.intfield = 0
    )
    OR (
        vsar.foretagkod = aklt.foretagkod
        AND vsar.redovisnar = aklt.redovisnar
        AND vsar.lagstalle = aklt.lagstalle
        AND aklt.lagtranstyp = 0
        AND aklt.intfield = 0
    )
    OR (
        vsar.foretagkod = aklt.foretagkod
        AND vsar.redovisnar = aklt.redovisnar
        AND aklt.lagtranstyp = 0
        AND aklt.intfield = 0
        AND aklt.lagstalle IS NULL
        AND aklt.artprodkonto IS NULL
    )
INNER JOIN dbo.ko ko WITH (READUNCOMMITTED)
    ON aklt.ForetagKod = ko.ForetagKod
   AND aklt.KtoNr = ko.KtoNr
   AND ko.Redovisnar = @Redar
INNER JOIN dbo.arpk arpk
    ON vsar.foretagkod = arpk.foretagkod
   AND vsar.ArtProdKonto = arpk.ArtProdKonto
WHERE vsar.PeriodLagSaldo <> 0
  AND vsar.redovisnar >= @Redar
  AND vsar.period = @Period
  AND vsar.foretagkod = @CompanyCode;

UPDATE @LagervardeRows
SET prio = 1
WHERE vsar_lagstalle = aklt_lagstalle
  AND vsar_prodkonto = aklt_prodkonto;

UPDATE @LagervardeRows
SET prio = 2
WHERE vsar_prodkonto = aklt_prodkonto
  AND prio = 0;

UPDATE @LagervardeRows
SET prio = 3
WHERE vsar_lagstalle = aklt_lagstalle
  AND prio = 0;

UPDATE @LagervardeRows
SET prio = 4
WHERE prio = 0;

WITH DeduplicatedRows AS
(
    SELECT
        artnr,
        vsar_lagstalle,
        foretagkod,
        reconamount,
        ROW_NUMBER() OVER (
            PARTITION BY artnr, vsar_lagstalle, foretagkod, reconamount
            ORDER BY prio, ktonr
        ) AS row_num
    FROM @LagervardeRows
)
DELETE FROM DeduplicatedRows
WHERE row_num > 1;

INSERT INTO dbo.q_zu_speedrecon_result
(
    q_zu_speedrecon_ktonr,
    ktobeskr,
    Q_zu_speedrecon_reconamount,
    Q_zu_speedrecon_recondate,
    perssign,
    foretagkod,
    q_zu_speedrecon_descr
)
SELECT
    ktonr,
    ktobeskr,
    SUM(reconamount),
    @ReconDate,
    @PersSign,
    foretagkod,
    'LAGVARDE'
FROM @LagervardeRows
GROUP BY ktonr, ktobeskr, foretagkod;
""";
}
