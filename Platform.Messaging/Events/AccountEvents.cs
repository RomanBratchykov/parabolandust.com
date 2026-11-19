namespace Platform.Messaging.Events;

// A complete source snapshot lets consumers apply events without calling Accounts.
// Authentication fields and historical subscription/run snapshots do not belong here.
public sealed record AccountSnapshot(long UserId, string UserName, string UserEmail,
    string UserWhoAmI, DateTimeOffset CreatedAt, DateTimeOffset SourceUpdatedAt,
    long SourceVersion, bool IsDeleted);

public sealed record AccountCreatedEvent(AccountSnapshot Account) : IntegrationEvent;
public sealed record AccountProfileChangedEvent(AccountSnapshot Account) : IntegrationEvent;
public sealed record AccountDeletedEvent(AccountSnapshot Account) : IntegrationEvent;
public sealed record AccountIdentityRotatedEvent(AccountSnapshot Account) : IntegrationEvent;

// Unlock membership is independent of profile ordering; EventId deduplicates delivery.
public sealed record AchievementUnlockedEvent(long UserId, long AchievementId,
    DateTimeOffset UnlockedAt) : IntegrationEvent;
