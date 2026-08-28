namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the original q_zu_speedrecon_orderunik calculation into a hub-owned module.
public sealed class OrderunikSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public OrderunikSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.Orderunik, repository)
    {
    }

    protected override string Sql => """
DECLARE @Redar smallint;
DECLARE @Period smallint;
DECLARE @Today varchar(6) = CONVERT(varchar, GETDATE(), 12);
DECLARE @FromDate datetime;

EXEC dbo.CalcPeriod
    @InpDat = @ReconDate,
    @CalledFromProcedure = NULL,
    @Just_orddat = NULL,
    @c_intrnCoNo = @CompanyCode,
    @Redovisnar = @Redar OUTPUT,
    @Period = @Period OUTPUT;

SET @FromDate = DATEADD(DAY, -730, @ReconDate);

EXEC dbo.jeeves_OU_PerDen
    @c_FomDat = @FromDate,
    @c_PerDenDat = @ReconDate,
    @c_ForetagKod = @CompanyCode,
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
    wou.ktonr,
    ko.KtoBeskr,
    ISNULL(SUM(wou.restdelvalueatitemcost), 0),
    wou.recondate,
    wou.perssign,
    wou.foretagkod,
    'ORDERUNIK'
FROM dbo.wou wou WITH (READUNCOMMITTED)
INNER JOIN dbo.ko ko WITH (READUNCOMMITTED)
    ON wou.foretagkod = ko.foretagkod
   AND wou.ktonr = ko.ktonr
   AND YEAR(wou.recondate) = ko.Redovisnar
WHERE wou.foretagkod = @CompanyCode
  AND wou.rowcreatedby = @PersSign
  AND wou.regdat = @Today
GROUP BY
    wou.ktonr,
    ko.ktobeskr,
    wou.recondate,
    wou.perssign,
    wou.foretagkod;
""";
}
