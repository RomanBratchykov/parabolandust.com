using Newsletters.Bll.Contracts;
using Newsletters.Bll.Services;
using Platform.Caching;

namespace Newsletters.Bll.Caching;

public sealed class CachedSubscriptionService(ISubscriptionService inner,
    CacheInvalidator invalidator) : ISubscriptionService
{
    public Task<SubscriptionDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => inner.GetByIdAsync(id, cancellationToken);

    public async Task<SubscriptionDto> CreateAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await inner.CreateAsync(request, cancellationToken);
        await invalidator.InvalidateAsync(["newsletters"], "subscription created");
        return result;
    }
}
