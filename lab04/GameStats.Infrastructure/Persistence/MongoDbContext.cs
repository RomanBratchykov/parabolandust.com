using GameStats.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GameStats.Infrastructure.Persistence;

public class MongoDbContext
{
    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        Database = client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<Run> Runs => Database.GetCollection<Run>("runs");
    public IMongoCollection<GameEvent> GameEvents => Database.GetCollection<GameEvent>("game_events");
    public IMongoCollection<StatsUser> StatsUsers => Database.GetCollection<StatsUser>("stats_user");
}