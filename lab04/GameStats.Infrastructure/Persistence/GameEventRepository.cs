using GameStats.Domain.Entities;
using GameStats.Domain.Interfaces;
using MongoDB.Driver;

namespace GameStats.Infrastructure.Persistence;

public class GameEventRepository(MongoDbContext context) : IGameEventRepository
{
    private readonly IMongoCollection<GameEvent> _events = context.GameEvents;

    public Task<bool> ExistsByEventIdAsync(string eventId, CancellationToken ct = default)
        => _events.Find(e => e.EventId == eventId).AnyAsync(ct);

    public Task AddAsync(GameEvent gameEvent, CancellationToken ct = default)
        => _events.InsertOneAsync(gameEvent, cancellationToken: ct);
}