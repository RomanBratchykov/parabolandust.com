using System.Text.Json.Serialization;

namespace Auth.Api.Tokens;

// Internal result for cookie delivery, never an HTTP response body or log payload.
public sealed class RefreshTokenGrant(string userId, string token, DateTimeOffset expiresAt)
{
    public string UserId { get; } = userId;
    [JsonIgnore]
    public string Token { get; } = token;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;

    public override string ToString() => "Refresh token grant (redacted)";
}
