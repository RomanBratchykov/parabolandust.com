using System.Text.Json.Serialization;

namespace Platform.Messaging.Events;

public abstract record IntegrationEvent
{
    [JsonRequired]
    public Guid EventId { get; init; } = Guid.NewGuid();

    [JsonRequired]
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonRequired]
    public int SchemaVersion { get; init; } = 1;

    public string? CorrelationId { get; init; }
}
