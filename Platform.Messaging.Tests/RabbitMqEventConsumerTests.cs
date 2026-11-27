using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Platform.Messaging.Tests;

public sealed class RabbitMqEventConsumerTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Worker_uses_bounded_prefetch_manual_ack_and_confirmed_serial_dispatch_then_disposes_on_stop()
    {
        using var harness = new ConsumerHarness();
        using var worker = harness.Worker();
        await worker.StartAsync(default);
        try
        {
            var consumer = await harness.Broker.Subscribed.Task.WaitAsync(Deadline);
            Assert.False(worker.ExecuteTask!.IsCompleted);
            Assert.True(harness.Broker.ChannelOptions!.PublisherConfirmationsEnabled);
            Assert.True(harness.Broker.ChannelOptions.PublisherConfirmationTrackingEnabled);
            Assert.Equal((ushort)1, harness.Broker.ChannelOptions.ConsumerDispatchConcurrency);
            var qos = Assert.Single(harness.Broker.Calls, call => call.Method == "BasicQosAsync");
            Assert.Equal(0U, qos.Arguments["prefetchSize"]);
            Assert.Equal((ushort)10, qos.Arguments["prefetchCount"]);
            Assert.Equal(false, qos.Arguments["global"]);
            var subscribe = Assert.Single(harness.Broker.Calls, call => call.Method == "BasicConsumeAsync");
            Assert.Equal(RabbitMqTopology.NewslettersQueue, subscribe.Arguments["queue"]);
            Assert.Equal(false, subscribe.Arguments["autoAck"]);
            Assert.True(harness.Broker.Calls.FindIndex(call => call.Method == "BasicQosAsync") <
                harness.Broker.Calls.FindIndex(call => call.Method == "BasicConsumeAsync"));
            await DeliverAsync(consumer);
            Assert.Single(harness.Handled);
            Assert.Single(harness.Broker.Calls, call => call.Method == "BasicAckAsync");
        }
        finally { await worker.StopAsync(default).WaitAsync(Deadline); }
        Assert.True(harness.Broker.ChannelDisposed.Task.IsCompletedSuccessfully);
        Assert.Null(harness.Broker.Shutdown);
    }

    [Fact]
    public async Task Failed_retry_publish_closes_the_session_without_ack_and_resubscribes_on_a_new_channel()
    {
        using var harness = new ConsumerHarness();
        var next = new BrokerDouble();
        var channels = 0;
        var connection = RecordingProxy<IConnection>.Create((method, _) => method.Name == "CreateChannelAsync"
            ? Task.FromResult(Interlocked.Increment(ref channels) == 1 ? harness.Broker.Channel : next.Channel)
            : throw new NotSupportedException(method.Name));
        harness.Handle = (_, _) => throw new TimeoutException("Database unavailable");
        harness.Broker.Confirmation.SetException(new IOException("Broker unavailable"));
        using var worker = harness.Worker(connection);
        await worker.StartAsync(default);
        try
        {
            await DeliverAsync(await harness.Broker.Subscribed.Task.WaitAsync(Deadline));
            var recovered = await next.Subscribed.Task.WaitAsync(Deadline);
            Assert.True(harness.Broker.ChannelDisposed.Task.IsCompletedSuccessfully);
            Assert.DoesNotContain(harness.Broker.Calls, call => call.Method is "BasicAckAsync" or "BasicNackAsync");
            harness.Handle = (_, _) => Task.CompletedTask;
            await DeliverAsync(recovered);
            Assert.Single(next.Calls, call => call.Method == "BasicAckAsync");
        }
        finally { await worker.StopAsync(default).WaitAsync(Deadline); }
    }

    [Fact]
    public async Task Channel_shutdown_recreates_subscription_instead_of_leaving_an_idle_worker()
    {
        using var harness = new ConsumerHarness();
        var next = new BrokerDouble();
        var channels = 0;
        var connection = RecordingProxy<IConnection>.Create((_, _) =>
            Task.FromResult(Interlocked.Increment(ref channels) == 1 ? harness.Broker.Channel : next.Channel));
        using var worker = harness.Worker(connection);
        await worker.StartAsync(default);
        try
        {
            await harness.Broker.Subscribed.Task.WaitAsync(Deadline);
            await harness.Broker.Shutdown!(harness.Broker.Channel,
                new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "Connection closed"));
            await next.Subscribed.Task.WaitAsync(Deadline);
            Assert.True(harness.Broker.ChannelDisposed.Task.IsCompletedSuccessfully);
        }
        finally { await worker.StopAsync(default).WaitAsync(Deadline); }
    }

    [Fact]
    public async Task Broker_consumer_cancellation_resubscribes_even_when_the_channel_stays_open()
    {
        using var harness = new ConsumerHarness();
        var next = new BrokerDouble();
        var channels = 0;
        var connection = RecordingProxy<IConnection>.Create((_, _) =>
            Task.FromResult(Interlocked.Increment(ref channels) == 1 ? harness.Broker.Channel : next.Channel));
        using var worker = harness.Worker(connection);
        await worker.StartAsync(default);
        try
        {
            var consumer = await harness.Broker.Subscribed.Task.WaitAsync(Deadline);
            await consumer.HandleBasicCancelAsync("test-consumer");
            await next.Subscribed.Task.WaitAsync(Deadline);
            Assert.True(harness.Broker.ChannelDisposed.Task.IsCompletedSuccessfully);
        }
        finally { await worker.StopAsync(default).WaitAsync(Deadline); }
    }

    [Fact]
    public async Task Stopping_cancels_an_inflight_handler_and_leaves_its_delivery_unacknowledged()
    {
        using var harness = new ConsumerHarness();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Handle = (_, token) => { entered.SetResult(); return Task.Delay(Timeout.Infinite, token); };
        using var worker = harness.Worker();
        await worker.StartAsync(default);
        var consumer = await harness.Broker.Subscribed.Task.WaitAsync(Deadline);
        var delivery = DeliverAsync(consumer);
        try { await entered.Task.WaitAsync(Deadline); }
        finally { await worker.StopAsync(default).WaitAsync(Deadline); }
        await delivery.WaitAsync(Deadline);
        Assert.Equal(1, harness.ScopesDisposed);
        Assert.DoesNotContain(harness.Broker.Calls, call => call.Method is "BasicAckAsync" or "BasicNackAsync");
        Assert.True(harness.Broker.ChannelDisposed.Task.IsCompletedSuccessfully);
    }

    private static Task DeliverAsync(IAsyncBasicConsumer consumer)
    {
        var delivery = ConsumerHarness.Delivery();
        return consumer.HandleBasicDeliverAsync(delivery.ConsumerTag, delivery.DeliveryTag, delivery.Redelivered,
            delivery.Exchange, delivery.RoutingKey, delivery.BasicProperties, delivery.Body);
    }
}
