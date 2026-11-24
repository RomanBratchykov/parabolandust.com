using GameStats.Application.Common.Models;

namespace GameStats.Application.Common.Interfaces;

public interface IRunQueries
{
    Task<RunDto?> GetByIdAsync(string id, CancellationToken ct);
    Task<PagedResult<RunDto>> GetListAsync(long? userId, int page, int pageSize, CancellationToken ct);
}

public interface IStatsQueries
{
    Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(int top, CancellationToken ct);
}

public interface IGameEventQueries
{
    Task<IReadOnlyList<EventCountDto>> GetCountsByTypeAsync(long userId, CancellationToken ct);
}