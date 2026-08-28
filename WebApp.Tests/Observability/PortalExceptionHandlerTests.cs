using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApp.Observability;

namespace WebApp.Tests;

// Verifies that unhandled failures are logged and returned without internal details.
public sealed class PortalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ReturnsSafeProblemDetailsAndLogsException()
    {
        var logger = new CapturingLogger<PortalExceptionHandler>();
        var handler = new PortalExceptionHandler(logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";
        context.Response.Body = new MemoryStream();
        context.Items[PortalObservability.SupportIdItemKey] = "4f892abc";
        context.Items[PortalObservability.CorrelationIdItemKey] = "correlation-42";
        var exception = new InvalidOperationException("internal database detail");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Same(exception, logger.Exception);
        Assert.Equal("4f892abc", context.Response.Headers[PortalObservability.SupportHeaderName]);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var json = document.RootElement.ToString();
        Assert.Contains("4f892abc", json, StringComparison.Ordinal);
        Assert.DoesNotContain("internal database detail", json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), json, StringComparison.Ordinal);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public Exception? Exception { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Exception = exception;
        }
    }
}
