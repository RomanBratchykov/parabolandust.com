using System.Globalization;
using Accounts.Bll.Contracts;
using Platform.Caching;

namespace Accounts.Bll.Caching;

public sealed class CachedAccountsService(IAccountsService inner, ICacheAside cache,
    CacheInvalidator invalidator) : IAccountsService
{
    public Task<UserResponse?> GetUserAsync(long userId, CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync("accounts", "user", userId.ToString(CultureInfo.InvariantCulture),
            CachePolicy.Accounts, token => inner.GetUserAsync(userId, token), cancellationToken);

    public async Task<long> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var userId = await inner.CreateUserAsync(request, cancellationToken);
        await invalidator.InvalidateAsync(["accounts"], "user created");
        return userId;
    }

    public async Task UpdateUserAsync(long userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        await inner.UpdateUserAsync(userId, request, cancellationToken);
        await invalidator.InvalidateAsync(["accounts"], "user updated");
    }

    public async Task DeleteUserAsync(long userId, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await inner.DeleteUserAsync(userId, expectedRowVersion, cancellationToken);
        await invalidator.InvalidateAsync(["accounts"], "user deleted");
    }

    public Task<DonationResponse?> GetDonationAsync(long donationId, CancellationToken cancellationToken = default) =>
        inner.GetDonationAsync(donationId, cancellationToken);

    public Task<long> CreateDonationAsync(CreateDonationRequest request, CancellationToken cancellationToken = default) =>
        inner.CreateDonationAsync(request, cancellationToken);

    public Task UpdateDonationAsync(long donationId, UpdateDonationRequest request, CancellationToken cancellationToken = default) =>
        inner.UpdateDonationAsync(donationId, request, cancellationToken);

    public Task DeleteDonationAsync(long donationId, CancellationToken cancellationToken = default) =>
        inner.DeleteDonationAsync(donationId, cancellationToken);

    public Task ChangeDonationStatusAsync(long donationId, ChangeDonationStatusRequest request,
        CancellationToken cancellationToken = default) =>
        inner.ChangeDonationStatusAsync(donationId, request, cancellationToken);

    public Task GrantAchievementAsync(GrantAchievementRequest request, CancellationToken cancellationToken = default) =>
        inner.GrantAchievementAsync(request, cancellationToken);
}
