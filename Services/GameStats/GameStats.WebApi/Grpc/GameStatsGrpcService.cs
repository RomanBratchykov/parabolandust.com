using GameStats.Application;
using GameStats.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Platform.ServiceDefaults;

namespace GameStats.WebApi.Grpc;

public sealed class GameStatsGrpcService(
    IPlayerStatisticsService service, ILogger<GameStatsGrpcService> logger, BusinessMetrics metrics)
    : GameStatsReader.GameStatsReaderBase
{
    public override async Task<StatisticsReply> GetStatistics(UserRequest request, ServerCallContext context)
    {
        using var operation = metrics.Start("statistics.read");
        if (request.UserId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "User ID must be positive."));

        try
        {
            var statistics = await service.GetByUserIdAsync(request.UserId, context.CancellationToken);
            if (statistics is null)
                throw new RpcException(new Status(StatusCode.NotFound, "Player statistics not found."));

            var reply = new StatisticsReply
            {
                UserId = statistics.UserId,
                UpdatedAt = Timestamp.FromDateTimeOffset(statistics.UpdatedAt),
                Totals = new TotalsReply
                {
                    Killed = statistics.Totals.TimesKilled,
                    Won = statistics.Totals.TimesWon,
                    TimePlayed = statistics.Totals.TimePlayed
                }
            };
            operation.Succeed();
            return reply;
        }
        catch (RpcException) { throw; }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request canceled."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to retrieve statistics for user {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal, "Unable to retrieve player statistics."));
        }
    }
}
