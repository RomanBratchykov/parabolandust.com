using Microsoft.Extensions.Options;

namespace Platform.Aggregator.Clients;

public sealed class NewslettersClient(
    HttpClient http, IOptions<DownstreamOptions> options, ILogger<NewslettersClient> logger)
{
    public Task<SubscriptionsResponse?> GetSubscriptionsAsync(long userId, CancellationToken cancellationToken) =>
        DownstreamJson.GetAsync<SubscriptionsResponse>(http, options.Value, logger,
            "newsletters-api", $"/api/users-copy/{userId}/subscriptions", cancellationToken);
}

public sealed record SubscriptionsResponse(long Id, IReadOnlyList<SubscriptionResponse> Subscriptions);
public sealed record SubscriptionResponse(
    long Id, long NewsletterId, string NewsletterTitle, string Status, DateTimeOffset ConsentAt);
