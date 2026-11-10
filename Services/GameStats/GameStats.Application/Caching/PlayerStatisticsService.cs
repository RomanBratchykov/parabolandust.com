using System.Globalization;
using GameStats.Domain;
using Platform.Caching;

namespace GameStats.Application.Caching;

public sealed class PlayerStatisticsService(
    IPlayerStatisticsRepository repository, ICacheAside cache) : IPlayerStatisticsService
{
    public Task<PlayerStatistics?> GetByUserIdAsync(
        long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        // MongoDB writes are external; their changes become visible after this short TTL.
        return cache.GetOrCreateAsync("gamestats", "statistics",
            userId.ToString(CultureInfo.InvariantCulture), CachePolicy.GameStats,
            token => repository.GetByUserIdAsync(userId, token), cancellationToken);
    }
}
