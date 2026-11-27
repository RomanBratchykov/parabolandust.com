using System.Text.Json.Serialization;

namespace Auth.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LoginRequest
{
    public string? UserName { get; init; }
    public string? Password { get; init; }
}
