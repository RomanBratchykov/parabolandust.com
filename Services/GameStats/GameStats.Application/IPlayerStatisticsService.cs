using GameStats.Domain;

namespace GameStats.Application;

public interface IPlayerStatisticsService
{
    Task<PlayerStatistics?> GetByUserIdAsync(
        long userId, CancellationToken cancellationToken = default);
}
