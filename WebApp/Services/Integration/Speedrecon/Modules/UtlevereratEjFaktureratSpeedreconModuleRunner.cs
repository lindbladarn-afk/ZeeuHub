namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the original q_zu_speedrecon_utlevejfakt calculation into a hub-owned module.
public sealed class UtlevereratEjFaktureratSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public UtlevereratEjFaktureratSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.UtlevereratEjFakturerat, repository)
    {
    }

    protected override string Sql => """
DECLARE @Today varchar(6) = CONVERT(varchar, GETDATE(), 12);

EXEC dbo.jeeves_oru_sal_PerDen
    @c_Datum = @ReconDate,
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
    woha.ktonr,
    ko.ktobeskr,
    ISNULL(SUM(woha.delnotinvoice), 0),
    woha.recondate,
    woha.perssign,
    woha.foretagkod,
    'UTLEVEJFAKT'
FROM dbo.woha woha WITH (READUNCOMMITTED)
LEFT JOIN dbo.ko ko WITH (READUNCOMMITTED)
    ON woha.foretagkod = ko.foretagkod
   AND woha.ktonr = ko.ktonr
   AND woha.Redovisnar = ko.Redovisnar
WHERE woha.foretagkod = @CompanyCode
  AND woha.rowcreatedby = @PersSign
  AND woha.regdat = @Today
GROUP BY
    woha.ktonr,
    ko.ktobeskr,
    woha.recondate,
    woha.ForetagKod,
    woha.perssign;
""";
}
