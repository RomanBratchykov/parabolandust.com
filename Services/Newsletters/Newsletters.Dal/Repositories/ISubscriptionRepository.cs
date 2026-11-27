using Newsletters.Domain.Models;

namespace Newsletters.Dal.Repositories;

public interface ISubscriptionRepository : IGenericRepository<Subscription>
{
    Task<Subscription?> GetByUserAndNewsletterAsync(
        long userId, long newsletterId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserCopy>> GetActiveSubscribersAsync(
        long newsletterId, DateTimeOffset since, CancellationToken cancellationToken = default);
}
