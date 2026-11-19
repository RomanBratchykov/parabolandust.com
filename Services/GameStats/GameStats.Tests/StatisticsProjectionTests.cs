using GameStats.Infrastructure.Events;
using MongoDB.Bson;
using Platform.Messaging.Events;
using Xunit;

namespace GameStats.Tests;

public sealed class StatisticsProjectionTests
{
    [Fact]
    public void Account_updates_preserve_gameplay_data_and_source_timestamps()
    {
        var original = LegacyStatistics();
        var account = Account() with { SourceVersion = 7, UserWhoAmI = "rotated-identity" };
        var projected = StatisticsProjection.Apply(original, new AccountIdentityRotatedEvent(account));

        Assert.Equal("rotated-identity", projected["user_whoami"].AsString);
        Assert.Equal(7L, projected["source_version"].AsInt64);
        Assert.Equal(account.SourceUpdatedAt.UtcDateTime, projected["source_updated_at"].ToUniversalTime());
        Assert.Equal(original["totals"], projected["totals"]);
        Assert.Equal(original["progress"], projected["progress"]);
        Assert.Equal(original["enemies"], projected["enemies"]);
        Assert.Equal(original["_id"], projected["_id"]);
        Assert.Equal("legacy-identity", original["user_whoami"].AsString);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    public void Stale_or_equal_lifecycle_versions_cannot_resurrect_a_deletion(long version)
    {
        var deleted = StatisticsProjection.Apply(null,
            new AccountDeletedEvent(Account() with { SourceVersion = 5, IsDeleted = true }));
        var result = StatisticsProjection.Apply(deleted, new AccountCreatedEvent(Account() with
        {
            SourceVersion = version, SourceUpdatedAt = Account().SourceUpdatedAt.AddDays(30)
        }));

        Assert.True(result["is_deleted"].AsBoolean);
        Assert.Equal(5L, result["source_version"].AsInt64);
        Assert.Equal(deleted, result);
    }

    [Fact]
    public void New_account_statistics_use_the_existing_schema_and_zero_gameplay_totals()
    {
        var result = StatisticsProjection.Apply(null, new AccountCreatedEvent(Account()));

        Assert.Equal(8001L, result["user_id"].AsInt64);
        Assert.Equal("source-identity", result["user_whoami"].AsString);
        Assert.Equal(0, result["totals"]["times_killed"].AsInt32);
        Assert.Equal(0, result["totals"]["times_won"].AsInt32);
        Assert.Equal(0L, result["totals"]["time_played"].AsInt64);
        Assert.False(result["is_deleted"].AsBoolean);
    }

    [Fact]
    public void Achievement_membership_is_independent_of_profile_order_and_preserves_existing_entries()
    {
        var deleted = StatisticsProjection.Apply(LegacyStatistics(),
            new AccountDeletedEvent(Account() with { SourceVersion = 9, IsDeleted = true }));
        var unlock = new AchievementUnlockedEvent(8001, 42, Account().CreatedAt);
        var result = StatisticsProjection.Apply(deleted, unlock);
        result = StatisticsProjection.Apply(result, unlock with { EventId = Guid.NewGuid() });
        result = StatisticsProjection.Apply(result, new AchievementUnlockedEvent(8001, 43, Account().CreatedAt));

        Assert.Equal(new BsonArray { "First Steps", new BsonInt64(42), new BsonInt64(43) },
            result["progress"]["achievements_got"]);
        Assert.Equal(deleted["progress"]["rooms_visited"], result["progress"]["rooms_visited"]);
        Assert.Equal(deleted["totals"], result["totals"]);
        Assert.Equal(9L, result["source_version"].AsInt64);
        Assert.True(result["is_deleted"].AsBoolean);
        Assert.Equal(deleted["source_updated_at"], result["source_updated_at"]);
    }

    [Fact]
    public void Unlock_before_account_creation_is_retained_when_the_identity_arrives()
    {
        var early = StatisticsProjection.Apply(null,
            new AchievementUnlockedEvent(8001, 42, Account().CreatedAt));
        var result = StatisticsProjection.Apply(early, new AccountCreatedEvent(Account()));

        Assert.Equal("source-identity", result["user_whoami"].AsString);
        Assert.Equal(new BsonArray { new BsonInt64(42) }, result["progress"]["achievements_got"]);
        Assert.Equal(1L, result["source_version"].AsInt64);
    }

    internal static AccountSnapshot Account() => new(8001, "event_player", "event@example.test",
        "source-identity", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), 1, false);

    internal static BsonDocument LegacyStatistics() => new()
    {
        { "_id", ObjectId.GenerateNewId() }, { "user_id", new BsonInt64(8001) },
        { "user_whoami", "legacy-identity" }, { "updated_at", Account().CreatedAt.UtcDateTime },
        { "totals", new BsonDocument { { "times_killed", 4 }, { "times_won", 2 }, { "time_played", new BsonInt64(900) } } },
        { "progress", new BsonDocument { { "achievements_got", new BsonArray { "First Steps" } }, { "rooms_visited", 8 } } },
        { "enemies", new BsonDocument("enemies_killed", 12) }
    };
}
