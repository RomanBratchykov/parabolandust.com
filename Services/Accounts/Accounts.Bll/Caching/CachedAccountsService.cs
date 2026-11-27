using System.Globalization;
using Accounts.Bll.Contracts;
using Accounts.Bll.Events;
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
        long userId;
        try { userId = await inner.CreateUserAsync(request, cancellationToken); }
        catch (AccountEventPublicationException)
        {
            await invalidator.InvalidateAsync(["accounts"], "user created; event publication failed");
            throw;
        }
        await invalidator.InvalidateAsync(["accounts"], "user created");
        return userId;
    }

    public async Task UpdateUserAsync(long userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        try { await inner.UpdateUserAsync(userId, request, cancellationToken); }
        catch (AccountEventPublicationException)
        {
            await invalidator.InvalidateAsync(["accounts"], "user updated; event publication failed");
            throw;
        }
        await invalidator.InvalidateAsync(["accounts"], "user updated");
    }

    public async Task DeleteUserAsync(long userId, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        try { await inner.DeleteUserAsync(userId, expectedRowVersion, cancellationToken); }
        catch (AccountEventPublicationException)
        {
            await invalidator.InvalidateAsync(["accounts"], "user deleted; event publication failed");
            throw;
        }
        await invalidator.InvalidateAsync(["accounts"], "user deleted");
    }

    public async Task RotateIdentityAsync(long userId, RotateIdentityRequest request, CancellationToken cancellationToken = default)
    {
        try { await inner.RotateIdentityAsync(userId, request, cancellationToken); }
        catch (AccountEventPublicationException)
        {
            await invalidator.InvalidateAsync(["accounts"], "identity rotated; event publication failed");
            throw;
        }
        await invalidator.InvalidateAsync(["accounts"], "identity rotated");
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
