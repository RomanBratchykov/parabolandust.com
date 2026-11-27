using Newsletters.Grpc;

namespace Platform.Aggregator.Clients;

public sealed class NewslettersClient(
    NewslettersReader.NewslettersReaderClient client, GrpcCall calls)
{
    public async Task<SubscriptionsResponse?> GetSubscriptionsAsync(long userId, CancellationToken cancellationToken)
    {
        var reply = await calls.InvokeAsync("newsletters-api", "newsletters.v1.NewslettersReader/GetUserSubscriptions",
            (deadline, token) => client.GetUserSubscriptionsAsync(new UserRequest { UserId = userId },
                deadline: deadline, cancellationToken: token), cancellationToken);
        if (reply is null)
            return null;
        try
        {
            return new SubscriptionsResponse(reply.UserId, reply.Subscriptions.Select(item =>
                new SubscriptionResponse(item.SubscriptionId, item.NewsletterId, item.Title, item.Status,
                    item.ConsentAt?.ToDateTimeOffset()
                        ?? throw new InvalidDataException("A subscription consent timestamp is missing."))).ToArray());
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("A subscription consent timestamp is invalid.", exception);
        }
    }
}

public sealed record SubscriptionsResponse(long Id, IReadOnlyList<SubscriptionResponse> Subscriptions);
public sealed record SubscriptionResponse(
    long Id, long NewsletterId, string NewsletterTitle, string Status, DateTimeOffset ConsentAt);
