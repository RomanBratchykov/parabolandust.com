namespace Newsletters.Domain.Models;

public class UserComment
{
    private UserComment() { }

    public long Id { get; private set; }
    public long UserId { get; private set; }
    public long NewsletterId { get; private set; }
    public long? ParentCommentId { get; private set; }
    public string CommentText { get; private set; } = string.Empty;
    public bool IsHidden { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public long RowVersion { get; private set; }
    public UserCopy User { get; private set; } = null!;
    public Newsletter Newsletter { get; private set; } = null!;
    public UserComment? ParentComment { get; private set; }
    public ICollection<UserComment> Replies { get; } = new List<UserComment>();
}
