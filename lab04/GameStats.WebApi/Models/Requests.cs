using System.Text.Json;

namespace GameStats.WebApi.Models;

public record CreateRunRequest(
    string RunId, long UserId, string UserName, string UserWhoami, string GameVersion,
    int EnemiesKilled, int RoomsVisited, int TimePlayed);

public record UpdateRunRequest(
    int EnemiesKilled, int RoomsVisited, int TimePlayed, bool? Successful);

public record CreateGameEventRequest(
    string EventId, string EventType, long UserId, string? RunId, JsonElement Payload);