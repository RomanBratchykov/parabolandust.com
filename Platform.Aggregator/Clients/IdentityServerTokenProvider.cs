using System.Text.Json.Serialization;

namespace Platform.Aggregator.Clients;

// A single short-lived cache is shared by the three downstream gRPC clients.
public sealed class IdentityServerTokenProvider(
    IHttpClientFactory clients, IConfiguration configuration, IHostEnvironment environment)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? cachedToken;
    private DateTimeOffset refreshAfter;

    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cachedToken is not null && DateTimeOffset.UtcNow < refreshAfter)
                return cachedToken;

            var address = Required("ServiceCredentials:TokenEndpoint");
            if (!Uri.TryCreate(address, UriKind.Absolute, out var endpoint) ||
                !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) ||
                !string.IsNullOrEmpty(endpoint.Fragment) ||
                (endpoint.Scheme != Uri.UriSchemeHttps &&
                    !(environment.IsDevelopment() && endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)))
                throw new InvalidOperationException("ServiceCredentials:TokenEndpoint must use HTTPS, except HTTP loopback in Development.");

            var clientId = Required("ServiceCredentials:ClientId");
            var clientSecret = Required("ServiceCredentials:ClientSecret");
            var scopes = Required("ServiceCredentials:Scopes");
            var requestedScopes = scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestedScopes.Length == 0 || requestedScopes.Any(scope => !scope.EndsWith(".read", StringComparison.Ordinal)))
                throw new InvalidOperationException("ServiceCredentials:Scopes must contain only read scopes ending in .read.");
            scopes = string.Join(' ', requestedScopes.Distinct(StringComparer.Ordinal));
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = scopes
                })
            };
            var requestedAt = DateTimeOffset.UtcNow;
            using var response = await clients.CreateClient("identityserver-token")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > 65536)
                throw new InvalidOperationException("IdentityServer did not issue a service credential.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int count;
            while ((count = await stream.ReadAsync(chunk, cancellationToken)) != 0)
            {
                if (buffer.Length + count > 65536)
                    throw new InvalidOperationException("IdentityServer token response exceeded the size limit.");
                await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
            }
            buffer.Position = 0;
            var result = await System.Text.Json.JsonSerializer.DeserializeAsync<TokenResponse>(buffer,
                cancellationToken: cancellationToken);
            if (result is null || string.IsNullOrWhiteSpace(result.AccessToken) ||
                !string.Equals(result.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                result.ExpiresIn <= 0)
                throw new InvalidOperationException("IdentityServer returned an invalid service credential.");

            var nextRefresh = requestedAt.AddSeconds(Math.Min(result.ExpiresIn * 0.8, 3600));
            if (DateTimeOffset.UtcNow >= nextRefresh)
                throw new InvalidOperationException("IdentityServer returned a service credential too close to expiry.");
            cachedToken = result.AccessToken;
            // Refresh before expiry, and never retain a service token for over an hour.
            refreshAfter = nextRefresh;
            return cachedToken;
        }
        finally { gate.Release(); }
    }

    private string Required(string key) => !string.IsNullOrWhiteSpace(configuration[key])
        ? configuration[key]! : throw new InvalidOperationException($"{key} is required in IdentityServer mode.");

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
        [JsonPropertyName("token_type")] public string? TokenType { get; init; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
    }
}
