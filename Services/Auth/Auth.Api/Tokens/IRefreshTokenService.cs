namespace Auth.Api.Tokens;

public interface IRefreshTokenService
{
    Task<RefreshTokenGrant> IssueAsync(string userId, CancellationToken cancellationToken = default);
    Task<RefreshTokenGrant?> RotateAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string userId, string token, CancellationToken cancellationToken = default);
}
