namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the original q_zu_speedrecon_lagerflytt calculation into a hub-owned module.
public sealed class LagerflyttSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public LagerflyttSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.Lagerflytt, repository)
    {
    }

    protected override string Sql => """
DECLARE @Redar smallint;
DECLARE @Period smallint;
DECLARE @Today varchar(6) = CONVERT(varchar, GETDATE(), 12);

EXEC dbo.CalcPeriod
    @InpDat = @ReconDate,
    @CalledFromProcedure = NULL,
    @Just_orddat = NULL,
    @c_intrnCoNo = @CompanyCode,
    @Redovisnar = @Redar OUTPUT,
    @Period = @Period OUTPUT;

EXEC dbo.jeeves_Create_wPOTransit_PerDen
    @c_PerDenDatum = @ReconDate,
    @c_IntrnCoNo = @CompanyCode,
    @c_PersSign = @PersSign;

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
    wpotransit.ktonr,
    ko.KtoBeskr,
    ISNULL(SUM(wpotransit.diffpickedamtrecamt), 0),
    wpotransit.ReconDate,
    wpotransit.PersSign,
    wpotransit.ForetagKod,
    'LAGFLYTT'
FROM dbo.wpotransit wpotransit WITH (READUNCOMMITTED)
INNER JOIN dbo.ko ko WITH (READUNCOMMITTED)
    ON wpotransit.ForetagKod = ko.ForetagKod
   AND wpotransit.KtoNr = ko.ktonr
   AND ko.Redovisnar = @Redar
WHERE wpotransit.foretagkod = @CompanyCode
  AND wpotransit.rowcreatedby = @PersSign
  AND wpotransit.regdat = @Today
GROUP BY
    wpotransit.ktonr,
    ko.ktobeskr,
    wpotransit.ReconDate,
    wpotransit.PersSign,
    wpotransit.ForetagKod;
""";
}
