using GameStats.Application.Common.Interfaces;
using GameStats.Application.Common.Models;
using GameStats.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameStats.Infrastructure.Persistence;

public class GameEventQueries(MongoDbContext context) : IGameEventQueries
{
    private readonly IMongoCollection<GameEvent> _events = context.GameEvents;

    public async Task<IReadOnlyList<EventCountDto>> GetCountsByTypeAsync(long userId, CancellationToken ct)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("user_id", userId)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$event_type" },
                { "count", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument { { "count", -1 }, { "_id", 1 } })
        };

        var docs = await _events.Aggregate<BsonDocument>(pipeline).ToListAsync(ct);

        return docs.Select(d => new EventCountDto(d["_id"].AsString, d["count"].ToInt32())).ToList();
    }
}