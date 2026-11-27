using Microsoft.Extensions.Logging;
using Platform.Messaging.Events;
using RabbitMQ.Client;
using Xunit;

namespace Platform.Messaging.Tests;

public sealed class ConsumerBusinessLogTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Processing_and_retry_logs_include_the_entity_ids_from_the_validated_message(bool achievement, bool retry)
    {
        var logger = new CaptureLogger();
        using var harness = new ConsumerHarness(logger: logger);
        IntegrationEvent value = achievement
            ? new AchievementUnlockedEvent(1234, 42, DateTimeOffset.UtcNow)
            : new AccountProfileChangedEvent(EventSerializationTests.Snapshot() with { UserId = 1234 });
        var properties = new BasicProperties { Type = value.GetType().Name, MessageId = value.EventId.ToString() };
        if (retry) harness.Handle = (_, _) => throw new TimeoutException("Database unavailable");
        harness.Broker.Confirmation.SetResult();
        await harness.Processor.ProcessAsync(harness.Broker.Channel,
            ConsumerHarness.Delivery(properties, EventJson.Serialize(value)), default);

        Assert.NotEmpty(logger.Entries);
        foreach (var entry in logger.Entries)
        {
            Assert.Equal(1234L, entry.Fields["UserId"]);
            if (achievement) Assert.Equal(42L, entry.Fields["AchievementId"]);
            else Assert.False(entry.Fields.ContainsKey("AchievementId"));
        }
        if (retry) Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Exception is TimeoutException);
    }

    private sealed class CaptureLogger : ILogger<RabbitMqDeliveryProcessor>
    {
        private readonly List<IEnumerable<KeyValuePair<string, object?>>> scopes = [];
        internal List<(LogLevel Level, Dictionary<string, object?> Fields, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            var fields = (IEnumerable<KeyValuePair<string, object?>>)state;
            scopes.Add(fields);
            return new Scope(() => scopes.Remove(fields));
        }
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, scopes.SelectMany(value => value).ToDictionary(value => value.Key, value => value.Value), exception));
        private sealed class Scope(Action dispose) : IDisposable { public void Dispose() => dispose(); }
    }
}
