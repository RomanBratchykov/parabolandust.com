using System.Text.Json.Serialization;

namespace Auth.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ResetPasswordRequest
{
    public string? UserId { get; init; }
    public string? Token { get; init; }
    public string? NewPassword { get; init; }
}
