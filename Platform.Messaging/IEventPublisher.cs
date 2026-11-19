using Platform.Messaging.Events;

namespace Platform.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(IntegrationEvent value, CancellationToken cancellationToken = default);
}
