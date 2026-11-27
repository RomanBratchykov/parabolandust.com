using Grpc.Core;
using Grpc.Health.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Aggregator.Clients;

public sealed class GrpcDependencyHealthCheck(
    Health.HealthClient client, string service, ILogger<GrpcDependencyHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var response = await client.CheckAsync(new HealthCheckRequest(),
                deadline: DateTime.UtcNow.AddSeconds(2), cancellationToken: cancellationToken);
            return response.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy($"{service} is serving.")
                : HealthCheckResult.Unhealthy($"{service} is not serving.");
        }
        catch (RpcException exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning(exception, "gRPC health check failed for {Service}", service);
            return HealthCheckResult.Unhealthy($"{service} is unavailable.", exception);
        }
    }
}
