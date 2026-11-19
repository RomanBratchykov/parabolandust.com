using GameStats.Application.Events;
using GameStats.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Platform.Caching;
using Platform.Messaging.Events;
using Xunit;

namespace GameStats.Tests;

public sealed class MongoFactAttribute : FactAttribute
{
    public MongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GAMESTATS_TEST_CONNECTION")))
            Skip = "Set GAMESTATS_TEST_CONNECTION to a disposable MongoDB replica-set database; tests create temporary databases.";
    }
}

public sealed class MongoAccountEventProjectionTests
{
    [MongoFact]
    public async Task Transactions_deduplicate_events_and_preserve_runs_gameplay_and_independent_unlocks()
    {
        await using var fixture = new MongoFixture();
        var stats = fixture.Database.GetCollection<BsonDocument>("stats_user");
        var runs = fixture.Database.GetCollection<BsonDocument>("runs");
        var inbox = fixture.Database.GetCollection<BsonDocument>("processed_events");
        var original = StatisticsProjectionTests.LegacyStatistics();
        var run = new BsonDocument
        {
            { "_id", "historic-run" }, { "user_id", new BsonInt64(8001) },
            { "player_snapshot", new BsonDocument { { "user_name", "old-name" }, { "user_whoami", "old-identity" } } },
            { "current_stats", new BsonDocument("rooms_visited", 12) }
        };
        await stats.InsertOneAsync(original);
        await runs.InsertOneAsync(run);
        await using var provider = AccountEventHandlerTests.CreateProvider();
        var handler = new AccountEventHandler(new MongoAccountEventProjection(fixture.Database),
            provider.GetRequiredService<CacheInvalidator>());
        var rotated = new AccountIdentityRotatedEvent(StatisticsProjectionTests.Account() with { SourceVersion = 3 });
        var deleted = new AccountDeletedEvent(rotated.Account with { SourceVersion = 4, IsDeleted = true });
        var stale = new AccountCreatedEvent(StatisticsProjectionTests.Account());
        var unlock = new AchievementUnlockedEvent(8001, 42, DateTimeOffset.UtcNow);
        await handler.HandleAsync(rotated, default);
        var generation = provider.GetRequiredService<CacheGroupState>().Capture("dashboard");
        await handler.HandleAsync(rotated, default);
        Assert.True(generation.Token.IsCancellationRequested);
        await handler.HandleAsync(deleted, default);
        await handler.HandleAsync(stale, default);
        await handler.HandleAsync(unlock, default);
        await handler.HandleAsync(unlock with { EventId = Guid.NewGuid() }, default);

        var result = await stats.Find(new BsonDocument("user_id", new BsonInt64(8001))).SingleAsync();
        Assert.Equal(4L, result["source_version"].AsInt64);
        Assert.True(result["is_deleted"].AsBoolean);
        Assert.Equal("source-identity", result["user_whoami"].AsString);
        Assert.Equal(original["totals"], result["totals"]);
        Assert.Equal(original["enemies"], result["enemies"]);
        Assert.Equal(new BsonArray { "First Steps", new BsonInt64(42) }, result["progress"]["achievements_got"]);
        Assert.Equal(run, await runs.Find(new BsonDocument("_id", "historic-run")).SingleAsync());
        Assert.Equal(5, await inbox.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [MongoFact]
    public async Task Failed_projection_rolls_back_the_inbox_and_allows_retry_of_the_same_event()
    {
        await using var fixture = new MongoFixture();
        // Reject just this identity, so the first transaction fails after its inbox write.
        await fixture.Database.CreateCollectionAsync("stats_user", new CreateCollectionOptions<BsonDocument>
        {
            Validator = new BsonDocument("user_whoami", new BsonDocument("$ne", "reject-identity"))
        });
        var projection = new MongoAccountEventProjection(fixture.Database);
        var value = new AccountCreatedEvent(StatisticsProjectionTests.Account() with { UserWhoAmI = "reject-identity" });
        var inbox = fixture.Database.GetCollection<BsonDocument>("processed_events");
        var stats = fixture.Database.GetCollection<BsonDocument>("stats_user");

        await Assert.ThrowsAsync<MongoWriteException>(() => projection.ApplyAsync(value, default));
        Assert.Equal(0, await inbox.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(0, await stats.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        await projection.ApplyAsync(new AccountCreatedEvent(StatisticsProjectionTests.Account()) { EventId = value.EventId }, default);
        Assert.Equal(1, await inbox.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(1, await stats.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [MongoFact]
    public async Task Concurrent_duplicate_deliveries_eventually_leave_one_projection_and_one_inbox_entry()
    {
        await using var fixture = new MongoFixture();
        var value = new AccountCreatedEvent(StatisticsProjectionTests.Account());
        var projection = new MongoAccountEventProjection(fixture.Database);
        // A competing insert can abort one transaction; redelivery then sees the committed inbox.
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try { await projection.ApplyAsync(value, default); return true; }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            { return false; }
        }));
        Assert.Contains(true, results);
        await projection.ApplyAsync(value, default);
        Assert.Equal(1, await fixture.Database.GetCollection<BsonDocument>("processed_events")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(1, await fixture.Database.GetCollection<BsonDocument>("stats_user")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    private sealed class MongoFixture : IAsyncDisposable
    {
        private readonly MongoClient client;
        public IMongoDatabase Database { get; }

        public MongoFixture()
        {
            var url = MongoUrl.Create(Environment.GetEnvironmentVariable("GAMESTATS_TEST_CONNECTION")!);
            if (string.IsNullOrWhiteSpace(url.DatabaseName))
                throw new InvalidOperationException("GAMESTATS_TEST_CONNECTION must include a database name.");
            client = new MongoClient(MongoClientSettings.FromUrl(url));
            Database = client.GetDatabase("lab8_test_" + Guid.NewGuid().ToString("N"));
        }

        public async ValueTask DisposeAsync() => await client.DropDatabaseAsync(Database.DatabaseNamespace.DatabaseName);
    }
}
