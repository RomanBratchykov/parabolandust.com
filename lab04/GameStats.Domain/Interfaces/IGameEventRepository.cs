using GameStats.Domain.Entities;

namespace GameStats.Domain.Interfaces;

public interface IGameEventRepository
{
    Task<bool> ExistsByEventIdAsync(string eventId, CancellationToken ct = default);
    Task AddAsync(GameEvent gameEvent, CancellationToken ct = default);
}