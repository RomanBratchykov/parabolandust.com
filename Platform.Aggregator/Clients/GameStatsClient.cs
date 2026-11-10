using GameStats.Grpc;

namespace Platform.Aggregator.Clients;

public sealed class GameStatsClient(
    GameStatsReader.GameStatsReaderClient client, GrpcCall calls)
{
    public async Task<StatisticsResponse?> GetStatisticsAsync(long userId, CancellationToken cancellationToken)
    {
        var reply = await calls.InvokeAsync("gamestats-api", "gamestats.v1.GameStatsReader/GetStatistics",
            (deadline, token) => client.GetStatisticsAsync(new UserRequest { UserId = userId },
                deadline: deadline, cancellationToken: token), cancellationToken);
        if (reply is null)
            return null;
        if (reply.Totals is null || reply.UpdatedAt is null)
            throw new InvalidDataException("Statistics totals or updated timestamp are missing.");
        try
        {
            return new StatisticsResponse(reply.UserId, reply.UpdatedAt.ToDateTimeOffset(),
                new StatisticsTotals(reply.Totals.Killed, reply.Totals.Won, reply.Totals.TimePlayed));
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The statistics updated timestamp is invalid.", exception);
        }
    }
}

public sealed record StatisticsResponse(long UserId, DateTimeOffset UpdatedAt, StatisticsTotals Totals);
public sealed record StatisticsTotals(int TimesKilled, int TimesWon, long TimePlayed);
