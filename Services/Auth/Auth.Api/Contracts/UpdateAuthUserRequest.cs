using System.Text.Json.Serialization;

namespace Auth.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateAuthUserRequest
{
    public string? UserName { get; init; }
}
