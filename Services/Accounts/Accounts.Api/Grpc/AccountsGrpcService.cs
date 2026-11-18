using Accounts.Bll;
using Accounts.Grpc;
using Grpc.Core;
using Platform.ServiceDefaults;

namespace Accounts.Api.Grpc;

public sealed class AccountsGrpcService(IAccountsService accounts, BusinessMetrics metrics) : AccountsReader.AccountsReaderBase
{
    public override async Task<UserReply> GetUser(UserRequest request, ServerCallContext context)
    {
        using var operation = metrics.Start("account.read");
        if (request.UserId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "User ID must be positive."));

        try
        {
            var user = await accounts.GetUserAsync(request.UserId, context.CancellationToken)
                ?? throw new RpcException(new Status(StatusCode.NotFound, "User was not found."));
            var reply = new UserReply { UserId = user.UserId, UserName = user.UserName };
            if (user.UserPhotoUrl is not null) reply.PhotoUrl = user.UserPhotoUrl;
            operation.Succeed();
            return reply;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled."));
        }
    }
}
