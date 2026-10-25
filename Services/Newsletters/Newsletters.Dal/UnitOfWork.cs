using Newsletters.Dal.Data;
using Newsletters.Dal.Repositories;
using Newsletters.Domain.Models;

namespace Newsletters.Dal;

public sealed class UnitOfWork(
    NewslettersDbContext context,
    IGenericRepository<Admin> admins,
    INewsletterRepository newsletters,
    IGenericRepository<NewsletterDeliverySettings> deliverySettings,
    IGenericRepository<UserComment> comments,
    IUserCopyRepository usersCopy,
    ISubscriptionRepository subscriptions) : IUnitOfWork
{
    public IGenericRepository<Admin> Admins { get; } = admins;
    public INewsletterRepository Newsletters { get; } = newsletters;
    public IGenericRepository<NewsletterDeliverySettings> DeliverySettings { get; } = deliverySettings;
    public IGenericRepository<UserComment> Comments { get; } = comments;
    public IUserCopyRepository UsersCopy { get; } = usersCopy;
    public ISubscriptionRepository Subscriptions { get; } = subscriptions;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
