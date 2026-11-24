using GameStats.Application.Common.Interfaces;
using GameStats.Application.Common.Models;
using GameStats.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameStats.Infrastructure.Persistence;

public class RunQueries(MongoDbContext context) : IRunQueries
{
    private readonly IMongoCollection<Run> _runs = context.Runs;

    // з БД вибираються лише потрібні поля
    private static readonly BsonDocument Fields = new()
    {
        { "run_id", 1 }, { "user_id", 1 }, { "player_snapshot.user_name", 1 },
        { "started_at", 1 }, { "ended_at", 1 }, { "game_version", 1 },
        { "successful", 1 }, { "current_stats", 1 }, { "version", 1 }
    };

    public async Task<RunDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return null;

        var doc = await _runs.Find(r => r.Id == objectId)
            .Project<BsonDocument>(Fields)
            .FirstOrDefaultAsync(ct);

        return doc is null ? null : ToDto(doc);
    }

    public async Task<PagedResult<RunDto>> GetListAsync(
        long? userId, int page, int pageSize, CancellationToken ct)
    {
        var filter = userId is null
            ? Builders<Run>.Filter.Empty
            : Builders<Run>.Filter.Eq(r => r.UserId, userId.Value);

        var total = await _runs.CountDocumentsAsync(filter, cancellationToken: ct);

        var docs = await _runs.Find(filter)
            .Sort(Builders<Run>.Sort.Descending(r => r.StartedAt))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project<BsonDocument>(Fields)
            .ToListAsync(ct);

        return new PagedResult<RunDto>(docs.Select(ToDto).ToList(), total, page, pageSize);
    }

    private static RunDto ToDto(BsonDocument d)
    {
        var stats = d.GetValue("current_stats", new BsonDocument()).AsBsonDocument;
        var snapshot = d.GetValue("player_snapshot", BsonNull.Value);
        var ended = d.GetValue("ended_at", BsonNull.Value);
        var successful = d.GetValue("successful", BsonNull.Value);

        return new RunDto(
            d["_id"].AsObjectId.ToString(),
            d.GetValue("run_id", "").AsString,
            d.GetValue("user_id", 0L).ToInt64(),
            snapshot.IsBsonDocument ? snapshot.AsBsonDocument.GetValue("user_name", "").AsString : "",
            d["started_at"].ToUniversalTime(),
            ended.IsBsonNull ? (DateTime?)null : ended.ToUniversalTime(),
            d.GetValue("game_version", "").AsString,
            successful.IsBoolean ? (bool?)successful.AsBoolean : null,
            stats.GetValue("enemies_killed", 0).ToInt32(),
            stats.GetValue("rooms_visited", 0).ToInt32(),
            stats.GetValue("time_played", 0).ToInt32(),
            d.GetValue("version", 0).ToInt32());
    }
}