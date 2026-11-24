namespace Auth.Api.Models;

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    // Store a SHA-256 hexadecimal digest, never the usable cookie value.
    public required string TokenHash { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacementTokenId { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
