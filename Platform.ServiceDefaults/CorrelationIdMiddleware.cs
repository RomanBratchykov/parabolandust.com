using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Platform.ServiceDefaults;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var values = context.Request.Headers[HeaderName];
        var correlationId = values.Count == 1 ? values[0] : null;
        var generated = string.IsNullOrWhiteSpace(correlationId);
        if (generated)
        {
            correlationId = Guid.NewGuid().ToString();
        }

        // Both YARP and outgoing HTTP handlers read the normalized request header.
        context.Request.Headers[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        // Exception handling can clear headers before writing ProblemDetails.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var logContext = LogContext.PushProperty("CorrelationId", correlationId);
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId!
        });

        if (generated)
        {
            logger.LogInformation("Generated correlation id {CorrelationId}", correlationId);
        }

        logger.LogInformation("Request started {Method} {Path}",
            context.Request.Method, context.Request.Path);

        var statusCode = StatusCodes.Status500InternalServerError;
        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            logger.LogInformation("Request finished with {StatusCode}", statusCode);
        }
    }
}
