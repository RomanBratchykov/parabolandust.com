namespace Accounts.Domain.Models;

public sealed class AccountUser
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? OAuthProvider { get; set; }
    public string? OAuthId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string UserWhoAmI { get; set; } = string.Empty;
    public string? UserPhotoUrl { get; set; }
    public long RowVersion { get; set; }
    public bool IsDeleted { get; set; }
}
