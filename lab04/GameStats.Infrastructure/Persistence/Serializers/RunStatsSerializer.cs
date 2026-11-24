using GameStats.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GameStats.Infrastructure.Persistence.Serializers;

// Власний серіалізатор: у документах із Лаб. 01 частини полів current_stats може не бути
// (гнучка схема), тому відсутні значення читаються як 0.
public sealed class RunStatsSerializer : SerializerBase<RunStats>
{
    public override RunStats Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var doc = BsonSerializer.Deserialize<BsonDocument>(context.Reader);
        return new RunStats(
            doc.GetValue("enemies_killed", 0).ToInt32(),
            doc.GetValue("rooms_visited", 0).ToInt32(),
            doc.GetValue("time_played", 0).ToInt32());
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, RunStats value)
    {
        var writer = context.Writer;
        writer.WriteStartDocument();
        writer.WriteName("enemies_killed");
        writer.WriteInt32(value.EnemiesKilled);
        writer.WriteName("rooms_visited");
        writer.WriteInt32(value.RoomsVisited);
        writer.WriteName("time_played");
        writer.WriteInt32(value.TimePlayed);
        writer.WriteEndDocument();
    }
}