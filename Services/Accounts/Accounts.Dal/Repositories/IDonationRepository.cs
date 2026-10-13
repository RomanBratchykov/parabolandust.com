using Accounts.Domain.Models;

namespace Accounts.Dal.Repositories;

public interface IDonationRepository : IGenericRepository<Donation>
{
    Task<IReadOnlyList<Donation>> GetForUserAsync(
        long userId,
        CancellationToken cancellationToken = default);
    Task<bool> ChangeStatusAsync(
        long donationId,
        string status,
        long expectedRowVersion,
        CancellationToken cancellationToken = default);
}
