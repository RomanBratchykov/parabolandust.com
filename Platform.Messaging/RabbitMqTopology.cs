using System.Text.Json;
using Platform.Messaging.Events;
using RabbitMQ.Client;

namespace Platform.Messaging;

public static class RabbitMqTopology
{
    public const string Exchange = "account-events";
    public const string DeadLetterExchange = "account-events.dlx";
    public const string NewslettersQueue = "newsletters.account-sync";
    public const string GameStatsQueue = "gamestats.account-sync";

    public static string RoutingKey(IntegrationEvent value) => value switch
    {
        AccountCreatedEvent => "account.created",
        AccountProfileChangedEvent => "account.profile_changed",
        AccountDeletedEvent => "account.deleted",
        AccountIdentityRotatedEvent => "account.identity_rotated",
        AchievementUnlockedEvent => "achievement.unlocked",
        _ => throw new JsonException("Unsupported event type.")
    };

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true,
            cancellationToken: cancellationToken);
        // Direct routing keeps a rejected delivery in only the failing consumer's DLQ.
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true,
            cancellationToken: cancellationToken);
        await DeclareSubscriptionAsync(channel, NewslettersQueue, cancellationToken);
        await DeclareSubscriptionAsync(channel, GameStatsQueue, cancellationToken);
        await channel.QueueBindAsync(GameStatsQueue, Exchange, "achievement.*", cancellationToken: cancellationToken);
    }

    private static async Task DeclareSubscriptionAsync(IChannel channel, string queue, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(queue + ".dlq", durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue + ".dlq", DeadLetterExchange, queue, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = queue
            }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue, Exchange, "account.*", cancellationToken: cancellationToken);
    }
}
