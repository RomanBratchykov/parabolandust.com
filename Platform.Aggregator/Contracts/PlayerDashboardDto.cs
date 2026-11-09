namespace Platform.Aggregator.Contracts;

public sealed record PlayerDashboardDto(
    DashboardPlayerDto Player,
    IReadOnlyList<DashboardSubscriptionDto>? Subscriptions,
    DashboardStatisticsDto? Statistics,
    IReadOnlyList<string> Warnings);

public sealed record DashboardPlayerDto(long UserId, string UserName, string? PhotoUrl);

public sealed record DashboardSubscriptionDto(
    long Id,
    long NewsletterId,
    string NewsletterTitle,
    string Status,
    DateTimeOffset ConsentAt);

public sealed record DashboardStatisticsDto(
    int TimesKilled,
    int TimesWon,
    long TimePlayed,
    DateTimeOffset UpdatedAt);
