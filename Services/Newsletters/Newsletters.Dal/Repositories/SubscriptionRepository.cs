using Microsoft.EntityFrameworkCore;
using Newsletters.Dal.Data;
using Newsletters.Domain.Models;

namespace Newsletters.Dal.Repositories;

public sealed class SubscriptionRepository(NewslettersDbContext context)
    : GenericRepository<Subscription>(context), ISubscriptionRepository
{
    public Task<Subscription?> GetByUserAndNewsletterAsync(
        long userId, long newsletterId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(subscription => subscription.UserId == userId
            && subscription.NewsletterId == newsletterId, cancellationToken);

    public async Task<IReadOnlyList<UserCopy>> GetActiveSubscribersAsync(
        long newsletterId, DateTimeOffset since, CancellationToken cancellationToken = default)
        => await Set.Where(subscription => subscription.NewsletterId == newsletterId
                && subscription.Status == SubscriptionStatus.Active
                && !subscription.IsDeleted
                && !subscription.User.IsDeleted
                && subscription.ConsentAt >= since)
            .OrderBy(subscription => subscription.UserId)
            .Select(subscription => subscription.User)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
}
