using Microsoft.Extensions.Options;

namespace Platform.Aggregator.Clients;

public sealed class GameStatsClient(
    HttpClient http, IOptions<DownstreamOptions> options, ILogger<GameStatsClient> logger)
{
    public Task<StatisticsResponse?> GetStatisticsAsync(long userId, CancellationToken cancellationToken) =>
        DownstreamJson.GetAsync<StatisticsResponse>(http, options.Value, logger,
            "gamestats-api", $"/api/stats/users/{userId}", cancellationToken);
}

public sealed record StatisticsResponse(long UserId, DateTimeOffset UpdatedAt, StatisticsTotals Totals);
public sealed record StatisticsTotals(int TimesKilled, int TimesWon, long TimePlayed);
