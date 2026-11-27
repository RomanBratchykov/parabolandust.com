using Platform.Messaging.Events;

namespace Newsletters.Dal.Events;

public interface IAccountEventProjection
{
    Task ApplyAsync(Guid eventId, AccountSnapshot account, CancellationToken cancellationToken);
}
