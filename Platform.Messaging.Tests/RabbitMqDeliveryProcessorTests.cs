using System.Text;
using System.Text.Json.Nodes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Platform.Messaging.Tests;

public sealed class RabbitMqDeliveryProcessorTests
{
    [Fact]
    public async Task Successful_processing_acks_only_after_the_scoped_handler_finishes()
    {
        using var harness = new ConsumerHarness();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Handle = (_, _) => completed.Task;
        var processing = harness.Processor.ProcessAsync(harness.Broker.Channel, ConsumerHarness.Delivery(), default);
        Assert.DoesNotContain(harness.Broker.Calls, call => call.Method == "BasicAckAsync");
        completed.SetResult();
        await processing;
        var ack = Assert.Single(harness.Broker.Calls, call => call.Method == "BasicAckAsync");
        Assert.Equal(42UL, ack.Arguments["deliveryTag"]);
        Assert.Equal(false, ack.Arguments["multiple"]);
        Assert.Equal(1, harness.ScopesDisposed);
        await harness.Processor.ProcessAsync(harness.Broker.Channel, ConsumerHarness.Delivery(), default);
        Assert.Equal(2, harness.ScopesCreated);
        Assert.Equal(2, harness.ScopesDisposed);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("type")]
    [InlineData("schema")]
    [InlineData("missing-id")]
    [InlineData("mismatched-id")]
    public async Task Malformed_messages_go_directly_to_dlq_without_invoking_a_handler(string defect)
    {
        using var harness = new ConsumerHarness();
        var delivery = ConsumerHarness.Delivery();
        var properties = new BasicProperties(delivery.BasicProperties);
        var body = delivery.Body.ToArray();
        switch (defect)
        {
            case "json": body = "{broken"u8.ToArray(); break;
            case "type": properties.Type = "UnknownEvent"; break;
            case "schema":
                var json = JsonNode.Parse(body)!;
                json["schemaVersion"] = 2;
                body = Encoding.UTF8.GetBytes(json.ToJsonString());
                break;
            case "missing-id": properties.MessageId = null; break;
            case "mismatched-id": properties.MessageId = Guid.NewGuid().ToString(); break;
        }
        await harness.Processor.ProcessAsync(harness.Broker.Channel, ConsumerHarness.Delivery(properties, body), default);
        Assert.Empty(harness.Handled);
        Assert.Equal(0, harness.ScopesCreated);
        AssertDeadLetter(harness.Broker);
    }

    [Fact]
    public async Task Permanent_business_errors_go_to_dlq_without_retrying()
    {
        using var harness = new ConsumerHarness();
        harness.Handle = (_, _) => throw new PermanentEventException("Invalid business data");
        await harness.Processor.ProcessAsync(harness.Broker.Channel, ConsumerHarness.Delivery(), default);
        Assert.Single(harness.Handled);
        Assert.Equal(1, harness.ScopesDisposed);
        AssertDeadLetter(harness.Broker);
    }

    [Fact]
    public async Task Retry_targets_only_its_queue_and_acks_after_confirming_the_preserved_message()
    {
        using var harness = new ConsumerHarness();
        harness.Handle = (_, _) => throw new TimeoutException("Database unavailable");
        var delivery = ConsumerHarness.Delivery();
        var original = new BasicProperties(delivery.BasicProperties)
        {
            Headers = new Dictionary<string, object?>
            {
                ["x-retry-count"] = 1, ["x-correlation-id"] = "request-123", ["custom"] = "preserved"
            }
        };
        delivery = ConsumerHarness.Delivery(original, delivery.Body.ToArray());
        var processing = harness.Processor.ProcessAsync(harness.Broker.Channel, delivery, default);
        var publish = Assert.Single(harness.Broker.Calls, call => call.Method == "BasicPublishAsync");
        Assert.Equal(string.Empty, publish.Arguments["exchange"]);
        Assert.Equal(RabbitMqTopology.NewslettersQueue, publish.Arguments["routingKey"]);
        Assert.Equal(true, publish.Arguments["mandatory"]);
        var retry = Assert.IsType<BasicProperties>(publish.Arguments["basicProperties"]);
        Assert.Equal(2, retry.Headers!["x-retry-count"]);
        Assert.Equal(1, original.Headers["x-retry-count"]);
        Assert.Equal("preserved", retry.Headers["custom"]);
        Assert.Equal("request-123", retry.Headers["x-correlation-id"]);
        Assert.Equal(original.MessageId, retry.MessageId);
        Assert.Equal(original.Type, retry.Type);
        Assert.Equal(original.ContentType, retry.ContentType);
        Assert.True(retry.Persistent);
        Assert.Equal(delivery.Body.ToArray(), Assert.IsType<ReadOnlyMemory<byte>>(publish.Arguments["body"]).ToArray());
        Assert.False(processing.IsCompleted);
        Assert.DoesNotContain(harness.Broker.Calls, call => call.Method == "BasicAckAsync");
        harness.Broker.Confirmation.SetResult();
        await processing;
        Assert.Equal("BasicAckAsync", harness.Broker.Calls[^1].Method);
        Assert.DoesNotContain(harness.Broker.Calls, call => call.Method == "BasicNackAsync");
    }

