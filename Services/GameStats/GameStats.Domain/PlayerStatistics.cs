namespace GameStats.Domain;

public sealed record PlayerStatistics(
    long UserId,
    DateTimeOffset UpdatedAt,
    PlayerTotals Totals);

public sealed record PlayerTotals(
    int TimesKilled,
    int TimesWon,
    long TimePlayed);
