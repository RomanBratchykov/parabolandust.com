using Platform.Messaging.Events;

namespace Platform.Messaging;

// Each consuming service supplies a scoped dispatcher for its projection handlers.
public interface IIntegrationEventHandler
{
    Task HandleAsync(IntegrationEvent value, CancellationToken cancellationToken);
}

// Handlers use this for invalid business data that another delivery cannot repair.
public sealed class PermanentEventException(string message) : Exception(message);
