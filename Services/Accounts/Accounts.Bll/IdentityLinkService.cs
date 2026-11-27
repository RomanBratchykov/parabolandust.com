using Accounts.Dal;
using Accounts.Dal.Repositories;
using Accounts.Bll.Events;
using Platform.Messaging;
using Platform.Messaging.Events;

namespace Accounts.Bll;

public sealed class IdentityLinkService(IdentityLinkRepository links, IUnitOfWork unitOfWork,
    IEventPublisher publisher, IEventCorrelation correlation)
{
    public Task<long?> ResolveAsync(string issuer, string subject, CancellationToken cancellationToken) =>
        links.ResolveAsync(issuer, subject, cancellationToken);

    public async Task<long?> ProvisionAsync(string issuer, string subject, string userName, string email,
        CancellationToken cancellationToken)
    {
        var id = await links.ProvisionAsync(issuer, subject, userName, email, cancellationToken);
        if (id is null) return null;
        var user = await unitOfWork.Users.GetByIdAsync(id.Value, cancellationToken);
        if (user is null) return null;

        var snapshot = new AccountSnapshot(user.UserId, user.UserName, user.UserEmail, user.UserWhoAmI,
            user.CreatedAt, user.UpdatedAt ?? user.CreatedAt, user.RowVersion, user.IsDeleted);
        try
        {
            // Retries republish the current snapshot when a prior post-commit publish failed.
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await publisher.PublishAsync(new AccountCreatedEvent(snapshot)
            {
                OccurredAt = snapshot.SourceUpdatedAt,
                CorrelationId = correlation.Current
            }, budget.Token);
        }
        catch (Exception exception)
        {
            throw new AccountEventPublicationException(exception);
        }
        return id;
    }
}
