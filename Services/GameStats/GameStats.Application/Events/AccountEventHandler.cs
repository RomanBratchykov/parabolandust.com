using System.Text.Json;
using Platform.Caching;
using Platform.Messaging;
using Platform.Messaging.Events;

namespace GameStats.Application.Events;

public sealed class AccountEventHandler(IAccountEventProjection projection,
    CacheInvalidator invalidator) : IIntegrationEventHandler
{
    public async Task HandleAsync(IntegrationEvent value, CancellationToken cancellationToken)
    {
        try
        {
            EventJson.Serialize(value);
        }
        catch (JsonException exception)
        {
            throw new PermanentEventException(exception.Message);
        }
        await projection.ApplyAsync(value, cancellationToken);
        // Duplicate delivery also repairs invalidation missed after the original commit.
        await invalidator.InvalidateAsync(["gamestats"], "account event projected");
    }
}
