namespace Newsletters.Domain.Models;

public enum SubscriptionStatus
{
    Active,
    Unsubscribed
}

public class Subscription
{
    private Subscription() { }

    public Subscription(long userId, long newsletterId, string? userEmailSnapshot)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));
        if (newsletterId <= 0)
            throw new ArgumentOutOfRangeException(nameof(newsletterId));

        UserId = userId;
        NewsletterId = newsletterId;
        Status = SubscriptionStatus.Active;
        ConsentAt = DateTimeOffset.UtcNow;
        UnsubscribeToken = Guid.NewGuid();
        UserEmailSnapshot = userEmailSnapshot;
        CreatedAt = ConsentAt;
        CreatedBy = "api";
        RowVersion = 1;
    }

    public long Id { get; private set; }
    public long UserId { get; private set; }
    public long NewsletterId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset ConsentAt { get; private set; }
    public Guid UnsubscribeToken { get; private set; }
    public string? UserEmailSnapshot { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public long RowVersion { get; private set; }
    public UserCopy User { get; private set; } = null!;
    public Newsletter Newsletter { get; private set; } = null!;

    public void Unsubscribe(string updatedBy, DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(updatedBy))
            throw new ArgumentException("Updater is required.", nameof(updatedBy));
        if (updatedAt == default)
            throw new ArgumentException("Update time is required.", nameof(updatedAt));

        Status = SubscriptionStatus.Unsubscribed;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
        RowVersion++;
    }
}
