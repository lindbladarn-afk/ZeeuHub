namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the original q_zu_speedrecon_periodisering calculation into a hub-owned module.
public sealed class PeriodiseringSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public PeriodiseringSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.Periodisering, repository)
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

EXEC dbo.Jeeves_Remd_recon_PerDen
    @c_IntrnCoNo = @CompanyCode,
    @c_Redovisnar = @Redar,
    @c_Period = @Period,
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
    remd_recon.ktonr,
    ko.KtoBeskr,
    ISNULL(SUM(remd_recon.allocateremainthisyear * -1), 0),
    @ReconDate,
    remd_recon.perssign,
    remd_recon.foretagkod,
    'PERIOD'
FROM dbo.remd_recon remd_recon WITH (READUNCOMMITTED)
INNER JOIN dbo.ko ko WITH (READUNCOMMITTED)
    ON remd_recon.foretagkod = ko.foretagkod
   AND remd_recon.ktonr = ko.ktonr
   AND remd_recon.Redovisnar = ko.Redovisnar
WHERE remd_recon.foretagkod = @CompanyCode
  AND remd_recon.perssign = @PersSign
  AND remd_recon.regdat = @Today
GROUP BY
    remd_recon.KtoNr,
    ko.ktobeskr,
    remd_recon.perssign,
    remd_recon.foretagkod;
""";
}
