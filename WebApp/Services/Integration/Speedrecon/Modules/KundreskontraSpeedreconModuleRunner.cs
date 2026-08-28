namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the original q_zu_speedrecon_kundresk calculation into a hub-owned module.
public sealed class KundreskontraSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public KundreskontraSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.Kundreskontra, repository)
    {
    }

    protected override string Sql => """
DECLARE @Redar smallint;
DECLARE @Period smallint;
DECLARE @Today datetime = CONVERT(date, GETDATE());

EXEC dbo.CalcPeriod
    @InpDat = @ReconDate,
    @CalledFromProcedure = NULL,
    @Just_orddat = NULL,
    @c_intrnCoNo = @CompanyCode,
    @Redovisnar = @Redar OUTPUT,
    @Period = @Period OUTPUT;

EXEC dbo.JEEVES_kr_sal_perden
    @PerDenDatum = @ReconDate,
    @ForetagKod = @CompanyCode,
    @Visa_Fakturor = '*',
    @DebugMsg = NULL,
    @redovisnar = @Redar,
    @Period = @Period,
    @PersSign = @PersSign;

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
    reav.ktonrresksaldo,
    ko.ktobeskr,
    ISNULL(SUM(reav.faktrestbel), 0),
    reav.RowUpdatedDt,
    reav.perssign,
    reav.foretagkod,
    'KUNDRESK'
FROM dbo.reav reav WITH (READUNCOMMITTED)
INNER JOIN dbo.ko ko WITH (READUNCOMMITTED)
    ON reav.foretagkod = ko.foretagkod
   AND reav.ktonrresksaldo = ko.ktonr
   AND reav.RedovisnAr = ko.Redovisnar
WHERE reav.foretagkod = @CompanyCode
  AND reav.rowcreatedby = @PersSign
  AND reav.regdat = @Today
  AND reav.krorlr = 0
GROUP BY
    reav.ktonrresksaldo,
    ko.ktobeskr,
    reav.RowUpdatedDt,
    reav.perssign,
    reav.foretagkod;
""";
}
