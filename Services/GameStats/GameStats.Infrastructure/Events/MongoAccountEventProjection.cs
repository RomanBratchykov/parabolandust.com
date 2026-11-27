using GameStats.Application.Events;
using MongoDB.Bson;
using MongoDB.Driver;
using Platform.Messaging.Events;

namespace GameStats.Infrastructure.Events;

public sealed class MongoAccountEventProjection(IMongoDatabase database) : IAccountEventProjection
{
    private readonly IMongoCollection<BsonDocument> statistics = database.GetCollection<BsonDocument>("stats_user");
    private readonly IMongoCollection<BsonDocument> inbox = database.GetCollection<BsonDocument>("processed_events");

    public async Task ApplyAsync(IntegrationEvent value, CancellationToken cancellationToken)
    {
        // Reuse the Lab 4 unique key, including when this is a fresh database.
        await statistics.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("user_id"),
            new CreateIndexOptions { Unique = true, Name = "ux_stats_user_id" }), cancellationToken: cancellationToken);
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        await session.WithTransactionAsync(async (transaction, token) =>
        {
            var eventId = value.EventId.ToString("D");
            var eventFilter = Builders<BsonDocument>.Filter.Eq("_id", eventId);
            if (await inbox.Find(transaction, eventFilter).AnyAsync(token)) return true;
            await inbox.InsertOneAsync(transaction, new BsonDocument
            {
                { "_id", eventId }, { "processed_at", DateTime.UtcNow }
            }, cancellationToken: token);

            var userFilter = Builders<BsonDocument>.Filter.Eq("user_id", new BsonInt64(StatisticsProjection.UserId(value)));
            var current = await statistics.Find(transaction, userFilter).FirstOrDefaultAsync(token);
            var projected = StatisticsProjection.Apply(current, value);
            if (current is null)
                await statistics.InsertOneAsync(transaction, projected, cancellationToken: token);
            else if (!ReferenceEquals(current, projected))
                await statistics.ReplaceOneAsync(transaction, userFilter, projected, cancellationToken: token);
            return true;
        }, new TransactionOptions(readConcern: ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority,
            readPreference: ReadPreference.Primary), cancellationToken);
    }
}
