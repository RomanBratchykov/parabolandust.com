using System.ComponentModel.DataAnnotations;

namespace Accounts.Bll.Contracts;

public sealed record UserResponse(
    long UserId,
    string UserName,
    string UserEmail,
    string UserWhoAmI,
    string? UserPhotoUrl,
    DateTimeOffset CreatedAt,
    long RowVersion);

public sealed class CreateUserRequest
{
    [Required, StringLength(50)] public string UserName { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(254)] public string UserEmail { get; init; } = string.Empty;
    [StringLength(255)] public string? PasswordHash { get; init; }
    [StringLength(50)] public string? OAuthProvider { get; init; }
    [StringLength(200)] public string? OAuthId { get; init; }
    [Required, StringLength(128)] public string UserWhoAmI { get; init; } = string.Empty;
    [Url, StringLength(2048)] public string? UserPhotoUrl { get; init; }
    [Range(1, long.MaxValue)] public long? InitialAchievementId { get; init; }
}

public sealed class UpdateUserRequest
{
    [Required, StringLength(50)] public string UserName { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(254)] public string UserEmail { get; init; } = string.Empty;
    [Url, StringLength(2048)] public string? UserPhotoUrl { get; init; }
    [Range(1, long.MaxValue)] public long ExpectedRowVersion { get; init; }
}

public sealed record DonationResponse(
    long DonationId,
    long? UserId,
    decimal DonationAmount,
    string? Commentary,
    DateTimeOffset DonatedAt,
    string DonationStatus,
    bool IsAnonymous,
    string Currency,
    long RowVersion);

public sealed class CreateDonationRequest
{
    [Range(0, long.MaxValue)] public long? UserId { get; init; }
    [Range(typeof(decimal), "0.01", "9999999999.99")] public decimal DonationAmount { get; init; }
    [StringLength(500)] public string? Commentary { get; init; }
    [Required, StringLength(3, MinimumLength = 3)] public string Currency { get; init; } = string.Empty;
    public bool IsAnonymous { get; init; }
    [StringLength(200)] public string? ProviderPaymentId { get; init; }
}

public sealed class UpdateDonationRequest
{
    [Range(typeof(decimal), "0.01", "9999999999.99")] public decimal DonationAmount { get; init; }
    [StringLength(500)] public string? Commentary { get; init; }
    [Required, StringLength(3, MinimumLength = 3)] public string Currency { get; init; } = string.Empty;
    public bool IsAnonymous { get; init; }
    [StringLength(200)] public string? ProviderPaymentId { get; init; }
    [Range(1, long.MaxValue)] public long ExpectedRowVersion { get; init; }
}

public sealed class ChangeDonationStatusRequest
{
    [Required] public string Status { get; init; } = string.Empty;
    [Range(1, long.MaxValue)] public long ExpectedRowVersion { get; init; }
}

public sealed record GrantAchievementRequest(
    [property: Range(1, long.MaxValue)] long UserId,
    [property: Range(1, long.MaxValue)] long AchievementId);
