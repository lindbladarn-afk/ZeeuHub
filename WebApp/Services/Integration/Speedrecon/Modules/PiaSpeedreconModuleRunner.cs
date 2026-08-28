namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the original q_zu_speedrecon_pia calculation into a hub-owned module.
public sealed class PiaSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public PiaSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.Pia, repository)
    {
    }

    protected override string Sql => """
DECLARE @Redar smallint;
DECLARE @Period smallint;
DECLARE @AkltKtonr nvarchar(8);
DECLARE @Today datetime = CONVERT(date, GETDATE());

EXEC dbo.CalcPeriod
    @InpDat = @ReconDate,
    @CalledFromProcedure = NULL,
    @Just_orddat = NULL,
    @c_intrnCoNo = @CompanyCode,
    @Redovisnar = @Redar OUTPUT,
    @Period = @Period OUTPUT;

EXEC dbo.jeeves_th_sal_PerDen
    @c_PerDenDatum = @ReconDate,
    @c_ForetagKod = @CompanyCode,
    @c_Visa_AONr = '+',
    @c_Perssign = @PersSign;

IF EXISTS (
    SELECT 1
    FROM dbo.aklt
    WHERE foretagkod = @CompanyCode
      AND redovisnar = @Redar
      AND lagtranstyp = 0
      AND intfield = 5
)
BEGIN
    SELECT @AkltKtonr = ktonr
    FROM dbo.aklt WITH (READUNCOMMITTED)
    WHERE foretagkod = @CompanyCode
      AND redovisnar = @Redar
      AND lagtranstyp = 0
      AND intfield = 5;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.aklt
    WHERE foretagkod = @CompanyCode
      AND redovisnar = @Redar
      AND lagtranstyp = 120
      AND intfield = 5
)
BEGIN
    SELECT @AkltKtonr = ktonr
    FROM dbo.aklt WITH (READUNCOMMITTED)
    WHERE foretagkod = @CompanyCode
      AND redovisnar = @Redar
      AND lagtranstyp = 120
      AND intfield = 5;
END;

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
    wtha.ktonr,
    ISNULL(SUM(wtha.debcresumamt), 0),
    wtha.Recondate,
    wtha.PersSign,
    wtha.ForetagKod,
    ko.ktobeskr,
    'PIA'
FROM dbo.wtha wtha
INNER JOIN dbo.ko ko
    ON wtha.foretagkod = ko.foretagkod
   AND wtha.ktonr = ko.ktonr
   AND wtha.Redovisnar = ko.Redovisnar
WHERE wtha.foretagkod = @CompanyCode
  AND wtha.rowcreatedby = @PersSign
  AND wtha.regdat = @Today
  AND wtha.ktonr = @AkltKtonr
GROUP BY
    wtha.ktonr,
    wtha.Recondate,
    wtha.PersSign,
    wtha.ForetagKod,
    ko.ktobeskr;
""";
}
