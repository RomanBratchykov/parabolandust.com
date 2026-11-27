using System.Text.Json.Serialization;

namespace Auth.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VerifyEmailRequest
{
    public string? UserId { get; init; }
    public string? Token { get; init; }
}
