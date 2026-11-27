namespace Newsletters.Domain.Models;

public sealed class ProcessedEvent
{
    private ProcessedEvent() { }

    public ProcessedEvent(Guid eventId)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("Event ID is required.", nameof(eventId));
        EventId = eventId;
    }

    public Guid EventId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
}
