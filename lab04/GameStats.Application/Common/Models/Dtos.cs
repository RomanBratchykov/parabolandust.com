namespace GameStats.Application.Common.Models;

public record RunDto(
    string Id, string RunId, long UserId, string PlayerName,
    DateTime StartedAt, DateTime? EndedAt, string GameVersion, bool? Successful,
    int EnemiesKilled, int RoomsVisited, int TimePlayed, int Version);

public record LeaderboardEntryDto(
    long UserId, string UserWhoami, int TimesWon, int TimesKilled);

public record EventCountDto(string EventType, int Count);