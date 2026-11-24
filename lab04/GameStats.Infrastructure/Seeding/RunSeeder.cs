using GameStats.Domain.Entities;
using GameStats.Domain.ValueObjects;
using GameStats.Infrastructure.Persistence;
using MongoDB.Driver;

namespace GameStats.Infrastructure.Seeding;

public class RunSeeder(MongoDbContext context) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var demo = new List<Run>
        {
            new("run-api-demo-001", 1001, "0.1.0-demo",
                new RunStats(5, 3, 600), new PlayerSnapshot("alex_player", "game-link-alex-001")),
            new("run-api-demo-002", 1002, "0.1.0-demo",
                new RunStats(2, 1, 240), new PlayerSnapshot("sam_player", "game-link-sam-002"))
        };

        var ids = demo.Select(r => r.RunId).ToList();
        var existing = await context.Runs.Find(r => ids.Contains(r.RunId))
            .Project(r => r.RunId)
            .ToListAsync(ct);

        var missing = demo.Where(r => !existing.Contains(r.RunId)).ToList();
        if (missing.Count > 0)
            await context.Runs.InsertManyAsync(missing, cancellationToken: ct);   // bulk
    }
}