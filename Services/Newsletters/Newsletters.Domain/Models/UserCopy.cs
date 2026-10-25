namespace Newsletters.Domain.Models;

public class UserCopy
{
    private UserCopy() { }

    public long Id { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset SourceUpdatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }
    public long RowVersion { get; private set; }
    public ICollection<UserComment> Comments { get; } = new List<UserComment>();
    public ICollection<Subscription> Subscriptions { get; } = new List<Subscription>();
}
