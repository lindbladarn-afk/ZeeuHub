namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the original q_zu_speedrecon_levresk calculation into a hub-owned module.
public sealed class LeverantorsreskontraSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public LeverantorsreskontraSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.Leverantorsreskontra, repository)
    {
    }

    protected override string Sql => """
DECLARE @Today datetime = CONVERT(date, GETDATE());

EXEC dbo.lr_sal_perden
    @PerDenDatum = @ReconDate,
    @ForetagKod = @CompanyCode,
    @PersSign = @PersSign,
    @LevLogg = '+',
    @Swirre = '0';

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
    ISNULL(-SUM(reav.faktrestbel), 0),
    reav.RowUpdatedDt,
    reav.perssign,
    reav.foretagkod,
    'LEVRESK'
FROM dbo.reav reav WITH (READUNCOMMITTED)
INNER JOIN dbo.ko ko WITH (READUNCOMMITTED)
    ON reav.ForetagKod = ko.ForetagKod
   AND reav.KtoNrReskSaldo = ko.ktonr
   AND reav.RedovisnAr = ko.Redovisnar
WHERE reav.foretagkod = @CompanyCode
  AND reav.rowcreatedby = @PersSign
  AND reav.regdat = @Today
  AND reav.krorlr = 1
GROUP BY
    reav.ktonrresksaldo,
    ko.ktobeskr,
    reav.RowUpdatedDt,
    reav.perssign,
    reav.foretagkod;
""";
}
