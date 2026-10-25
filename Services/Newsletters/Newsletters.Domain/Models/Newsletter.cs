namespace Newsletters.Domain.Models;

public class Newsletter
{
    private Newsletter() { }

    public Newsletter(long adminId, string title, string mainInfo, string? imageUrl = null)
    {
        if (adminId <= 0)
            throw new ArgumentOutOfRangeException(nameof(adminId), "Admin ID must be positive.");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            throw new ArgumentException("Title is required and must be at most 200 characters.", nameof(title));
        if (string.IsNullOrWhiteSpace(mainInfo))
            throw new ArgumentException("Main information is required.", nameof(mainInfo));
        if (imageUrl?.Length > 2048)
            throw new ArgumentException("Image URL must be at most 2048 characters.", nameof(imageUrl));

        AdminId = adminId;
        Title = title;
        MainInfo = mainInfo;
        ImageUrl = imageUrl;
        IsHidden = true;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = "api";
        RowVersion = 1;
    }

    public long Id { get; private set; }
    public long AdminId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string MainInfo { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public bool IsHidden { get; private set; }
    public string? PublishedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public long RowVersion { get; private set; }
    public Admin Admin { get; private set; } = null!;
    public NewsletterDeliverySettings? DeliverySettings { get; private set; }
    public ICollection<UserComment> Comments { get; } = new List<UserComment>();
    public ICollection<Subscription> Subscriptions { get; } = new List<Subscription>();

    public void Publish(string publishedBy, DateTimeOffset publishedAt)
    {
        if (string.IsNullOrWhiteSpace(publishedBy))
            throw new ArgumentException("Publisher is required.", nameof(publishedBy));
        if (publishedAt == default)
            throw new ArgumentException("Publication time is required.", nameof(publishedAt));

        IsHidden = false;
        PublishedAt = publishedAt;
        PublishedBy = publishedBy;
        UpdatedAt = publishedAt;
        UpdatedBy = publishedBy;
        RowVersion++;
    }

    public void Hide(string updatedBy, DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(updatedBy))
            throw new ArgumentException("Updater is required.", nameof(updatedBy));
        if (updatedAt == default)
            throw new ArgumentException("Update time is required.", nameof(updatedAt));

        IsHidden = true;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
        RowVersion++;
    }
}
