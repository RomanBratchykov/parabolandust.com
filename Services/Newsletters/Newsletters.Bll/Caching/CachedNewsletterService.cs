using System.Globalization;
using Newsletters.Bll.Contracts;
using Newsletters.Bll.Services;
using Platform.Caching;

namespace Newsletters.Bll.Caching;

public sealed class CachedNewsletterService(INewsletterService inner, ICacheAside cache,
    CacheInvalidator invalidator) : INewsletterService
{
    public Task<PagedResult<NewsletterDto>> GetListAsync(NewsletterQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = query with
        {
            Page = Math.Max(query.Page, 1), PageSize = Math.Clamp(query.PageSize, 1, 100),
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            SortBy = query.SortBy.ToLowerInvariant(), SortDir = query.SortDir.ToLowerInvariant(),
            PublishedAfter = query.PublishedAfter?.ToUniversalTime()
        };
        return ReadAsync("list", CacheKeys.ForQuery(normalized), CachePolicy.Newsletters,
            token => inner.GetListAsync(normalized, token), cancellationToken);
    }

    public Task<NewsletterDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => ReadAsync("card", Identity(id), CachePolicy.Newsletters,
            token => inner.GetByIdAsync(id, token), cancellationToken);

    public Task<NewsletterDetailsDto> GetWithDetailsAsync(long id, CancellationToken cancellationToken = default)
        => ReadAsync("details", Identity(id), CachePolicy.Newsletters,
            token => inner.GetWithDetailsAsync(id, token), cancellationToken);

    public Task<UserSubscriptionsDto> GetUserSubscriptionsAsync(long userId, CancellationToken cancellationToken = default)
        => ReadAsync("subscriptions", Identity(userId), CachePolicy.PlayerProjection,
            token => inner.GetUserSubscriptionsAsync(userId, token), cancellationToken);

    public async Task<NewsletterDto> CreateAsync(CreateNewsletterRequest request, CancellationToken cancellationToken = default)
    {
        var result = await inner.CreateAsync(request, cancellationToken);
        await invalidator.InvalidateAsync(["newsletters"], "newsletter created");
        return result;
    }

    public async Task PublishAsync(long id, PublishNewsletterRequest request, CancellationToken cancellationToken = default)
    {
        await inner.PublishAsync(id, request, cancellationToken);
        await invalidator.InvalidateAsync(["newsletters"], "newsletter published");
    }

    public Task<IReadOnlyList<SubscriberDto>> GetActiveSubscribersAsync(
        long newsletterId, DateTimeOffset since, CancellationToken cancellationToken = default)
        => inner.GetActiveSubscribersAsync(newsletterId, since, cancellationToken);

    private async Task<T> ReadAsync<T>(string operation, string identity, CachePolicy policy,
        Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken) where T : class
        => (await cache.GetOrCreateAsync<T>("newsletters", operation, identity, policy,
            async token => await factory(token), cancellationToken))!;

    private static string Identity(long id) => id.ToString(CultureInfo.InvariantCulture);
}
