// Verifies Intelligence routing, including low-cost templates for common business questions.
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WebApp.Models.AI;
using WebApp.Services.Application;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiDbChatOrchestratorFlowTests
{
    [Fact]
    public async Task AskDatabaseAsync_UsesVerifiedEntityListBeforeGenerativePlanning()
    {
        var chat = new QueueChatService("Här är kunderna.");
        var executor = new RecordingSqlExecutor(sql =>
            SuccessResult(sql, ["Customer No", "Customer"], ["1001", "Acme AB"]));
        var orchestrator = CreateOrchestrator(chat, executor);

        var response = await orchestrator.AskDatabaseAsync(new AiQueryRequest
        {
            Question = "Visa mina kunder",
            Source = "intelligence",
            DataSourceKey = $"model-first-{Guid.NewGuid():N}"
        });

        Assert.True(response.Success);
        Assert.Contains("SELECT TOP (200)", executor.DataQueries.Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(executor.DataQueries.Single(), response.Sql);
        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task AskDatabaseAsync_RepairsFailedModelSqlBeforeReturningError()
    {
        const string failedSql =
            "SELECT [Missing] FROM [dbo].[q_zu_bi_customer]";
        const string repairedSql =
            "SELECT [Customer No], [Customer] FROM [dbo].[q_zu_bi_customer] ORDER BY [Customer]";
        var chat = new QueueChatService(
            StructuredSql(failedSql),
            StructuredSql(repairedSql),
            "Här är kunderna.");
        var executor = new RecordingSqlExecutor(sql =>
            sql.Contains("[Missing]", StringComparison.OrdinalIgnoreCase)
                ? FailedResult(sql, "Invalid column name 'Missing'.")
                : SuccessResult(sql, ["Customer No", "Customer"], ["1001", "Acme AB"]));
        var orchestrator = CreateOrchestrator(chat, executor);

        var response = await orchestrator.AskDatabaseAsync(new AiQueryRequest
        {
            Question = "Visa kundernas interna klassificering",
            Source = "intelligence",
            DataSourceKey = $"repair-{Guid.NewGuid():N}"
        });

        Assert.True(response.Success);
        Assert.Equal([failedSql, repairedSql], executor.DataQueries);
        Assert.Equal(repairedSql, response.Sql);
        Assert.Contains("reparerades automatiskt", response.Warning, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, chat.CallCount);
    }

    [Fact]
    public async Task AskDatabaseAsync_RechecksFullSchemaBeforeRequestingClarification()
    {
        const string modelSql =
            "SELECT [Customer No], [Customer] FROM [dbo].[q_zu_bi_customer] ORDER BY [Customer]";
        var chat = new QueueChatService(
            Clarification("Jag hittar inte rätt kundfält i urvalet."),
            StructuredSql(modelSql),
            "Här är kunderna.");
        var executor = new RecordingSqlExecutor(sql =>
            SuccessResult(sql, ["Customer No", "Customer"], ["1001", "Acme AB"]));
        var orchestrator = CreateOrchestrator(chat, executor);

        var response = await orchestrator.AskDatabaseAsync(new AiQueryRequest
        {
            Question = "Visa kundernas interna klassificering",
            Source = "intelligence",
            DataSourceKey = $"full-schema-{Guid.NewGuid():N}"
        });

        Assert.True(response.Success);
        Assert.Equal(modelSql, response.Sql);
        Assert.Equal(3, chat.CallCount);
    }

    [Fact]
    public async Task AskDatabaseAsync_UsesPreviousResultIdentifierForFollowUpQuestion()
    {
        const string topArticleSql =
            "SELECT [ProductID], [Revenue] FROM [dbo].[q_zu_bi_fsg] ORDER BY [Revenue] DESC";
        const string articleNameSql =
            "SELECT [ProductID], [ProductName] FROM [dbo].[q_zu_bi_item] WHERE [ProductID] = '101001'";
        var chat = new QueueChatService(
            StructuredSql(topArticleSql),
            "Toppartikeln är 101001.",
            StructuredSql(articleNameSql),
            "Artikel 101001 heter Konferensstol.");
        chat.OnAsk = (callCount, userMessage, history) =>
        {
            if (callCount != 3)
                return;

            var context = Assert.Single(history!, message =>
                message.Role == "system" &&
                message.Content.StartsWith("LATEST DATABASE RESULT CONTEXT", StringComparison.Ordinal));
            Assert.Contains("ProductID=101001", context.Content, StringComparison.Ordinal);
            Assert.Contains("Revenue=100100", context.Content, StringComparison.Ordinal);
            Assert.Contains("FOLLOW-UP REFERENCE", userMessage, StringComparison.Ordinal);
            Assert.Contains("ProductID=101001", userMessage, StringComparison.Ordinal);
        };
        var executor = new RecordingSqlExecutor(sql =>
            sql.Contains("[ProductName]", StringComparison.OrdinalIgnoreCase)
                ? SuccessResult(sql, ["ProductID", "ProductName"], ["101001", "Konferensstol"])
                : SuccessResult(sql, ["ProductID", "Revenue"], ["101001", 100100m]));
        var memory = new FakeConversationMemory();
        var orchestrator = CreateOrchestrator(chat, executor, memory);
        var dataSourceKey = $"follow-up-{Guid.NewGuid():N}";

        await orchestrator.AskDatabaseAsync(new AiQueryRequest
        {
            Question = "toppartiklar",
            Source = "intelligence",
            DataSourceKey = dataSourceKey
        });
        var response = await orchestrator.AskDatabaseAsync(new AiQueryRequest
        {
            Question = "Vad är det för artikel i namn?",
            Source = "intelligence",
            DataSourceKey = dataSourceKey
        });

        Assert.True(response.Success);
        Assert.Equal(articleNameSql, response.Sql);
        Assert.Equal(4, chat.CallCount);
    }

    [Fact]
    public async Task AskDatabaseAsync_UsesVerifiedCustomerRankingBeforeGenerativePlanning()
    {
        var chat = new QueueChatService("Tyggrossisten AB (10001): 269 114,25 kr.");
        var executor = new RecordingSqlExecutor(
            sql => SuccessResult(
                sql,
                ["CustomerID", "CustomerName", "TotalOmsatt"],
                ["10001", "Tyggrossisten AB", 269114.25m]),
            schemaRows:
            [
                ["dbo", "q_zu_bi_fsg", "Customer No", "nvarchar", false, 10L, null],
                ["dbo", "q_zu_bi_fsg", "Customer", "nvarchar", false, 80L, null],
                ["dbo", "q_zu_bi_fsg", "Invoice Date", "datetime", false, null, null],
                ["dbo", "q_zu_bi_fsg", "Invoice Row SUM", "money", false, null, null]
            ]);
        var orchestrator = CreateOrchestrator(chat, executor);

        var response = await orchestrator.AskDatabaseAsync(new AiQueryRequest
        {
            Question = "Visa de fem största kunderna i år",
            Source = "intelligence",
            DataSourceKey = $"verified-ranking-{Guid.NewGuid():N}"
        });

        Assert.True(response.Success);
        Assert.Contains("SELECT TOP (5)", response.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AS [CustomerID]", response.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AS [CustomerName]", response.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["CustomerID", "CustomerName", "TotalOmsatt"], response.Columns);
        Assert.Equal(1, chat.CallCount);
    }

    [Fact]
    public async Task AskDatabaseAsync_RepairsResultThatDoesNotFulfillComparisonPlan()
    {
        const string incompleteSql =
            "SELECT SUM([Invoice Row SUM]) AS [CurrentPeriod] FROM [dbo].[q_zu_bi_fsg]";
        const string repairedSql =
            "SELECT SUM([Invoice Row SUM]) AS [CurrentPeriod], 90 AS [PreviousPeriod], SUM([Invoice Row SUM]) - 90 AS [Difference] FROM [dbo].[q_zu_bi_fsg]";
        var chat = new QueueChatService(
            StructuredSql(incompleteSql, intent: "comparison", metric: "net_revenue", period: "current_year"),
            StructuredSql(repairedSql, intent: "comparison", metric: "net_revenue", period: "current_year"),
            "Omsättningen är 10 kr högre i år.");
        var executor = new RecordingSqlExecutor(sql =>
            sql.Contains("[PreviousPeriod]", StringComparison.OrdinalIgnoreCase)
                ? SuccessResult(sql, ["CurrentPeriod", "PreviousPeriod", "Difference"], [100m, 90m, 10m])
                : SuccessResult(sql, ["CurrentPeriod"], [100m]));
        var orchestrator = CreateOrchestrator(chat, executor);

        var response = await orchestrator.AskDatabaseAsync(new AiQueryRequest
        {
            Question = "Hur ligger vi till mot förra året?",
            Source = "intelligence",
            DataSourceKey = $"contract-repair-{Guid.NewGuid():N}"
        });

        Assert.True(response.Success);
        Assert.Equal([incompleteSql, repairedSql], executor.DataQueries);
        Assert.Equal(["CurrentPeriod", "PreviousPeriod", "Difference"], response.Columns);
        Assert.Contains("inte uppfyllde analysplanen", response.Warning, StringComparison.Ordinal);
        Assert.Equal(3, chat.CallCount);
    }

    [Fact]
    public async Task AskDatabaseAsync_ExpandsMonthlyBreakdownWithPreviousPeriod()
    {
        var chat = new QueueChatService(
            "Här är kunderna.",
            "Här är omsättningen per månad.");
        var executor = new RecordingSqlExecutor(
            sql => sql.Contains("[Month]", StringComparison.OrdinalIgnoreCase)
                ? SuccessResult(sql, ["Month", "Revenue"], [new DateTime(2026, 1, 1), 100m])
                : SuccessResult(sql, ["CustomerID", "CustomerName", "Revenue"], ["10001", "Tyggrossisten AB", 100m]),
            schemaRows:
            [
                ["dbo", "q_zu_bi_fsg", "Customer No", "nvarchar", false, 10L, null],
                ["dbo", "q_zu_bi_fsg", "Customer", "nvarchar", false, 80L, null],
                ["dbo", "q_zu_bi_fsg", "Invoice Date", "datetime", false, null, null],
                ["dbo", "q_zu_bi_fsg", "Invoice Row SUM", "money", false, null, null]
            ]);
        var orchestrator = CreateOrchestrator(chat, executor);
        var dataSourceKey = $"monthly-follow-up-{Guid.NewGuid():N}";

        await orchestrator.AskDatabaseAsync(new AiQueryRequest
        {
            Question = "Visa de fem största kunderna i år",
            Source = "intelligence",
            DataSourceKey = dataSourceKey
        });
        var response = await orchestrator.AskDatabaseAsync(new AiQueryRequest
        {
            Question = "Bryt ned omsättningen per månad",
            Source = "intelligence",
            DataSourceKey = dataSourceKey
        });

        Assert.True(response.Success);
        Assert.Contains("CONVERT(char(7)", response.Sql, StringComparison.Ordinal);
        Assert.Equal(2, chat.CallCount);
    }

    private static AiDbChatOrchestrator CreateOrchestrator(
        IOpenAiChatService chat,
        IAiSqlExecutor executor,
        IAiConversationMemory? memory = null)
    {
        return new AiDbChatOrchestrator(
            new FakeDataSourceResolver(),
            executor,
            chat,
            new FakeHostEnvironment(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            memory ?? new FakeConversationMemory(),
            new NullInvoiceQuestionService(),
            new AiSemanticCatalog(),
            new AiSqlSecurityPolicy(),
            new AiResultVerifier(),
            new AiPromptDataPolicy());
    }

    private static string StructuredSql(
        string sql,
        string intent = "lookup",
        string metric = "custom",
        string? period = null)
    {
        return $$"""
        {
          "plan": {
            "intent": "{{intent}}",
            "metric": "{{metric}}",
            "dimensions": ["customer"],
            "filters": [],
            "period": {{(period is null ? "null" : $"\"{period}\"")}},
            "sort": "ascending",
            "limit": 200,
            "assumptions": []
          },
          "sql": "{{sql}}",
          "requires_clarification": false,
          "reason": ""
        }
        """;
    }

    private static string Clarification(string reason)
    {
        return $$"""
        {
          "plan": {
            "intent": "lookup",
            "metric": "custom",
            "dimensions": ["customer"],
            "filters": [],
            "period": null,
            "sort": null,
            "limit": null,
            "assumptions": []
          },
          "sql": "",
          "requires_clarification": true,
          "reason": "{{reason}}"
        }
        """;
    }

    private static SqlQueryResult SuccessResult(
        string sql,
        IReadOnlyList<string> columns,
        IReadOnlyList<object?> row)
    {
        var result = new SqlQueryResult
        {
            Success = true,
            RowCount = 1,
            ExecutedSql = sql
        };
        result.Columns.AddRange(columns);
        result.Rows.Add(row.ToList());
        return result;
    }

    private static SqlQueryResult FailedResult(string sql, string error) =>
        new()
        {
            Success = false,
            Error = error,
            ExecutedSql = sql
        };

    private sealed class RecordingSqlExecutor : IAiSqlExecutor
    {
        private readonly Func<string, SqlQueryResult> _executeDataQuery;
        private readonly IReadOnlyList<IReadOnlyList<object?>>? _schemaRows;

        public RecordingSqlExecutor(
            Func<string, SqlQueryResult> executeDataQuery,
            IReadOnlyList<IReadOnlyList<object?>>? schemaRows = null)
        {
            _executeDataQuery = executeDataQuery;
            _schemaRows = schemaRows;
        }

        public List<string> DataQueries { get; } = [];

        public Task<SqlQueryResult> ExecuteSelectAsync(
            string connectionString,
            string sql,
            int maxRows = 200,
            CancellationToken ct = default,
            bool allowSchemaIntrospection = false)
        {
            if (allowSchemaIntrospection)
            {
                return Task.FromResult(sql.Contains("foreign_key_columns", StringComparison.OrdinalIgnoreCase)
                    ? new SqlQueryResult { Success = true }
                    : SchemaResult());
            }

            DataQueries.Add(sql);
            return Task.FromResult(_executeDataQuery(sql));
        }

        private SqlQueryResult SchemaResult()
        {
            IReadOnlyList<IReadOnlyList<object?>> rows = _schemaRows ??
            [
                ["dbo", "q_zu_bi_customer", "Customer No", "nvarchar", false, 10L, null],
                ["dbo", "q_zu_bi_customer", "Customer", "nvarchar", false, 10L, null]
            ];
            var result = new SqlQueryResult { Success = true, RowCount = rows.Count };
            foreach (var row in rows)
                result.Rows.Add(row.ToList());
            return result;
        }
    }

    private sealed class QueueChatService(params string[] responses) : IOpenAiChatService
    {
        private readonly Queue<string> _responses = new(responses);

        public int CallCount { get; private set; }
        public Action<int, string, IReadOnlyList<OpenAiChatMessage>?>? OnAsk { get; set; }

        public Task<OpenAiChatResult> AskAsync(
            string userMessage,
            IReadOnlyList<OpenAiChatMessage>? history = null,
            CancellationToken ct = default)
        {
            CallCount++;
            OnAsk?.Invoke(CallCount, userMessage, history);
            return Task.FromResult(new OpenAiChatResult
            {
                Answer = _responses.Dequeue()
            });
        }
    }

    private sealed class FakeDataSourceResolver : IAiDataSourceResolver
    {
        public IReadOnlyList<AiDataSourceInfo> GetConfiguredDataSources() => [];

        public Task<(string ConnectionString, AiDataSourceInfo Info)> ResolveAsync(
            string? requestedKey = null,
            CancellationToken ct = default)
        {
            var key = requestedKey ?? "default";
            return Task.FromResult((
                "Server=test;Database=test;",
                new AiDataSourceInfo
                {
                    Key = key,
                    Name = "Testdatabas",
                    HasConnectionString = true
                }));
        }

        public void SetSelected(string key)
        {
        }

        public string? GetSelected() => null;
    }

    private sealed class FakeConversationMemory : IAiConversationMemory
    {
        private readonly Dictionary<string, AiConversationResultContext> _resultContexts = [];

        public List<OpenAiChatMessage> GetHistory(string key) => [];
        public void AppendTurn(string key, string userMessage, string assistantMessage)
        {
        }

        public AiConversationResultContext? GetLastResultContext(string key) =>
            _resultContexts.TryGetValue(key, out var context) ? context : null;

        public void SetLastResultContext(string key, AiConversationResultContext resultContext) =>
            _resultContexts[key] = resultContext;

        public void Clear(string key)
        {
            _resultContexts.Remove(key);
        }
    }

    private sealed class NullInvoiceQuestionService : IAiInvoiceQuestionService
    {
        public Task<AiQueryResponse?> TryAnswerAsync(
            string question,
            string connectionString,
            int? companyCode,
            CancellationToken ct = default) =>
            Task.FromResult<AiQueryResponse?>(null);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "WebApp.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
