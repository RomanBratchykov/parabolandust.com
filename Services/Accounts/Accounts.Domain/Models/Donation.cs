namespace Accounts.Domain.Models;

public sealed class Donation
{
    public long DonationId { get; set; }
    public long? UserId { get; set; }
    public decimal DonationAmount { get; set; }
    public string? Commentary { get; set; }
    public DateTimeOffset DonatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string DonationStatus { get; set; } = "Pending";
    public bool IsAnonymous { get; set; }
    public bool IsDeleted { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? ProviderPaymentId { get; set; }
    public long RowVersion { get; set; }
}
