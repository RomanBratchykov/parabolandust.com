using System.Text.Json;
using Newsletters.Dal.Events;
using Platform.Caching;
using Platform.Messaging;
using Platform.Messaging.Events;

namespace Newsletters.Bll.Events;

public sealed class AccountEventHandler(IAccountEventProjection projection,
    CacheInvalidator invalidator) : IIntegrationEventHandler
{
    public async Task HandleAsync(IntegrationEvent value, CancellationToken cancellationToken)
    {
        var account = value switch
        {
            AccountCreatedEvent created => created.Account,
            AccountProfileChangedEvent changed => changed.Account,
            AccountDeletedEvent deleted => deleted.Account,
            AccountIdentityRotatedEvent rotated => rotated.Account,
            _ => throw new PermanentEventException("Unsupported Newsletters account event.")
        };
        try
        {
            EventJson.Serialize(value);
        }
        catch (JsonException exception)
        {
            throw new PermanentEventException(exception.Message);
        }
        if (account.UserName.Length > 50 || account.UserEmail.Length > 254)
            throw new PermanentEventException("Account snapshot exceeds Newsletters field limits.");

        await projection.ApplyAsync(value.EventId, account, cancellationToken);
        // Redelivery retries invalidation if the process stopped after the database commit.
        await invalidator.InvalidateAsync(["newsletters"], "account event projected");
    }
}
