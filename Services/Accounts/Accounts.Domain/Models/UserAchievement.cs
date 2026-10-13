namespace Accounts.Domain.Models;

public sealed class UserAchievement
{
    public long UserId { get; set; }
    public long AchievementId { get; set; }
    public DateTimeOffset UnlockedAt { get; set; }
}
