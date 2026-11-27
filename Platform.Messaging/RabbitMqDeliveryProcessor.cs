using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Messaging.Diagnostics;
using Platform.Messaging.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.Messaging;

public sealed class RabbitMqDeliveryProcessor
{
    private readonly IServiceScopeFactory scopes;
    private readonly RabbitMqConsumerOptions options;
    private readonly ILogger<RabbitMqDeliveryProcessor> logger;

    public RabbitMqDeliveryProcessor(IServiceScopeFactory scopes, RabbitMqConsumerOptions options,
        ILogger<RabbitMqDeliveryProcessor> logger)
    {
        options.Validate();
        this.scopes = scopes;
        this.options = options;
        this.logger = logger;
    }

    public async Task ProcessAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var properties = delivery.BasicProperties;
        using var activity = TracePropagation.StartConsumerActivity(properties.Headers, options.Queue, properties.MessageId, properties.Type);
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Queue"] = options.Queue, ["EventType"] = properties.Type, ["EventId"] = properties.MessageId,
            ["CorrelationId"] = TracePropagation.ReadCorrelationId(properties.Headers)
        });
        IntegrationEvent value;
        int retryCount;
        try
        {
            value = EventJson.Deserialize(properties.Type ?? string.Empty, delivery.Body.Span);
            if (!Guid.TryParse(properties.MessageId, out var messageId) || messageId != value.EventId)
                throw new JsonException("MessageId must match the event envelope's EventId.");
            retryCount = ReadRetryCount(properties.Headers);
        }
        catch (JsonException exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            await RejectAsync(channel, delivery, exception, cancellationToken);
            return;
        }

        var userId = value switch
        {
            AccountCreatedEvent created => created.Account.UserId,
            AccountProfileChangedEvent changed => changed.Account.UserId,
            AccountDeletedEvent deleted => deleted.Account.UserId,
            AccountIdentityRotatedEvent rotated => rotated.Account.UserId,
            AchievementUnlockedEvent unlocked => unlocked.UserId,
            _ => throw new InvalidOperationException("Unsupported validated integration event.")
        };
        var identifiers = new Dictionary<string, object> { ["UserId"] = userId };
        if (value is AchievementUnlockedEvent achievement) identifiers["AchievementId"] = achievement.AchievementId;
        using var businessScope = logger.BeginScope(identifiers);
        try
        {
            logger.LogInformation("Received {EventType} {EventId}", properties.Type, value.EventId);
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler>().HandleAsync(value, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (PermanentEventException exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            await RejectAsync(channel, delivery, exception, cancellationToken);
            return;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            await RetryAsync(channel, delivery, retryCount, exception, cancellationToken);
            return;
        }

        // Transport errors here must escape to the worker, not start a business retry.
        cancellationToken.ThrowIfCancellationRequested();
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        logger.LogInformation("Acknowledged {EventId} after successful processing", value.EventId);
    }

    private async Task RetryAsync(IChannel channel, BasicDeliverEventArgs delivery, int count,
        Exception exception, CancellationToken cancellationToken)
    {
        if (count >= options.MaxRetries)
        {
            await RejectAsync(channel, delivery, exception, cancellationToken);
            return;
        }
        logger.LogWarning(exception, "Retrying {MessageId}, attempt {Attempt}/{MaxRetries}",
            delivery.BasicProperties.MessageId, count + 1, options.MaxRetries);
        await Task.Delay(options.RetryDelay, cancellationToken);
        var properties = new BasicProperties(delivery.BasicProperties)
        {
            Persistent = true,
            Headers = new Dictionary<string, object?>(delivery.BasicProperties.Headers ?? new Dictionary<string, object?>())
            {
                ["x-retry-count"] = count + 1
            }
        };
        // The default exchange routes to exactly this queue, avoiding a fan-out retry.
        // The worker creates a channel with tracked confirms; failed publishes escape
        // without acknowledging the original and cause the channel to be recreated.
        await channel.BasicPublishAsync(string.Empty, options.Queue, mandatory: true, properties, delivery.Body, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        logger.LogInformation("Confirmed retry and acknowledged original {MessageId}", properties.MessageId);
    }

    private async Task RejectAsync(IChannel channel, BasicDeliverEventArgs delivery, Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Rejecting {MessageId} to the dead-letter queue: invalid message or exhausted retries",
            delivery.BasicProperties.MessageId);
        await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
    }

    private static int ReadRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-retry-count", out var value)) return 0;
        return value switch
        {
            byte count => count,
            sbyte count when count >= 0 => count,
            short count when count >= 0 => count,
            int count when count >= 0 => count,
            long count when count is >= 0 and <= int.MaxValue => (int)count,
            _ => throw new JsonException("x-retry-count must be a nonnegative AMQP integer.")
        };
    }
}
