using MongoDB.Bson;
using Platform.Messaging.Events;

namespace GameStats.Infrastructure.Events;

public static class StatisticsProjection
{
    public static long UserId(IntegrationEvent value) => value switch
    {
        AccountCreatedEvent created => created.Account.UserId,
        AccountProfileChangedEvent changed => changed.Account.UserId,
        AccountDeletedEvent deleted => deleted.Account.UserId,
        AccountIdentityRotatedEvent rotated => rotated.Account.UserId,
        AchievementUnlockedEvent unlocked => unlocked.UserId,
        _ => throw new ArgumentException("Unsupported statistics event.", nameof(value))
    };

    public static BsonDocument Apply(BsonDocument? current, IntegrationEvent value)
    {
        var document = current?.DeepClone().AsBsonDocument ?? new BsonDocument
        {
            { "user_id", new BsonInt64(UserId(value)) }, { "user_whoami", "" },
            { "updated_at", value.OccurredAt.UtcDateTime }, { "source_version", new BsonInt64(0) },
            { "is_deleted", false },
            { "totals", new BsonDocument { { "times_killed", 0 }, { "times_won", 0 }, { "time_played", new BsonInt64(0) } } }
        };
        if (value is AchievementUnlockedEvent unlocked)
            ApplyAchievement(document, unlocked);
        else
        {
            var account = value switch
            {
                AccountCreatedEvent created => created.Account,
                AccountProfileChangedEvent changed => changed.Account,
                AccountDeletedEvent deleted => deleted.Account,
                AccountIdentityRotatedEvent rotated => rotated.Account,
                _ => throw new ArgumentException("Unsupported statistics event.", nameof(value))
            };
            if (account.SourceVersion <= document.GetValue("source_version", new BsonInt64(0)).ToInt64())
                return current!;
            document["user_whoami"] = account.UserWhoAmI;
            document["source_version"] = new BsonInt64(account.SourceVersion);
            document["source_updated_at"] = account.SourceUpdatedAt.UtcDateTime;
            document["is_deleted"] = account.IsDeleted;
        }
        // An old unlock must not move the statistics' last-change time backwards.
        if (value.OccurredAt.UtcDateTime > document["updated_at"].ToUniversalTime())
            document["updated_at"] = value.OccurredAt.UtcDateTime;
        return document;
    }

    private static void ApplyAchievement(BsonDocument document, AchievementUnlockedEvent value)
    {
        if (!document.Contains("progress")) document["progress"] = new BsonDocument();
        var progress = document["progress"].AsBsonDocument;
        if (!progress.Contains("achievements_got")) progress["achievements_got"] = new BsonArray();
        var achievements = progress["achievements_got"].AsBsonArray;
        var achievementId = new BsonInt64(value.AchievementId);
        if (!achievements.Contains(achievementId)) achievements.Add(achievementId);
    }
}
