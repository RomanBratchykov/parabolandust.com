using Platform.Messaging.Events;

namespace GameStats.Application.Events;

public interface IAccountEventProjection
{
    Task ApplyAsync(IntegrationEvent value, CancellationToken cancellationToken);
}
