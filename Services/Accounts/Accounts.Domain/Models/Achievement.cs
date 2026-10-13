namespace Accounts.Domain.Models;

public sealed class Achievement
{
    public long AchievementId { get; set; }
    public string AchievementName { get; set; } = string.Empty;
    public string AchievementDescription { get; set; } = string.Empty;
    public bool IsSecret { get; set; }
}
