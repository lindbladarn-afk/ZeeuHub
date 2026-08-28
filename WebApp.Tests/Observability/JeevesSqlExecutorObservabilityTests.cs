using Microsoft.Extensions.Logging;
using Repository.Execution;

namespace WebApp.Tests;

// Verifies that Jeeves SQL failures retain exceptions without exposing SQL or credentials.
public sealed class JeevesSqlExecutorObservabilityTests
{
    [Fact]
    public void Query_LogsStructuredConnectionFailureWithoutSensitiveInputs()
    {
        const string connectionString = "invalid-secret-connection-string";
        const string sql = "SELECT 'private-customer-value'";
        var logger = new CapturingLogger<JeevesSqlExecutor>();
        var executor = new JeevesSqlExecutor(logger);

        var exception = Assert.Throws<ArgumentException>(() =>
            executor.Query<int>(
                connectionString,
                sql,
                operationName: "LoadOrders"));

        var entry = Assert.Single(logger.Entries, item => item.Level == LogLevel.Error);
        Assert.Same(exception, entry.Exception);
        Assert.Equal(
            JeevesSqlTelemetry.ConnectionFailedErrorCode,
            entry.Properties["ErrorCode"]?.ToString());
        Assert.Equal("LoadOrders", entry.Properties["Operation"]?.ToString());
        Assert.Equal("Jeeves", entry.Properties["ExternalSystem"]?.ToString());
        Assert.DoesNotContain(connectionString, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sql, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("private-customer-value", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_UsesStandardDotNetLogger()
    {
        var parameterType = Assert.Single(typeof(JeevesSqlExecutor)
                .GetConstructors()
                .Single()
                .GetParameters())
            .ParameterType;

        Assert.Equal(typeof(ILogger<JeevesSqlExecutor>), parameterType);
    }

    [Fact]
    public void Executor_UsesLegacyCompatibleSqlClient()
    {
        var referencedAssemblies = typeof(JeevesSqlExecutor)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.Contains("System.Data.SqlClient", referencedAssemblies);
        Assert.DoesNotContain("Microsoft.Data.SqlClient", referencedAssemblies);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, properties));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);
}
