using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Platform.ServiceDefaults.Grpc;

public sealed class GrpcLoggingInterceptor(ILogger<GrpcLoggingInterceptor> logger) : Interceptor
{
    private static readonly ActivitySource Activities = new("Platform.Grpc");

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
        ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var started = Stopwatch.GetTimestamp();
        using var logContext = LogContext.PushProperty("Transport", "grpc");
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["Transport"] = "grpc" });
        using var activity = Activities.StartActivity(context.Method, ActivityKind.Internal);
        var method = context.Method.Trim('/').Split('/');
        activity?.SetTag("rpc.system", "grpc");
        activity?.SetTag("rpc.service", method[0]);
        activity?.SetTag("rpc.method", method.Length > 1 ? method[1] : context.Method);
        var status = StatusCode.OK;
        Exception? failure = null;
        try
        {
            return await continuation(request, context);
        }
        catch (Exception exception)
        {
            failure = exception;
            var error = exception switch
            {
                RpcException rpc => rpc,
                OperationCanceledException when context.CancellationToken.IsCancellationRequested =>
                    new RpcException(new Status(StatusCode.Cancelled, "Request canceled.")),
                ArgumentException => new RpcException(new Status(StatusCode.InvalidArgument, "Invalid request.")),
                KeyNotFoundException => new RpcException(new Status(StatusCode.NotFound, "Data not found.")),
                _ => new RpcException(new Status(StatusCode.Internal, "An unexpected server error occurred."))
            };
            status = error.StatusCode;
            activity?.SetStatus(ActivityStatusCode.Error, status.ToString());
            throw error;
        }
        finally
        {
            if (failure is null) status = context.Status.StatusCode;
            activity?.SetTag("rpc.grpc.status_code", (int)status);
            logger.Log(failure is null ? LogLevel.Information : LogLevel.Error, failure,
                "gRPC {RpcMethod} peer {Peer} finished {RpcStatus} in {ElapsedMilliseconds} ms",
                context.Method, context.Peer, status, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
