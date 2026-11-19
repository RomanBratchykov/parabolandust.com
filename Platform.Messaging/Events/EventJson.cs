using System.Text.Json;

namespace Platform.Messaging.Events;

public static class EventJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectRequiredConstructorParameters = true
    };

    public static byte[] Serialize(IntegrationEvent value)
    {
        Validate(value);
        return JsonSerializer.SerializeToUtf8Bytes(value, EventType(value.GetType().Name), Options);
    }

    public static IntegrationEvent Deserialize(string eventType, ReadOnlySpan<byte> payload)
    {
        var value = JsonSerializer.Deserialize(payload, EventType(eventType), Options) as IntegrationEvent;
        if (value is null)
            throw new JsonException("An event envelope is required.");
        Validate(value);
        return value;
    }

    private static Type EventType(string name) => name switch
    {
        nameof(AccountCreatedEvent) => typeof(AccountCreatedEvent),
        nameof(AccountProfileChangedEvent) => typeof(AccountProfileChangedEvent),
        nameof(AccountDeletedEvent) => typeof(AccountDeletedEvent),
        nameof(AccountIdentityRotatedEvent) => typeof(AccountIdentityRotatedEvent),
        nameof(AchievementUnlockedEvent) => typeof(AchievementUnlockedEvent),
        _ => throw new JsonException($"Unsupported event type: {name}.")
    };

    private static void Validate(IntegrationEvent value)
    {
        if (value.EventId == Guid.Empty || value.OccurredAt == default || value.SchemaVersion != 1)
            throw new JsonException("Invalid event metadata or unsupported schema version.");

        if (value is AchievementUnlockedEvent achievement)
        {
            if (achievement.UserId <= 0 || achievement.AchievementId <= 0 || achievement.UnlockedAt == default)
                throw new JsonException("Invalid achievement unlock.");
            return;
        }

        var account = value switch
        {
            AccountCreatedEvent created => created.Account,
            AccountProfileChangedEvent changed => changed.Account,
            AccountDeletedEvent deleted => deleted.Account,
            AccountIdentityRotatedEvent rotated => rotated.Account,
            _ => throw new JsonException("Unsupported event type.")
        };
        ValidateSnapshot(account);
        if (account.IsDeleted != (value is AccountDeletedEvent))
            throw new JsonException("Account deletion state does not match the event type.");
    }

    private static void ValidateSnapshot(AccountSnapshot? account)
    {
        if (account is null || account.UserId <= 0 || account.SourceVersion <= 0 ||
            string.IsNullOrWhiteSpace(account.UserName) || string.IsNullOrWhiteSpace(account.UserEmail) ||
            string.IsNullOrWhiteSpace(account.UserWhoAmI) || account.CreatedAt == default ||
            account.SourceUpdatedAt < account.CreatedAt)
            throw new JsonException("Invalid account snapshot or source ordering metadata.");
    }
}
