using System.Diagnostics;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Platform.Aggregator.Clients;

public sealed class GrpcCall(IOptions<DownstreamOptions> options, ILogger<GrpcCall> logger)
{
    public async Task<T?> InvokeAsync<T>(string service, string method,
        Func<DateTime, CancellationToken, AsyncUnaryCall<T>> start,
        CancellationToken cancellationToken) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var seconds = options.Value.TimeoutSeconds;
        if (seconds is < 1 or > 30)
            throw new InvalidOperationException("Downstream:TimeoutSeconds must be between 1 and 30 seconds.");
        var started = Stopwatch.GetTimestamp();
        var status = StatusCode.Unknown;
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["rpc.system"] = "grpc", ["rpc.service"] = service, ["rpc.method"] = method
        });
        try
        {
            using var call = start(DateTime.UtcNow.AddSeconds(seconds), cancellationToken);
            var response = await call.ResponseAsync;
            cancellationToken.ThrowIfCancellationRequested();
            status = StatusCode.OK;
            return response;
        }
        catch (RpcException exception)
        {
            status = exception.StatusCode;
            cancellationToken.ThrowIfCancellationRequested();
            if (status == StatusCode.NotFound)
                return null;
            if (status == StatusCode.DeadlineExceeded)
                throw new TimeoutException($"{service} exceeded its downstream budget.", exception);
            throw new HttpRequestException($"{service} RPC failed with {status}.", exception);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status = StatusCode.Cancelled;
            throw;
        }
        finally
        {
            logger.LogInformation("gRPC {Service}/{Method} finished with {RpcStatus} in {ElapsedMilliseconds} ms",
                service, method, status, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
