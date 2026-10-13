using Accounts.Domain.Models;

namespace Accounts.Dal.Repositories;

public interface IUserAchievementRepository
{
    Task<IReadOnlyList<UserAchievement>> GetForUserAsync(
        long userId,
        CancellationToken cancellationToken = default);
    Task<bool> AddAsync(
        UserAchievement userAchievement,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(
        long userId,
        long achievementId,
        CancellationToken cancellationToken = default);
}
