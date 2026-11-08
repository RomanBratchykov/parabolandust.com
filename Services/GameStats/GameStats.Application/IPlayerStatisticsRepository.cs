using GameStats.Domain;

namespace GameStats.Application;

public interface IPlayerStatisticsRepository
{
    Task<PlayerStatistics?> GetByUserIdAsync(
        long userId, CancellationToken cancellationToken = default);
}
