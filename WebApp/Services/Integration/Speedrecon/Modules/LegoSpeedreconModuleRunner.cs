namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the original q_zu_speedrecon_lego calculation into a hub-owned module.
public sealed class LegoSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public LegoSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.Lego, repository)
    {
    }

    protected override string Sql => """
DECLARE @Redar smallint;
DECLARE @Period smallint;
DECLARE @Today datetime = CONVERT(varchar, GETDATE(), 12);
DECLARE @AkopKtonr nvarchar(8);

EXEC dbo.CalcPeriod
    @InpDat = @ReconDate,
    @Redovisnar = @Redar OUTPUT,
    @Period = @Period OUTPUT,
    @c_intrnCoNo = @CompanyCode,
    @CalledFromProcedure = NULL,
    @Just_orddat = NULL;

EXEC dbo.jeeves_wbpa_PerDen
    @c_IntrnCoNo = @CompanyCode,
    @c_PersSign = @PersSign,
    @c_PerDenDatum = @ReconDate,
    @c_HistFromDatum = '100101';

SELECT @AkopKtonr = akop.ktonr
FROM dbo.akop akop WITH (READUNCOMMITTED)
WHERE akop.foretagkod = @CompanyCode
  AND akop.RedovisnAr = @Redar
  AND akop.arbtranstyp = 20;

SET @AkopKtonr = ISNULL(@AkopKtonr, '0');

INSERT INTO dbo.q_zu_speedrecon_result
(
    q_zu_speedrecon_ktonr,
    Q_zu_speedrecon_reconamount,
    Q_zu_speedrecon_recondate,
    perssign,
    foretagkod,
    ktobeskr,
    q_zu_speedrecon_descr
)
SELECT
    wbpa.ktonr,
    ISNULL(SUM(wbpa.debcresumamt), 0),
    wbpa.recondate,
    wbpa.perssign,
    wbpa.foretagkod,
    ko.ktobeskr,
    'LEGO'
FROM dbo.wbpa wbpa WITH (READUNCOMMITTED)
INNER JOIN dbo.ko ko WITH (READUNCOMMITTED)
    ON wbpa.foretagkod = ko.foretagkod
   AND wbpa.redovisnar = ko.redovisnar
   AND wbpa.ktonr = ko.ktonr
WHERE wbpa.foretagkod = @CompanyCode
  AND wbpa.rowcreatedby = @PersSign
  AND wbpa.regdat = @Today
  AND wbpa.ktonr = @AkopKtonr
GROUP BY
    wbpa.ktonr,
    wbpa.recondate,
    wbpa.perssign,
    wbpa.foretagkod,
    ko.ktobeskr;
""";
}
