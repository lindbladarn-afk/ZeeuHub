using Dapper;
using Repository.Execution;
using WebApp.Models.Integration.Speedrecon;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.Speedrecon;

// Keeps Speedrecon SQL access tenant-scoped and parameterized.
public sealed class SpeedreconRepository : ISpeedreconRepository
{
    private static readonly string[] TableNames =
    {
        "q_zu_speedrecon",
        "q_zu_speedrecon_result"
    };

    private const string ProbeSql = @"
DECLARE @ObjectNames TABLE (ObjectName sysname NOT NULL, ObjectType nvarchar(20) NOT NULL);

INSERT INTO @ObjectNames (ObjectName, ObjectType)
VALUES
    (N'q_zu_speedrecon', N'Table'),
    (N'q_zu_speedrecon_result', N'Table');

SELECT
    names.ObjectName,
    names.ObjectType,
    CAST(CASE WHEN objects.object_id IS NULL THEN 0 ELSE 1 END AS bit) AS [Exists]
FROM @ObjectNames names
LEFT JOIN sys.objects objects
    ON objects.object_id = OBJECT_ID(N'dbo.' + names.ObjectName)
ORDER BY
    CASE names.ObjectType WHEN N'Table' THEN 0 ELSE 1 END,
    names.ObjectName;

SELECT
    schemas.name AS SchemaName,
    tables.name AS TableName,
    columns.column_id AS ColumnId,
    columns.name AS ColumnName,
    types.name AS DataType,
    columns.max_length AS MaxLength,
    columns.precision AS Precision,
    columns.scale AS Scale,
    columns.is_nullable AS IsNullable,
    defaults.definition AS DefaultDefinition
FROM sys.tables tables
INNER JOIN sys.schemas schemas
    ON schemas.schema_id = tables.schema_id
INNER JOIN sys.columns columns
    ON columns.object_id = tables.object_id
INNER JOIN sys.types types
    ON types.user_type_id = columns.user_type_id
LEFT JOIN sys.default_constraints defaults
    ON defaults.parent_object_id = columns.object_id
   AND defaults.parent_column_id = columns.column_id
WHERE schemas.name = N'dbo'
  AND tables.name IN @TableNames
ORDER BY tables.name, columns.column_id;

DECLARE @IsEnabledInJeeves bit = 0;
IF OBJECT_ID(N'dbo.Jeeves_FN_GetParam') IS NOT NULL
BEGIN
    EXEC sp_executesql
        N'SELECT @EnabledOut = CAST(CASE WHEN dbo.Jeeves_FN_GetParam(@CompanyCodeIn, N''CUSTOM_SPEEDRECON_01'', N''B'', N''0'') = N''1'' THEN 1 ELSE 0 END AS bit);',
        N'@CompanyCodeIn int, @EnabledOut bit OUTPUT',
        @CompanyCodeIn = @CompanyCode,
        @EnabledOut = @IsEnabledInJeeves OUTPUT;
END;
SELECT @IsEnabledInJeeves AS IsEnabledInJeeves;

IF OBJECT_ID(N'dbo.q_zu_speedrecon') IS NOT NULL
   AND COL_LENGTH(N'dbo.q_zu_speedrecon', N'foretagkod') IS NOT NULL
   AND COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_recondate') IS NOT NULL
   AND COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_execdate') IS NOT NULL
   AND COL_LENGTH(N'dbo.q_zu_speedrecon', N'PersSign') IS NOT NULL
BEGIN
    DECLARE @PlanSql nvarchar(max) = N'
SELECT TOP (24)
    CAST(q_zu_speedrecon_recondate AS datetime) AS ReconDate,
    CAST(q_zu_speedrecon_execdate AS datetime) AS ExecDate,
    CAST(COALESCE(PersSign, N'''') AS nvarchar(30)) AS PersSign,
    (' +
    CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_kundresk') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_kundresk, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N' + ' +
    CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_levresk') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_levresk, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N' + ' +
    CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_intlevresk') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_intlevresk, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N' + ' +
    CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_per') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_per, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N' + ' +
    CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_anlag') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_anlag, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N' + ' +
    CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_lager') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_lager, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N' + ' +
    CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_orderunikt') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_orderunikt, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N' + ' +
    CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_inlevejfakt') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_inlevejfakt, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N' + ' +
    CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_utlevejfakt') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_utlevejfakt, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N' + ' +
    CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_lagerflytt') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_lagerflytt, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N' + ' +
    CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_pia') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_pia, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N') AS EnabledChecks,
    CAST(' + CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon', N'q_zu_speedrecon_sparr') IS NULL THEN N'0' ELSE N'CASE WHEN ISNULL(q_zu_speedrecon_sparr, ''0'') = ''1'' THEN 1 ELSE 0 END' END + N' AS bit) AS IsLocked
FROM dbo.q_zu_speedrecon WITH (READUNCOMMITTED)
WHERE foretagkod = @CompanyCodeIn
  AND (@PersSignIn IS NULL OR PersSign = @PersSignIn)
ORDER BY ABS(DATEDIFF(DAY, q_zu_speedrecon_recondate, @ReconDateIn)), q_zu_speedrecon_recondate DESC;';

    EXEC sp_executesql
        @PlanSql,
        N'@CompanyCodeIn int, @PersSignIn nvarchar(30), @ReconDateIn datetime',
        @CompanyCodeIn = @CompanyCode,
        @PersSignIn = @PersSign,
        @ReconDateIn = @ReconDate;
END
ELSE
BEGIN
    SELECT TOP (0)
        CAST(NULL AS datetime) AS ReconDate,
        CAST(NULL AS datetime) AS ExecDate,
        CAST(NULL AS nvarchar(30)) AS PersSign,
        CAST(0 AS int) AS EnabledChecks,
        CAST(0 AS bit) AS IsLocked;
END;

IF OBJECT_ID(N'dbo.q_zu_speedrecon_result') IS NOT NULL
   AND COL_LENGTH(N'dbo.q_zu_speedrecon_result', N'foretagkod') IS NOT NULL
   AND COL_LENGTH(N'dbo.q_zu_speedrecon_result', N'Q_zu_speedrecon_recondate') IS NOT NULL
   AND COL_LENGTH(N'dbo.q_zu_speedrecon_result', N'PersSign') IS NOT NULL
BEGIN
    DECLARE @ResultSql nvarchar(max) = N'
SELECT
    CAST(Q_zu_speedrecon_recondate AS datetime) AS ReconDate,
    CAST(' + CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon_result', N'q_zu_speedrecon_descr') IS NULL THEN N'N'''' ' ELSE N'COALESCE(q_zu_speedrecon_descr, N'''')' END + N' AS nvarchar(50)) AS Description,
    COUNT(1) AS [RowCount],
    CAST(SUM(' + CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon_result', N'Q_zu_speedrecon_reconamount') IS NULL THEN N'0' ELSE N'COALESCE(Q_zu_speedrecon_reconamount, 0)' END + N') AS decimal(18, 2)) AS ReconAmount,
    CAST(SUM(' + CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon_result', N'q_zu_speedrecon_glamount') IS NULL THEN N'0' ELSE N'COALESCE(q_zu_speedrecon_glamount, 0)' END + N') AS decimal(18, 2)) AS GlAmount,
    CAST(SUM(' + CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon_result', N'q_zu_speedrecon_diff') IS NULL THEN N'0' ELSE N'COALESCE(q_zu_speedrecon_diff, 0)' END + N') AS decimal(18, 2)) AS Difference,
    SUM(CASE WHEN ' + CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon_result', N'q_zu_speedrecon_diff') IS NULL THEN N'0' ELSE N'COALESCE(q_zu_speedrecon_diff, 0)' END + N' <> 0 THEN 1 ELSE 0 END) AS DifferenceRows
FROM dbo.q_zu_speedrecon_result WITH (READUNCOMMITTED)
WHERE foretagkod = @CompanyCodeIn
  AND Q_zu_speedrecon_recondate = @ReconDateIn
  AND (@PersSignIn IS NULL OR PersSign = @PersSignIn)
GROUP BY Q_zu_speedrecon_recondate' + CASE WHEN COL_LENGTH(N'dbo.q_zu_speedrecon_result', N'q_zu_speedrecon_descr') IS NULL THEN N'' ELSE N', COALESCE(q_zu_speedrecon_descr, N'''')' END + N'
ORDER BY Description;';

    EXEC sp_executesql
        @ResultSql,
        N'@CompanyCodeIn int, @PersSignIn nvarchar(30), @ReconDateIn datetime',
        @CompanyCodeIn = @CompanyCode,
        @PersSignIn = @PersSign,
        @ReconDateIn = @ReconDate;
END
ELSE
BEGIN
    SELECT TOP (0)
        CAST(NULL AS datetime) AS ReconDate,
        CAST(NULL AS nvarchar(50)) AS Description,
        CAST(0 AS int) AS [RowCount],
        CAST(0 AS decimal(18, 2)) AS ReconAmount,
        CAST(0 AS decimal(18, 2)) AS GlAmount,
        CAST(0 AS decimal(18, 2)) AS Difference,
        CAST(0 AS int) AS DifferenceRows;
END;";

    private const string RunPlansSql = @"
SELECT
    CAST(q_zu_speedrecon_execdate AS datetime) AS ExecDate,
    CAST(q_zu_speedrecon_recondate AS datetime) AS ReconDate,
    CAST(CASE WHEN ISNULL(q_zu_speedrecon_kundresk, '0') = '1' THEN 1 ELSE 0 END AS bit) AS Kundreskontra,
    CAST(CASE WHEN ISNULL(q_zu_speedrecon_levresk, '0') = '1' THEN 1 ELSE 0 END AS bit) AS Leverantorsreskontra,
    CAST(CASE WHEN ISNULL(q_zu_speedrecon_anlag, '0') = '1' THEN 1 ELSE 0 END AS bit) AS Anlaggning,
    CAST(CASE WHEN ISNULL(q_zu_speedrecon_inlevejfakt, '0') = '1' THEN 1 ELSE 0 END AS bit) AS InlevereratEjFakturerat,
    CAST(CASE WHEN ISNULL(q_zu_speedrecon_intlevresk, '0') = '1' THEN 1 ELSE 0 END AS bit) AS InternLeverantorsreskontra,
    CAST(CASE WHEN ISNULL(q_zu_speedrecon_lager, '0') = '1' THEN 1 ELSE 0 END AS bit) AS Lagervarde,
    CAST(CASE WHEN ISNULL(q_zu_speedrecon_lagerflytt, '0') = '1' THEN 1 ELSE 0 END AS bit) AS Lagerflytt,
    CAST(CASE WHEN ISNULL(q_zu_speedrecon_orderunikt, '0') = '1' THEN 1 ELSE 0 END AS bit) AS Orderunik,
    CAST(CASE WHEN ISNULL(q_zu_speedrecon_per, '0') = '1' THEN 1 ELSE 0 END AS bit) AS Periodisering,
    CAST(CASE WHEN ISNULL(q_zu_speedrecon_pia, '0') = '1' THEN 1 ELSE 0 END AS bit) AS Pia,
    CAST(CASE WHEN ISNULL(q_zu_speedrecon_utlevejfakt, '0') = '1' THEN 1 ELSE 0 END AS bit) AS UtlevereratEjFakturerat
FROM dbo.q_zu_speedrecon WITH (READUNCOMMITTED)
WHERE foretagkod = @CompanyCode
  AND perssign = @PersSign
  AND q_zu_speedrecon_recondate = @ReconDate;";

    private const string DeleteResultRowsSql = @"
DELETE FROM dbo.q_zu_speedrecon_result
WHERE foretagkod = @CompanyCode
  AND PersSign = @PersSign
  AND q_zu_speedrecon_recondate = @ReconDate
  AND q_zu_speedrecon_descr IN @Descriptions;";

    private const string UpdateGeneralLedgerAmountsSql = @"
DECLARE @Redar smallint;
DECLARE @Period smallint;
DECLARE @RedarStart datetime;

EXEC dbo.CalcPeriod
    @InpDat = @ReconDate,
    @CalledFromProcedure = NULL,
    @Just_orddat = NULL,
    @c_intrnCoNo = @CompanyCode,
    @Redovisnar = @Redar OUTPUT,
    @Period = @Period OUTPUT;

SELECT @RedarStart = DATEADD(DAY, 1, EOMONTH(periodend, -1))
FROM dbo.per WITH (READUNCOMMITTED)
WHERE foretagkod = @CompanyCode
  AND redovisnar = @Redar
  AND period = 1;

WITH GeneralLedgerBalances AS
(
    SELECT source.ktonr, SUM(source.Amount) AS Amount
    FROM
    (
        SELECT vr.ktonr, SUM(vr.debbel - vr.krebel) AS Amount
        FROM dbo.vr WITH (READUNCOMMITTED)
        WHERE vr.foretagkod = @CompanyCode
          AND vr.redovisnar = @Redar
          AND vr.period = 0
          AND vr.VrUpdateCode = 8
        GROUP BY vr.ktonr

        UNION ALL

        SELECT vr.ktonr, SUM(vr.debbel - vr.krebel) AS Amount
        FROM dbo.vr WITH (READUNCOMMITTED)
        WHERE vr.foretagkod = @CompanyCode
          AND vr.BokfDat >= @RedarStart
          AND vr.BokfDat <= @ReconDate
          AND vr.VrUpdateCode = 8
          AND vr.period > 0
        GROUP BY vr.ktonr
    ) source
    GROUP BY source.ktonr
)
UPDATE result
SET q_zu_speedrecon_glamount = ISNULL(balances.Amount, 0),
    q_zu_speedrecon_diff = ISNULL(result.Q_zu_speedrecon_reconamount, 0) - ISNULL(balances.Amount, 0)
FROM dbo.q_zu_speedrecon_result result
LEFT JOIN GeneralLedgerBalances balances
    ON balances.ktonr = result.q_zu_speedrecon_ktonr
WHERE result.foretagkod = @CompanyCode
  AND result.Q_zu_speedrecon_recondate = @ReconDate
  AND result.PersSign = @PersSign;";

    private const string CreateYearSql = @"
INSERT INTO dbo.q_zu_speedrecon
(
    foretagkod,
    q_zu_speedrecon_execdate,
    q_zu_speedrecon_recondate,
    PersSign,
    RowCreatedBy,
    RowCreatedDt,
    q_zu_speedrecon_kundresk,
    q_zu_speedrecon_levresk,
    q_zu_speedrecon_intlevresk,
    q_zu_speedrecon_per,
    q_zu_speedrecon_anlag,
    q_zu_speedrecon_lager,
    q_zu_speedrecon_orderunikt,
    q_zu_speedrecon_inlevejfakt,
    q_zu_speedrecon_utlevejfakt,
    q_zu_speedrecon_lagerflytt,
    q_zu_speedrecon_pia
)
SELECT
    @CompanyCode,
    DATEADD(DAY, 1, CONVERT(date, per.periodend)),
    CONVERT(date, per.periodend),
    @PersSign,
    @PersSign,
    GETDATE(),
    '1',
    '1',
    '1',
    '1',
    '1',
    '1',
    '1',
    '1',
    '1',
    '1',
    '1'
FROM dbo.per per WITH (READUNCOMMITTED)
WHERE per.foretagkod = @CompanyCode
  AND per.redovisnar = @FiscalYear
  AND per.bokslutsperiod = 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.q_zu_speedrecon existing WITH (READUNCOMMITTED)
      WHERE existing.foretagkod = @CompanyCode
        AND existing.PersSign = @PersSign
        AND existing.q_zu_speedrecon_recondate = CONVERT(date, per.periodend)
  );

SELECT @@ROWCOUNT;";

    private const string StandaloneDepreciationSql = @"
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
    'ANLAVSKR',
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

SELECT @@ROWCOUNT;";

    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

    public SpeedreconRepository(IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public async Task<SpeedreconProbeResult> ProbeAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        CancellationToken cancellationToken = default)
    {
        return await _jeevesSqlExecutor.WithConnectionAsync(
            runtimeContext.ConnectionString,
            async connection =>
            {
                var command = new CommandDefinition(
                    ProbeSql,
                    new
                    {
                        TableNames,
                        CompanyCode = runtimeContext.CompanyCode,
                        PersSign = NormalizePersSign(runtimeContext.PersSign),
                        ReconDate = reconDate.Date
                    },
                    commandTimeout: 30,
                    cancellationToken: cancellationToken);

                using var multi = await connection.QueryMultipleAsync(command);
                var objects = (await multi.ReadAsync<SpeedreconObjectStatus>()).ToList();
                var columns = (await multi.ReadAsync<SpeedreconSchemaColumn>()).ToList();
                var enabled = await multi.ReadFirstOrDefaultAsync<bool>();
                var planRows = (await multi.ReadAsync<SpeedreconPlanRow>()).ToList();
                var resultSummary = (await multi.ReadAsync<SpeedreconResultSummaryRow>()).ToList();

                return new SpeedreconProbeResult
                {
                    CompanyCode = runtimeContext.CompanyCode,
                    CompanyName = runtimeContext.CompanyName,
                    PersSign = runtimeContext.PersSign,
                    RuntimeAvailable = true,
                    IsEnabledInJeeves = enabled,
                    ProbeTimeUtc = DateTime.UtcNow,
                    Objects = objects,
                    Columns = columns,
                    PlanRows = planRows,
                    ResultSummary = resultSummary
                };
            },
            operationName: "SpeedreconRepository.Probe");
    }

    public async Task<IReadOnlyList<SpeedreconRunPlan>> GetRunPlansAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        CancellationToken cancellationToken = default)
    {
        return await _jeevesSqlExecutor.QueryAsync<SpeedreconRunPlan>(
            runtimeContext.ConnectionString,
            RunPlansSql,
            new
            {
                CompanyCode = runtimeContext.CompanyCode,
                PersSign = NormalizePersSign(runtimeContext.PersSign) ?? runtimeContext.UserId,
                ReconDate = reconDate.Date
            },
            commandTimeoutSeconds: 30,
            cancellationToken: cancellationToken,
            operationName: "SpeedreconRepository.GetRunPlans");
    }

