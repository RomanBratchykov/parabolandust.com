namespace Accounts.Bll;

using Accounts.Bll.Contracts;

public interface IAccountsService
{
    Task<UserResponse?> GetUserAsync(long userId, CancellationToken cancellationToken = default);
    Task<long> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(long userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(long userId, long expectedRowVersion, CancellationToken cancellationToken = default);
    Task<DonationResponse?> GetDonationAsync(long donationId, CancellationToken cancellationToken = default);
    Task<long> CreateDonationAsync(CreateDonationRequest request, CancellationToken cancellationToken = default);
    Task UpdateDonationAsync(long donationId, UpdateDonationRequest request, CancellationToken cancellationToken = default);
    Task DeleteDonationAsync(long donationId, CancellationToken cancellationToken = default);
    Task ChangeDonationStatusAsync(long donationId, ChangeDonationStatusRequest request, CancellationToken cancellationToken = default);
    Task GrantAchievementAsync(GrantAchievementRequest request, CancellationToken cancellationToken = default);
}
