using System.Text.Json;
using System.Text.Json.Nodes;
using Platform.Messaging.Events;
using Xunit;

namespace Platform.Messaging.Tests;

public sealed class EventSerializationTests
{
    [Fact]
    public void Snapshot_event_round_trip_preserves_metadata_and_source_ordering_without_authentication_fields()
    {
        var original = new AccountProfileChangedEvent(Snapshot()) { CorrelationId = "request-123" };
        var payload = EventJson.Serialize(original);
        var copy = EventJson.Deserialize(nameof(AccountProfileChangedEvent), payload);
        var json = JsonNode.Parse(payload)!;
        Assert.Equal(original, copy);
        Assert.Equal(7, json["account"]!["sourceVersion"]!.GetValue<long>());
        Assert.Equal("request-123", json["correlationId"]!.GetValue<string>());
        Assert.Null(json["account"]!["passwordHash"]);
        Assert.Null(json["EventId"]);
    }

    [Theory]
    [InlineData("eventId")]
    [InlineData("occurredAt")]
    [InlineData("schemaVersion")]
    [InlineData("account")]
    public void Missing_required_envelope_fields_are_rejected(string property)
    {
        var json = JsonNode.Parse(EventJson.Serialize(new AccountCreatedEvent(Snapshot())))!.AsObject();
        json.Remove(property);
        Assert.Throws<JsonException>(() => EventJson.Deserialize(nameof(AccountCreatedEvent), JsonSerializer.SerializeToUtf8Bytes(json)));
    }

    [Theory]
    [InlineData("schemaVersion", "2")]
    [InlineData("eventId", "\"00000000-0000-0000-0000-000000000000\"")]
    public void Incompatible_or_invalid_metadata_is_rejected(string property, string value)
    {
        var json = JsonNode.Parse(EventJson.Serialize(new AccountCreatedEvent(Snapshot())))!;
        json[property] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => EventJson.Deserialize(nameof(AccountCreatedEvent), JsonSerializer.SerializeToUtf8Bytes(json)));
    }

    [Fact]
    public void Unknown_type_and_empty_payload_are_permanent_format_errors()
    {
        Assert.Throws<JsonException>(() => EventJson.Deserialize("UnknownEvent", "{}"u8.ToArray()));
        Assert.Throws<JsonException>(() => EventJson.Deserialize(nameof(AccountCreatedEvent), "null"u8.ToArray()));
    }

    [Fact]
    public void Invalid_snapshot_is_rejected_before_publishing()
    {
        var invalid = new AccountCreatedEvent(Snapshot() with { SourceVersion = 0 });
        Assert.Throws<JsonException>(() => EventJson.Serialize(invalid));
    }

    internal static AccountSnapshot Snapshot() => new(1001, "alex", "alex@example.test", "identity-1",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.Zero), 7, false);

    [Fact]
    public void All_supported_events_round_trip_with_large_identifiers()
    {
        var account = Snapshot() with { UserId = (long)int.MaxValue + 1 };
        IntegrationEvent[] events = [new AccountCreatedEvent(account), new AccountProfileChangedEvent(account),
            new AccountDeletedEvent(account with { IsDeleted = true }), new AccountIdentityRotatedEvent(account),
            new AchievementUnlockedEvent(account.UserId, 42, account.SourceUpdatedAt)];
        foreach (var value in events)
            Assert.Equal(value, EventJson.Deserialize(value.GetType().Name, EventJson.Serialize(value)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{broken")]
    [InlineData("[]")]
    public void Malformed_envelopes_are_rejected(string payload) =>
        Assert.Throws<JsonException>(() => EventJson.Deserialize(nameof(AccountCreatedEvent), System.Text.Encoding.UTF8.GetBytes(payload)));

    [Theory]
    [InlineData("userId", "0")]
    [InlineData("userName", "null")]
    [InlineData("userEmail", "\" \"")]
    [InlineData("userWhoAmI", "\"\"")]
    [InlineData("sourceUpdatedAt", "\"0001-01-01T00:00:00+00:00\"")]
    public void Invalid_incoming_snapshots_are_rejected(string property, string value)
    {
        var json = JsonNode.Parse(EventJson.Serialize(new AccountCreatedEvent(Snapshot())))!;
        json["account"]![property] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => EventJson.Deserialize(nameof(AccountCreatedEvent), JsonSerializer.SerializeToUtf8Bytes(json)));
    }

    [Fact]
    public void Missing_snapshot_fields_and_invalid_achievement_are_rejected()
    {
        var json = JsonNode.Parse(EventJson.Serialize(new AccountCreatedEvent(Snapshot())))!;
        json["account"]!.AsObject().Remove("sourceVersion");
        Assert.Throws<JsonException>(() => EventJson.Deserialize(nameof(AccountCreatedEvent), JsonSerializer.SerializeToUtf8Bytes(json)));
        Assert.Throws<JsonException>(() => EventJson.Serialize(new AchievementUnlockedEvent(1, 0, DateTimeOffset.UtcNow)));
        Assert.Throws<JsonException>(() => EventJson.Serialize(new AccountDeletedEvent(Snapshot())));
    }
}
