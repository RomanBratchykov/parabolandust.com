using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Newsletters.Bll.Services;
using Newsletters.Domain.Exceptions;
using Newsletters.Grpc;

namespace Newsletters.Api.Grpc;

public sealed class NewslettersGrpcService(INewsletterService newsletters) : NewslettersReader.NewslettersReaderBase
{
    public override async Task<UserSubscriptionsReply> GetUserSubscriptions(UserRequest request, ServerCallContext context)
    {
        if (request.UserId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "User ID must be positive."));
        try
        {
            var user = await newsletters.GetUserSubscriptionsAsync(request.UserId, context.CancellationToken);
            var reply = new UserSubscriptionsReply { UserId = user.Id };
            reply.Subscriptions.AddRange(user.Subscriptions.Select(subscription => new SubscriptionReply
            {
                SubscriptionId = subscription.Id,
                NewsletterId = subscription.NewsletterId,
                Title = subscription.NewsletterTitle,
                Status = subscription.Status,
                ConsentAt = Timestamp.FromDateTimeOffset(subscription.ConsentAt)
            }));
            return reply;
        }
        catch (EntityNotFoundException)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "User subscriptions were not found."));
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request canceled."));
        }
    }
}
