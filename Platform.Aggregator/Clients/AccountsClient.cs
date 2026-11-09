using Microsoft.Extensions.Options;

namespace Platform.Aggregator.Clients;

public sealed class AccountsClient(
    HttpClient http, IOptions<DownstreamOptions> options, ILogger<AccountsClient> logger)
{
    public Task<AccountResponse?> GetUserAsync(long userId, CancellationToken cancellationToken) =>
        DownstreamJson.GetAsync<AccountResponse>(http, options.Value, logger,
            "accounts-api", $"/api/users/{userId}", cancellationToken);
}

public sealed record AccountResponse(long UserId, string UserName, string? UserPhotoUrl = null);
