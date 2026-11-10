using Accounts.Grpc;

namespace Platform.Aggregator.Clients;

public sealed class AccountsClient(
    AccountsReader.AccountsReaderClient client, GrpcCall calls)
{
    public async Task<AccountResponse?> GetUserAsync(long userId, CancellationToken cancellationToken)
    {
        var reply = await calls.InvokeAsync("accounts-api", "accounts.v1.AccountsReader/GetUser",
            (deadline, token) => client.GetUserAsync(new UserRequest { UserId = userId },
                deadline: deadline, cancellationToken: token), cancellationToken);
        return reply is null ? null : new AccountResponse(reply.UserId, reply.UserName,
            reply.HasPhotoUrl ? reply.PhotoUrl : null);
    }
}

public sealed record AccountResponse(long UserId, string UserName, string? UserPhotoUrl = null);
