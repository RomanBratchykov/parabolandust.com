using System.Diagnostics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Platform.ApiGateway;

public sealed class GatewayTrafficMiddleware(
    RequestDelegate next, ILogger<GatewayTrafficMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var started = Stopwatch.GetTimestamp();
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        Exception? failure = null;

        logger.LogInformation("Gateway received {Method} {Path} from {ClientIp} at {StartedAt}",
            context.Request.Method, context.Request.Path, clientIp, startedAt);
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            // YARP records ordinary forwarding failures as a feature, without throwing.
            var proxy = context.Features.Get<IReverseProxyFeature>();
            var error = context.Features.Get<IForwarderErrorFeature>();
            var exception = failure ?? error?.Exception;
            var statusCode = failure is null ? context.Response.StatusCode : 500;
            logger.Log(exception is null && error is null ? LogLevel.Information : LogLevel.Error,
                exception,
                "Gateway completed {Method} {Path} from {ClientIp} at {StartedAt} " +
                "via {RouteId} to {ClusterId} with {StatusCode} in {ElapsedMilliseconds} ms; " +
                "response bytes {ResponseBytes}, forwarding error {ForwarderError}",
                context.Request.Method, context.Request.Path, clientIp, startedAt,
                proxy?.Route.Config.RouteId, proxy?.Cluster.Config.ClusterId, statusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                // Streaming responses may have no declared length; do not buffer them.
                context.Response.ContentLength, error?.Error.ToString() ?? "None");
        }
    }
}
