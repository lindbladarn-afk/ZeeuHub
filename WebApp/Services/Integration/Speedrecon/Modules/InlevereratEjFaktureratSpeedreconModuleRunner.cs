namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the original q_zu_speedrecon_inlevejfakt calculation into a hub-owned module.
public sealed class InlevereratEjFaktureratSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public InlevereratEjFaktureratSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.InlevereratEjFakturerat, repository)
    {
    }

    protected override string Sql => """
DECLARE @Redar smallint;
DECLARE @Period smallint;
DECLARE @Today datetime = CONVERT(varchar, GETDATE(), 12);
DECLARE @AkltKtonr nvarchar(8);

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

SELECT @AkltKtonr = aklt.ktonr
FROM dbo.aklt aklt WITH (READUNCOMMITTED)
WHERE aklt.foretagkod = @CompanyCode
  AND aklt.RedovisnAr = @Redar
  AND aklt.lagtranstyp = 110
  AND aklt.intfield = 1;

SET @AkltKtonr = ISNULL(@AkltKtonr, '0');

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
    'INLEVEJFAKT'
FROM dbo.wbpa wbpa WITH (READUNCOMMITTED)
INNER JOIN dbo.ko ko WITH (READUNCOMMITTED)
    ON wbpa.foretagkod = ko.foretagkod
   AND wbpa.redovisnar = ko.redovisnar
   AND wbpa.ktonr = ko.ktonr
WHERE wbpa.foretagkod = @CompanyCode
  AND wbpa.rowcreatedby = @PersSign
  AND wbpa.regdat = @Today
  AND wbpa.ktonr = @AkltKtonr
GROUP BY
    wbpa.ktonr,
    wbpa.recondate,
    wbpa.perssign,
    wbpa.foretagkod,
    ko.ktobeskr;
""";
}
