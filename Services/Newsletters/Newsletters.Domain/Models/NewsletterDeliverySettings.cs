namespace Newsletters.Domain.Models;

public class NewsletterDeliverySettings
{
    private NewsletterDeliverySettings() { }

    public NewsletterDeliverySettings(long newsletterId, string? replyToEmail = null)
    {
        if (newsletterId <= 0)
            throw new ArgumentOutOfRangeException(nameof(newsletterId));
        if (replyToEmail?.Length > 254)
            throw new ArgumentException("Reply-to email must be at most 254 characters.", nameof(replyToEmail));

        NewsletterId = newsletterId;
        ReplyToEmail = replyToEmail;
        RecipientSettings = "{\"subscribers\": true}";
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = "api";
        RowVersion = 1;
    }

    public long NewsletterId { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }
    public string RecipientSettings { get; private set; } = "{\"subscribers\": true}";
    public string? ReplyToEmail { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public long RowVersion { get; private set; }
    public Newsletter Newsletter { get; private set; } = null!;
}
