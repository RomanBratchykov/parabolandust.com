using GameStats.Domain.Entities;
using GameStats.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameStats.Infrastructure.Seeding;

public class GameEventSeeder(MongoDbContext context) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var demo = new List<GameEvent>
        {
            new("event-api-demo-001", "item_collected", 1001, "run-api-demo-001",
                new BsonDocument { { "item_id", "silver_key" }, { "item_name", "Silver Key" }, { "room", 3 } }),
            new("event-api-demo-002", "boss_defeated", 1001, "run-api-demo-001",
                new BsonDocument { { "boss", "Meowbert" }, { "duration_sec", 95 } })
        };

        var ids = demo.Select(e => e.EventId).ToList();
        var existing = await context.GameEvents.Find(e => ids.Contains(e.EventId))
            .Project(e => e.EventId)
            .ToListAsync(ct);

        var missing = demo.Where(e => !existing.Contains(e.EventId)).ToList();
        if (missing.Count > 0)
            await context.GameEvents.InsertManyAsync(missing, cancellationToken: ct);
    }
}