using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.Messaging;

public sealed class RabbitMqEventConsumer : BackgroundService
{
    private readonly IConnection connection;
    private readonly RabbitMqDeliveryProcessor processor;
    private readonly RabbitMqConsumerOptions options;
    private readonly ILogger<RabbitMqEventConsumer> logger;

    public RabbitMqEventConsumer(IConnection connection, RabbitMqDeliveryProcessor processor,
        RabbitMqConsumerOptions options, ILogger<RabbitMqEventConsumer> logger)
    {
        options.Validate();
        this.connection = connection;
        this.processor = processor;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunSessionAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Consumer channel for {Queue} failed; recreating after {Delay}", options.Queue, options.RecoveryDelay);
                await Task.Delay(options.RecoveryDelay, stoppingToken);
            }
        }
    }

    private async Task RunSessionAsync(CancellationToken stoppingToken)
    {
        using var session = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true, consumerDispatchConcurrency: 1), stoppingToken);
        var failed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task OnShutdown(object sender, ShutdownEventArgs args)
        {
            failed.TrySetResult(new IOException($"Broker channel closed: {args.ReplyText}"));
            return Task.CompletedTask;
        }
        channel.ChannelShutdownAsync += OnShutdown;
        try
        {
            await RabbitMqTopology.DeclareAsync(channel, session.Token);
            await channel.BasicQosAsync(0, options.PrefetchCount, global: false, session.Token);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.UnregisteredAsync += (_, _) =>
            {
                // A broker basic.cancel ends this subscription without closing the channel.
                failed.TrySetResult(new IOException($"Broker cancelled the subscription for {options.Queue}."));
                return Task.CompletedTask;
            };
            consumer.ReceivedAsync += async (_, delivery) =>
            {
                try { await processor.ProcessAsync(channel, delivery, session.Token); }
                catch (OperationCanceledException) when (session.IsCancellationRequested) { }
                catch (Exception exception)
                {
                    // ReceivedAsync exceptions alone would only reach RabbitMQ's callback
                    // error event, leaving an unacknowledged delivery stuck on this channel.
                    failed.TrySetResult(exception);
                    session.Cancel();
                }
            };
            await channel.BasicConsumeAsync(options.Queue, autoAck: false, consumer, session.Token);
            logger.LogInformation("Consuming {Queue} with prefetch {PrefetchCount}", options.Queue, options.PrefetchCount);
            var error = await failed.Task.WaitAsync(stoppingToken);
            throw error;
        }
        finally
        {
            session.Cancel();
            channel.ChannelShutdownAsync -= OnShutdown;
        }
    }
}
