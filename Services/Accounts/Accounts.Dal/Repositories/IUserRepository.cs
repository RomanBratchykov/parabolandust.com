using Accounts.Domain.Models;

namespace Accounts.Dal.Repositories;

public interface IUserRepository
{
    Task<AccountUser?> GetByIdAsync(long userId, CancellationToken cancellationToken = default);
    Task<long> AddAsync(AccountUser user, CancellationToken cancellationToken = default);
    Task<bool> UpdateProfileAsync(
        AccountUser user,
        long expectedRowVersion,
        CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(
        long userId,
        long expectedRowVersion,
        CancellationToken cancellationToken = default);
}
