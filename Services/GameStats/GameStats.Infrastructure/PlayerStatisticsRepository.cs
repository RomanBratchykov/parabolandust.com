using GameStats.Application;
using GameStats.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameStats.Infrastructure;

public sealed class PlayerStatisticsRepository(IMongoDatabase database) : IPlayerStatisticsRepository
{
    private readonly IMongoCollection<BsonDocument> _statistics =
        database.GetCollection<BsonDocument>("stats_user");

    public async Task<PlayerStatistics?> GetByUserIdAsync(
        long userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("user_id", new BsonInt64(userId));
        var projection = Builders<BsonDocument>.Projection
            .Include("user_id").Include("updated_at").Include("totals").Exclude("_id");
        var document = await _statistics.Find(filter).Project(projection)
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null)
            return null;

        var totals = document["totals"].AsBsonDocument;
        return new PlayerStatistics(
            document["user_id"].AsInt64,
            new DateTimeOffset(document["updated_at"].AsBsonDateTime.ToUniversalTime()),
            new PlayerTotals(totals["times_killed"].AsInt32, totals["times_won"].AsInt32,
                totals["time_played"].AsInt64));
    }
}