    [Fact]
    public async Task Exhausted_retries_go_to_dlq_without_republishing()
    {
        using var harness = new ConsumerHarness();
        harness.Handle = (_, _) => throw new IOException("Unavailable");
        var delivery = ConsumerHarness.Delivery();
        var properties = new BasicProperties(delivery.BasicProperties)
        {
            Headers = new Dictionary<string, object?> { ["x-retry-count"] = 3L }
        };
        await harness.Processor.ProcessAsync(harness.Broker.Channel, ConsumerHarness.Delivery(properties, delivery.Body.ToArray()), default);
        Assert.Single(harness.Handled);
        AssertDeadLetter(harness.Broker);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData("2")]
    [InlineData(true)]
    [InlineData(long.MaxValue)]
    public async Task Invalid_retry_headers_are_permanent_format_errors(object count)
    {
        using var harness = new ConsumerHarness();
        var delivery = ConsumerHarness.Delivery();
        var properties = new BasicProperties(delivery.BasicProperties)
        {
            Headers = new Dictionary<string, object?> { ["x-retry-count"] = count }
        };
        await harness.Processor.ProcessAsync(harness.Broker.Channel, ConsumerHarness.Delivery(properties, delivery.Body.ToArray()), default);
        Assert.Empty(harness.Handled);
        AssertDeadLetter(harness.Broker);
    }

    [Fact]
    public async Task Failed_republish_leaves_original_unacknowledged()
    {
        using var harness = new ConsumerHarness();
        harness.Handle = (_, _) => throw new TimeoutException();
        var error = new IOException("Confirmation failed");
        harness.Broker.Confirmation.SetException(error);
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() =>
            harness.Processor.ProcessAsync(harness.Broker.Channel, ConsumerHarness.Delivery(), default)));
        Assert.DoesNotContain(harness.Broker.Calls, call => call.Method is "BasicAckAsync" or "BasicNackAsync");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_during_handler_or_retry_delay_leaves_the_original_for_redelivery(bool duringDelay)
    {
        using var harness = new ConsumerHarness(TimeSpan.FromSeconds(30));
        using var cancellation = new CancellationTokenSource();
        harness.Handle = (_, token) => duringDelay ? throw new TimeoutException() : Task.Delay(Timeout.Infinite, token);
        var processing = harness.Processor.ProcessAsync(harness.Broker.Channel, ConsumerHarness.Delivery(), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing);
        Assert.Equal(1, harness.ScopesDisposed);
        Assert.Empty(harness.Broker.Calls);
    }

    [Fact]
    public async Task Ack_failure_is_not_misclassified_as_a_handler_failure_and_republished()
    {
        using var harness = new ConsumerHarness();
        harness.Broker.AckFailure = new IOException("Channel closed after processing");
        await Assert.ThrowsAsync<IOException>(() => harness.Processor.ProcessAsync(
            harness.Broker.Channel, ConsumerHarness.Delivery(), default));
        Assert.Single(harness.Handled);
        Assert.DoesNotContain(harness.Broker.Calls, call => call.Method is "BasicPublishAsync" or "BasicNackAsync");
    }

    private static void AssertDeadLetter(BrokerDouble broker)
    {
        var nack = Assert.Single(broker.Calls, call => call.Method == "BasicNackAsync");
        Assert.Equal(42UL, nack.Arguments["deliveryTag"]);
        Assert.Equal(false, nack.Arguments["multiple"]);
        Assert.Equal(false, nack.Arguments["requeue"]);
        Assert.DoesNotContain(broker.Calls, call => call.Method is "BasicPublishAsync" or "BasicAckAsync");
    }
}
