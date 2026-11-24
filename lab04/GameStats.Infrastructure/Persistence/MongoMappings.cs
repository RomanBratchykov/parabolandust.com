using GameStats.Domain.Common;
using GameStats.Domain.Entities;
using GameStats.Domain.ValueObjects;
using GameStats.Infrastructure.Persistence.Serializers;
using MongoDB.Bson.Serialization;

namespace GameStats.Infrastructure.Persistence;

public static class MongoMappings
{
    private static readonly object Sync = new();
    private static bool _registered;

    // викликається один раз при старті, до першого звернення до БД
    public static void Register()
    {
        lock (Sync)
        {
            if (_registered) return;
            _registered = true;

            BsonSerializer.RegisterSerializer(new RunStatsSerializer());

            // спільні поля всіх сутностей
            BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
            {
                cm.MapIdProperty(e => e.Id);
                cm.MapProperty(e => e.CreatedAt).SetElementName("created_at");
                cm.MapProperty(e => e.UpdatedAt).SetElementName("updated_at").SetIgnoreIfNull(true);
                cm.MapProperty(e => e.Version).SetElementName("version");
                cm.SetIgnoreExtraElements(true);
            });

            // Value Objects: незмінні, створюються через конструктор
            BsonClassMap.RegisterClassMap<PlayerSnapshot>(cm =>
            {
                cm.MapProperty(s => s.UserName).SetElementName("user_name");
                cm.MapProperty(s => s.UserWhoami).SetElementName("user_whoami");
                cm.MapCreator(s => new PlayerSnapshot(s.UserName, s.UserWhoami));
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<PlayerTotals>(cm =>
            {
                cm.MapProperty(t => t.TimesKilled).SetElementName("times_killed");
                cm.MapProperty(t => t.TimesWon).SetElementName("times_won");
                cm.MapProperty(t => t.TimePlayed).SetElementName("time_played");
                cm.MapCreator(t => new PlayerTotals(t.TimesKilled, t.TimesWon, t.TimePlayed));
                cm.SetIgnoreExtraElements(true);
            });

            // Сутності
            BsonClassMap.RegisterClassMap<Run>(cm =>
            {
                cm.MapProperty(r => r.RunId).SetElementName("run_id");
                cm.MapProperty(r => r.UserId).SetElementName("user_id");
                cm.MapProperty(r => r.StartedAt).SetElementName("started_at");
                cm.MapProperty(r => r.EndedAt).SetElementName("ended_at");
                cm.MapProperty(r => r.GameVersion).SetElementName("game_version");
                cm.MapProperty(r => r.Successful).SetElementName("successful").SetIgnoreIfNull(true);
                cm.MapProperty(r => r.CurrentStats).SetElementName("current_stats");
                cm.MapProperty(r => r.PlayerSnapshot).SetElementName("player_snapshot").SetIgnoreIfNull(true);
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<GameEvent>(cm =>
            {
                cm.MapProperty(e => e.EventId).SetElementName("event_id");
                cm.MapProperty(e => e.EventType).SetElementName("event_type");
                cm.MapProperty(e => e.UserId).SetElementName("user_id");
                cm.MapProperty(e => e.RunId).SetElementName("run_id");
                cm.MapProperty(e => e.OccurredAt).SetElementName("occurred_at");
                cm.MapProperty(e => e.Payload).SetElementName("payload");
                cm.SetIgnoreExtraElements(true);   // напр. client_context у деяких подій
            });

            BsonClassMap.RegisterClassMap<StatsUser>(cm =>
            {
                cm.MapProperty(s => s.UserId).SetElementName("user_id");
                cm.MapProperty(s => s.UserWhoami).SetElementName("user_whoami");
                cm.MapProperty(s => s.Totals).SetElementName("totals");
                cm.SetIgnoreExtraElements(true);
            });
        }
    }
}