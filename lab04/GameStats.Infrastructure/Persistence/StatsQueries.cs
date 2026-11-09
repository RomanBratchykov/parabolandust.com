using GameStats.Application.Common.Interfaces;
using GameStats.Application.Common.Models;
using GameStats.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameStats.Infrastructure.Persistence;

public class StatsQueries(MongoDbContext context) : IStatsQueries
{
    private readonly IMongoCollection<StatsUser> _stats = context.StatsUsers;

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(int top, CancellationToken ct)
    {
        var sort = Builders<StatsUser>.Sort.Combine(
            Builders<StatsUser>.Sort.Descending("totals.times_won"),
            Builders<StatsUser>.Sort.Ascending("totals.times_killed"));

        var projection = new BsonDocument
        {
            { "_id", 0 }, { "user_id", 1 }, { "user_whoami", 1 }, { "totals", 1 }
        };

        var docs = await _stats.Find(FilterDefinition<StatsUser>.Empty)
            .Sort(sort)
            .Limit(top)
            .Project<BsonDocument>(projection)
            .ToListAsync(ct);

        return docs.Select(d =>
        {
            var totals = d.GetValue("totals", new BsonDocument()).AsBsonDocument;
            return new LeaderboardEntryDto(
                d["user_id"].ToInt64(),
                d.GetValue("user_whoami", "").AsString,
                totals.GetValue("times_won", 0).ToInt32(),
                totals.GetValue("times_killed", 0).ToInt32());
        }).ToList();
    }
}