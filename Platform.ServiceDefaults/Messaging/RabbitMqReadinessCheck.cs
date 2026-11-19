using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Messaging;
using RabbitMQ.Client;

namespace Platform.ServiceDefaults.Messaging;

public sealed class RabbitMqReadinessCheck(IConnection connection, RabbitMqEventConsumer? consumer = null) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!connection.IsOpen)
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ connection is unavailable."));
        if (consumer is not null && !consumer.IsConsuming)
            return Task.FromResult(HealthCheckResult.Unhealthy("The event subscription is not active."));
        return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ connection and configured subscription are active."));
    }
}
