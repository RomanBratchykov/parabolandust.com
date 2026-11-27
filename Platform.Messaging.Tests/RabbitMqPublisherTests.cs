using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Messaging.Events;
using RabbitMQ.Client;
using Xunit;

namespace Platform.Messaging.Tests;

public sealed class RabbitMqPublisherTests
{
    [Fact]
    public async Task Publishing_waits_for_confirmation_and_preserves_persistent_event_properties()
    {
        var broker = new BrokerDouble();
        var publisher = new RabbitMqEventPublisher(broker.Connection, NullLogger<RabbitMqEventPublisher>.Instance);
        var value = new AccountCreatedEvent(EventSerializationTests.Snapshot()) { CorrelationId = "request-123" };
        using var request = new Activity("request").Start();
        using var cancellation = new CancellationTokenSource();
        var publish = publisher.PublishAsync(value, cancellation.Token);
        Assert.False(publish.IsCompleted);
        Assert.True(broker.ChannelOptions!.PublisherConfirmationsEnabled);
        Assert.True(broker.ChannelOptions.PublisherConfirmationTrackingEnabled);
        Assert.Equal(cancellation.Token, broker.ChannelToken);
        var call = Assert.Single(broker.Calls, call => call.Method == "BasicPublishAsync");
        Assert.Equal("account-events", call.Arguments["exchange"]);
        Assert.Equal("account.created", call.Arguments["routingKey"]);
        Assert.Equal(true, call.Arguments["mandatory"]);
        var properties = Assert.IsType<BasicProperties>(call.Arguments["basicProperties"]);
        Assert.True(properties.Persistent);
        Assert.Equal("application/json", properties.ContentType);
        Assert.Equal(value.EventId.ToString(), properties.MessageId);
        Assert.Equal(nameof(AccountCreatedEvent), properties.Type);
        Assert.Equal(properties.Type, properties.Headers!["x-event-type"]);
        Assert.Equal(value.CorrelationId, properties.Headers["x-correlation-id"]);
        Assert.True(properties.Headers.ContainsKey("traceparent"));
        var body = Assert.IsType<ReadOnlyMemory<byte>>(call.Arguments["body"]);
        Assert.Equal(value, EventJson.Deserialize(properties.Type!, body.Span));
        Assert.DoesNotContain(broker.Calls, call => call.Method == "DisposeAsync");
        broker.Confirmation.SetResult();
        await publish;
        Assert.Equal("DisposeAsync", broker.Calls[^1].Method);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Broker_failure_or_cancellation_is_propagated_and_channel_is_disposed(bool cancel)
    {
        var broker = new BrokerDouble();
        var publisher = new RabbitMqEventPublisher(broker.Connection, NullLogger<RabbitMqEventPublisher>.Instance);
        using var cancellation = new CancellationTokenSource();
        var publish = publisher.PublishAsync(new AccountCreatedEvent(EventSerializationTests.Snapshot()), cancellation.Token);
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publish);
        }
        else
        {
            var error = new IOException("Broker rejected or returned publication");
            broker.Confirmation.SetException(error);
            Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => publish));
        }
        Assert.Equal("DisposeAsync", broker.Calls[^1].Method);
    }

    [Fact]
    public async Task Failed_declaration_never_publishes_and_invalid_event_never_opens_a_channel()
    {
        var broker = new BrokerDouble { DeclarationFailure = new IOException("Unavailable") };
        var publisher = new RabbitMqEventPublisher(broker.Connection, NullLogger<RabbitMqEventPublisher>.Instance);
        await Assert.ThrowsAsync<JsonException>(() => publisher.PublishAsync(
            new AccountCreatedEvent(EventSerializationTests.Snapshot() with { UserId = 0 })));
        Assert.Null(broker.ChannelOptions);
        await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync(new AccountCreatedEvent(EventSerializationTests.Snapshot())));
        Assert.DoesNotContain(broker.Calls, call => call.Method == "BasicPublishAsync");
        Assert.Equal("DisposeAsync", broker.Calls[^1].Method);
    }

    [Fact]
    public void Each_contract_has_a_stable_routing_key()
    {
        var account = EventSerializationTests.Snapshot();
        Assert.Equal("account.created", RabbitMqTopology.RoutingKey(new AccountCreatedEvent(account)));
        Assert.Equal("account.profile_changed", RabbitMqTopology.RoutingKey(new AccountProfileChangedEvent(account)));
        Assert.Equal("account.deleted", RabbitMqTopology.RoutingKey(new AccountDeletedEvent(account with { IsDeleted = true })));
        Assert.Equal("account.identity_rotated", RabbitMqTopology.RoutingKey(new AccountIdentityRotatedEvent(account)));
        Assert.Equal("achievement.unlocked", RabbitMqTopology.RoutingKey(new AchievementUnlockedEvent(1, 1, DateTimeOffset.UtcNow)));
    }
}