    public async Task DeleteResultRowsAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        IReadOnlyCollection<string> descriptions,
        CancellationToken cancellationToken = default)
    {
        if (descriptions.Count == 0)
            return;

        await _jeevesSqlExecutor.ExecuteAsync(
            runtimeContext.ConnectionString,
            DeleteResultRowsSql,
            new
            {
                CompanyCode = runtimeContext.CompanyCode,
                PersSign = NormalizePersSign(runtimeContext.PersSign) ?? runtimeContext.UserId,
                ReconDate = reconDate.Date,
                Descriptions = descriptions
            },
            commandTimeoutSeconds: 60,
            cancellationToken: cancellationToken,
            operationName: "SpeedreconRepository.DeleteResultRows");
    }

    public async Task ExecuteBatchAsync(
        JeevesRuntimeContext runtimeContext,
        string sql,
        DateTime reconDate,
        CancellationToken cancellationToken = default)
    {
        await _jeevesSqlExecutor.ExecuteAsync(
            runtimeContext.ConnectionString,
            sql,
            new
            {
                CompanyCode = runtimeContext.CompanyCode,
                PersSign = NormalizePersSign(runtimeContext.PersSign) ?? runtimeContext.UserId,
                ReconDate = reconDate.Date
            },
            commandTimeoutSeconds: 1200,
            cancellationToken: cancellationToken,
            operationName: "SpeedreconRepository.ExecuteModuleBatch");
    }

    public async Task UpdateGeneralLedgerAmountsAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        CancellationToken cancellationToken = default)
    {
        await _jeevesSqlExecutor.ExecuteAsync(
            runtimeContext.ConnectionString,
            UpdateGeneralLedgerAmountsSql,
            new
            {
                CompanyCode = runtimeContext.CompanyCode,
                PersSign = NormalizePersSign(runtimeContext.PersSign) ?? runtimeContext.UserId,
                ReconDate = reconDate.Date
            },
            commandTimeoutSeconds: 300,
            cancellationToken: cancellationToken,
            operationName: "SpeedreconRepository.UpdateGeneralLedgerAmounts");
    }

    public async Task<int> CreateYearAsync(
        JeevesRuntimeContext runtimeContext,
        int fiscalYear,
        CancellationToken cancellationToken = default)
        => await _jeevesSqlExecutor.ExecuteScalarAsync<int>(
               runtimeContext.ConnectionString,
               CreateYearSql,
               new
               {
                   CompanyCode = runtimeContext.CompanyCode,
                   PersSign = NormalizePersSign(runtimeContext.PersSign) ?? runtimeContext.UserId,
                   FiscalYear = fiscalYear
               },
               commandTimeoutSeconds: 60,
               cancellationToken: cancellationToken,
               operationName: "SpeedreconRepository.CreateYear");

    public async Task<int> RunStandaloneDepreciationAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        CancellationToken cancellationToken = default)
        => await _jeevesSqlExecutor.ExecuteScalarAsync<int>(
               runtimeContext.ConnectionString,
               StandaloneDepreciationSql,
               new
               {
                   CompanyCode = runtimeContext.CompanyCode,
                   PersSign = NormalizePersSign(runtimeContext.PersSign) ?? runtimeContext.UserId,
                   ReconDate = reconDate.Date
               },
               commandTimeoutSeconds: 300,
               cancellationToken: cancellationToken,
               operationName: "SpeedreconRepository.RunStandaloneDepreciation");

    private static string? NormalizePersSign(string? persSign)
        => string.IsNullOrWhiteSpace(persSign) ? null : persSign.Trim();
}
