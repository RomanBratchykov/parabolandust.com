using System.Text.Json.Serialization;

namespace Auth.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RegisterRequest
{
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }
}
