using Accounts.Domain.Models;

namespace Accounts.Dal.Repositories;

public interface IUserRepository
{
    Task<AccountUser?> GetByIdAsync(long userId, CancellationToken cancellationToken = default);
    Task<AccountUser?> GetIncludingDeletedAsync(long userId, CancellationToken cancellationToken = default);
    Task<bool> RotateIdentityAsync(long userId, string identity, long expectedRowVersion, CancellationToken cancellationToken = default);
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
