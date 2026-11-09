using GameStats.Domain.Common;
using GameStats.Domain.Exceptions;
using MongoDB.Bson;

namespace GameStats.Domain.Entities;

public class GameEvent : BaseEntity
{
    public string EventId { get; private set; } = "";       
    public string EventType { get; private set; } = "";
    public long UserId { get; private set; }
    public string? RunId { get; private set; }              
    public DateTime OccurredAt { get; private set; }
    public BsonDocument Payload { get; private set; } = new();   

    private GameEvent() { }

    public GameEvent(string eventId, string eventType, long userId,
                     string? runId, BsonDocument payload)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            throw new DomainException("EventId is required");
        if (string.IsNullOrWhiteSpace(eventType))
            throw new DomainException("EventType is required");
        if (userId <= 0)
            throw new DomainException("UserId must be positive");

        EventId = eventId;
        EventType = eventType;
        UserId = userId;
        RunId = runId;
        Payload = payload ?? new BsonDocument();
        OccurredAt = DateTime.UtcNow;
    }
}