using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Platform.Messaging.Diagnostics;
using Platform.Messaging.Events;
using Platform.ServiceDefaults.Messaging;
using RabbitMQ.Client;
using Xunit;

namespace Platform.Messaging.Tests;

public sealed class MessagingRegistrationTests
{
    [Fact]
    public async Task Publisher_is_shared_and_broker_outage_changes_readiness_without_failing_liveness()
    {
        var broker = new BrokerDouble();
        var builder = Builder(broker);
        using var host = builder.Build();
        using var first = host.Services.CreateScope();
        using var second = host.Services.CreateScope();
        Assert.Same(first.ServiceProvider.GetRequiredService<IEventPublisher>(), second.ServiceProvider.GetRequiredService<IEventPublisher>());
        var checks = host.Services.GetRequiredService<HealthCheckService>();
        Assert.Equal(HealthStatus.Healthy, (await checks.CheckHealthAsync(check => check.Tags.Contains("ready"))).Status);
        broker.IsOpen = false;
        Assert.Equal(HealthStatus.Unhealthy, (await checks.CheckHealthAsync(check => check.Tags.Contains("ready"))).Status);
        Assert.Equal(HealthStatus.Healthy, (await checks.CheckHealthAsync(check => check.Tags.Contains("live"))).Status);
        broker.IsOpen = true;
        Assert.Equal(HealthStatus.Healthy, (await checks.CheckHealthAsync(check => check.Tags.Contains("ready"))).Status);
    }

    [Fact]
    public async Task Consumer_readiness_requires_a_subscription_and_is_cleared_on_broker_cancellation()
    {
        var broker = new BrokerDouble();
        var builder = Builder(broker);
        builder.Services.AddPlatformEventConsumer<Handler>(new(RabbitMqTopology.GameStatsQueue)
        {
            RecoveryDelay = TimeSpan.FromSeconds(30)
        });
        using var host = builder.Build();
        var checks = host.Services.GetRequiredService<HealthCheckService>();
        Assert.Equal(HealthStatus.Unhealthy, (await checks.CheckHealthAsync(check => check.Tags.Contains("ready"))).Status);
        var worker = host.Services.GetRequiredService<RabbitMqEventConsumer>();
        await worker.StartAsync(default);
        try
        {
            var consumer = await broker.Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!worker.IsConsuming) await Task.Delay(10, deadline.Token);
            Assert.Equal(HealthStatus.Healthy, (await checks.CheckHealthAsync(check => check.Tags.Contains("ready"))).Status);
            await consumer.HandleBasicCancelAsync("test-consumer");
            Assert.Equal(HealthStatus.Unhealthy, (await checks.CheckHealthAsync(check => check.Tags.Contains("ready"))).Status);
        }
        finally { await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.False(worker.IsConsuming);
    }

    [Fact]
    public void Registered_tracing_exports_linked_messaging_spans()
    {
        var builder = Builder(new BrokerDouble());
        var captured = new CaptureActivities();
        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
            .SetSampler(new AlwaysOnSampler()).AddProcessor(captured));
        using var host = builder.Build();
        host.Services.GetRequiredService<TracerProvider>();
        var messageId = Guid.NewGuid().ToString();
        using var request = new Activity("incoming-request").Start();
        var headers = new Dictionary<string, object?>();
        ActivitySpanId producerId;
        using (var producer = TracePropagation.StartProducerActivity("account-events", "AccountCreatedEvent", messageId))
        {
            Assert.NotNull(producer);
            producerId = producer.SpanId;
            TracePropagation.Inject(headers, "registration-test");
        }
        using (TracePropagation.StartConsumerActivity(headers, RabbitMqTopology.GameStatsQueue, messageId, "AccountCreatedEvent")) { }
        var spans = captured.Items.Where(value => Equals(value.GetTagItem("messaging.message.id"), messageId)).ToArray();
        Assert.Equal(2, spans.Length);
        var consumer = Assert.Single(spans, value => value.Kind == ActivityKind.Consumer);
        Assert.Equal(request.TraceId, consumer.TraceId);
        Assert.Equal(producerId, consumer.ParentSpanId);
    }

    private static HostApplicationBuilder Builder(BrokerDouble broker)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration["ConnectionStrings:messaging"] = "amqp://guest:guest@localhost:5672";
        builder.AddPlatformMessaging();
        builder.Services.Replace(ServiceDescriptor.Singleton<IConnection>(broker.Connection));
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    private sealed class Handler : IIntegrationEventHandler
    {
        public Task HandleAsync(IntegrationEvent value, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CaptureActivities : BaseProcessor<Activity>
    {
        internal ConcurrentBag<Activity> Items { get; } = [];
        public override void OnEnd(Activity data) => Items.Add(data);
    }
}
