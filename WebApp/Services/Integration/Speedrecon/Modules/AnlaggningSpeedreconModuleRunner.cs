namespace WebApp.Services.Integration.Speedrecon.Modules;

// Ports the original q_zu_speedrecon_anlaggning calculation into a hub-owned module.
public sealed class AnlaggningSpeedreconModuleRunner : SpeedreconSqlBatchModuleRunner
{
    public AnlaggningSpeedreconModuleRunner(ISpeedreconRepository repository)
        : base(SpeedreconModuleDefinitions.Anlaggning, repository)
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

EXEC dbo.jeeves_inv_sal_PerDen
    @ReconDate,
    @CompanyCode,
    '+',
    NULL,
    NULL,
    NULL,
    @PersSign,
    NULL,
    NULL;

INSERT INTO dbo.q_zu_speedrecon_result
(
    q_zu_speedrecon_ktonr,
    Q_zu_speedrecon_reconamount,
    Q_zu_speedrecon_recondate,
    perssign,
    foretagkod,
    q_zu_speedrecon_descr,
    ktobeskr
)
SELECT
    intk.ktonr,
    SUM(winvt.perdenanskbelopp),
    winvt.facheckdate,
    winvt.perssign,
    winvt.foretagkod,
    'ANLAG',
    ko.ktobeskr
FROM dbo.winvt winvt WITH (READUNCOMMITTED)
INNER JOIN dbo.intk intk WITH (READUNCOMMITTED)
    ON winvt.foretagkod = intk.foretagkod
   AND winvt.anlgrp = intk.anlgrp
INNER JOIN dbo.ko ko WITH (READUNCOMMITTED)
    ON intk.ForetagKod = ko.ForetagKod
   AND intk.Redovisnar = ko.Redovisnar
   AND intk.ktonr = ko.ktonr
WHERE winvt.foretagkod = @CompanyCode
  AND winvt.rowcreatedby = @PersSign
  AND winvt.regdat = @Today
  AND intk.intfield = 0
  AND intk.redovisnar = @Redar
GROUP BY
    intk.ktonr,
    winvt.facheckdate,
    winvt.perssign,
    winvt.foretagkod,
    ko.ktobeskr;

INSERT INTO dbo.q_zu_speedrecon_result
(
    q_zu_speedrecon_ktonr,
    Q_zu_speedrecon_reconamount,
    Q_zu_speedrecon_recondate,
    perssign,
    foretagkod,
    q_zu_speedrecon_descr,
    ktobeskr
)
SELECT
    intk.ktonr,
    ISNULL(SUM(winvt.perdenavskrbelopp), 0) * -1,
    winvt.facheckdate,
    winvt.perssign,
    winvt.foretagkod,
    'AVSKR',
    ko.ktobeskr
FROM dbo.winvt winvt WITH (READUNCOMMITTED)
INNER JOIN dbo.intk intk WITH (READUNCOMMITTED)
    ON winvt.foretagkod = intk.foretagkod
   AND winvt.anlgrp = intk.anlgrp
INNER JOIN dbo.ko ko WITH (READUNCOMMITTED)
    ON intk.ForetagKod = ko.ForetagKod
   AND intk.Redovisnar = ko.Redovisnar
   AND intk.ktonr = ko.ktonr
WHERE winvt.foretagkod = @CompanyCode
  AND winvt.rowcreatedby = @PersSign
  AND winvt.regdat = @Today
  AND intk.intfield = 1
  AND intk.redovisnar = @Redar
GROUP BY
    intk.ktonr,
    winvt.facheckdate,
    winvt.perssign,
    winvt.foretagkod,
    ko.ktobeskr;
""";
}
