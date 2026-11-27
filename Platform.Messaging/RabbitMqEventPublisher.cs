using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Platform.Messaging.Diagnostics;
using Platform.Messaging.Events;
using RabbitMQ.Client;

namespace Platform.Messaging;

public sealed class RabbitMqEventPublisher(IConnection connection, ILogger<RabbitMqEventPublisher> logger) : IEventPublisher
{
    public async Task PublishAsync(IntegrationEvent value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = EventJson.Serialize(value);
        var eventType = value.GetType().Name;
        var routingKey = RabbitMqTopology.RoutingKey(value);
        using var activity = TracePropagation.StartProducerActivity(RabbitMqTopology.Exchange, eventType, value.EventId.ToString());
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = value.EventId.ToString(),
            Type = eventType,
            CorrelationId = value.CorrelationId,
            Headers = new Dictionary<string, object?> { ["x-event-type"] = eventType }
        };
        TracePropagation.Inject(properties.Headers, value.CorrelationId);
        try
        {
            await using var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                cancellationToken);
            // Declare before sending so subscribers retain messages even before their first start.
            await RabbitMqTopology.DeclareAsync(channel, cancellationToken);
            await channel.BasicPublishAsync(RabbitMqTopology.Exchange, routingKey, mandatory: true,
                properties, body, cancellationToken);
            logger.LogInformation("Published {EventType} {EventId} to {Exchange}/{RoutingKey} with CorrelationId {CorrelationId}",
                eventType, value.EventId, RabbitMqTopology.Exchange, routingKey, value.CorrelationId);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw;
        }
    }
}
