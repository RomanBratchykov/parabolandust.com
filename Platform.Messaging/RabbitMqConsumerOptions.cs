namespace Platform.Messaging;

public sealed record RabbitMqConsumerOptions(string Queue)
{
    public ushort PrefetchCount { get; init; } = 10;
    public int MaxRetries { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan RecoveryDelay { get; init; } = TimeSpan.FromSeconds(5);

    internal void Validate()
    {
        if (Queue is not (RabbitMqTopology.NewslettersQueue or RabbitMqTopology.GameStatsQueue))
            throw new ArgumentException("The consumer requires a declared subscription queue.", nameof(Queue));
        if (PrefetchCount == 0 || MaxRetries < 0 || RetryDelay < TimeSpan.Zero || RecoveryDelay < TimeSpan.Zero)
            throw new ArgumentException("Prefetch must be positive; retry counts and delays cannot be negative.");
    }
}
