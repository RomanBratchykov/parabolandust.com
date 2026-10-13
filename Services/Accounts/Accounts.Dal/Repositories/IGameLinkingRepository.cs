using Accounts.Domain.Models;

namespace Accounts.Dal.Repositories;

public interface IGameLinkingRepository : IGenericRepository<GameLinking>
{
    Task<GameLinking?> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default);
}
