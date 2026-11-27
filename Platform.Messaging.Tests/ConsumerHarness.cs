using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Platform.Messaging.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.Messaging.Tests;

internal sealed class ConsumerHarness : IDisposable
{
    internal BrokerDouble Broker { get; } = new();
    internal RabbitMqConsumerOptions Options { get; }
    internal ServiceProvider Services { get; }
    internal RabbitMqDeliveryProcessor Processor { get; }
    internal Func<IntegrationEvent, CancellationToken, Task> Handle { get; set; } = (_, _) => Task.CompletedTask;
    internal List<IntegrationEvent> Handled { get; } = [];
    internal int ScopesCreated { get; private set; }
    internal int ScopesDisposed { get; private set; }

    internal ConsumerHarness(TimeSpan? retryDelay = null, ILogger<RabbitMqDeliveryProcessor>? logger = null)
    {
        Options = new(RabbitMqTopology.NewslettersQueue)
        {
            RetryDelay = retryDelay ?? TimeSpan.Zero,
            RecoveryDelay = TimeSpan.Zero
        };
        Services = new ServiceCollection()
            .AddScoped<IIntegrationEventHandler>(_ => new Handler(this))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Processor = new(Services.GetRequiredService<IServiceScopeFactory>(), Options,
            logger ?? NullLogger<RabbitMqDeliveryProcessor>.Instance);
    }

    internal RabbitMqEventConsumer Worker(IConnection? connection = null) => new(connection ?? Broker.Connection,
        Processor, Options, NullLogger<RabbitMqEventConsumer>.Instance);

    internal static BasicDeliverEventArgs Delivery(BasicProperties? properties = null, byte[]? body = null)
    {
        var value = new AccountCreatedEvent(EventSerializationTests.Snapshot()) { CorrelationId = "request-123" };
        properties ??= new BasicProperties
        {
            MessageId = value.EventId.ToString(), Type = nameof(AccountCreatedEvent),
            ContentType = "application/json", Persistent = true,
            Headers = new Dictionary<string, object?> { ["x-correlation-id"] = value.CorrelationId }
        };
        if (body is null)
        {
            // Keep the broker ID and envelope ID in agreement even with supplied properties.
            value = value with { EventId = Guid.TryParse(properties.MessageId, out var id) ? id : value.EventId };
            body = EventJson.Serialize(value);
        }
        return new("test-consumer", 42, false, RabbitMqTopology.Exchange, "account.created", properties, body);
    }

    public void Dispose() => Services.Dispose();

    private sealed class Handler : IIntegrationEventHandler, IAsyncDisposable
    {
        private readonly ConsumerHarness owner;
        internal Handler(ConsumerHarness owner) { this.owner = owner; owner.ScopesCreated++; }
        public Task HandleAsync(IntegrationEvent value, CancellationToken cancellationToken)
        {
            owner.Handled.Add(value);
            return owner.Handle(value, cancellationToken);
        }
        public ValueTask DisposeAsync() { owner.ScopesDisposed++; return ValueTask.CompletedTask; }
    }
}
