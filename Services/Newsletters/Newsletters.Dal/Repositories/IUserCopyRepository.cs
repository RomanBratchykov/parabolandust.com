using Newsletters.Domain.Models;

namespace Newsletters.Dal.Repositories;

public interface IUserCopyRepository : IGenericRepository<UserCopy>
{
    Task<UserCopy?> GetWithSubscriptionsAsync(long id, CancellationToken cancellationToken = default);
}
