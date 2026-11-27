namespace Newsletters.Dal;

using Newsletters.Domain.Models;

public interface IUnitOfWork
{
    Repositories.IGenericRepository<Admin> Admins { get; }
    Repositories.INewsletterRepository Newsletters { get; }
    Repositories.IGenericRepository<NewsletterDeliverySettings> DeliverySettings { get; }
    Repositories.IGenericRepository<UserComment> Comments { get; }
    Repositories.IUserCopyRepository UsersCopy { get; }
    Repositories.ISubscriptionRepository Subscriptions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
