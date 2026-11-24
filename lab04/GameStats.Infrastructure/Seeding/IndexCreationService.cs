using GameStats.Domain.Entities;
using GameStats.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace GameStats.Infrastructure.Seeding;

public class IndexCreationService(
    MongoDbContext context, ILogger<IndexCreationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var runKeys = Builders<Run>.IndexKeys;
            await context.Runs.Indexes.CreateManyAsync(new[]
            {
                // дублікати run_id відхиляє сама БД -> 409
                new CreateIndexModel<Run>(runKeys.Ascending(r => r.RunId),
                    new CreateIndexOptions { Unique = true, Name = "ux_runs_run_id" }),
                // історія забігів гравця
                new CreateIndexModel<Run>(runKeys.Ascending(r => r.UserId).Descending(r => r.StartedAt),
                    new CreateIndexOptions { Name = "ix_runs_user_history" }),
                // загальний список забігів (сортування за часом)
                new CreateIndexModel<Run>(runKeys.Descending(r => r.StartedAt),
                    new CreateIndexOptions { Name = "ix_runs_started_at" })
            }, ct);

            var eventKeys = Builders<GameEvent>.IndexKeys;
            await context.GameEvents.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<GameEvent>(eventKeys.Ascending(e => e.EventId),
                    new CreateIndexOptions { Unique = true, Name = "ux_game_events_event_id" }),
                new CreateIndexModel<GameEvent>(eventKeys.Ascending(e => e.UserId).Descending(e => e.OccurredAt),
                    new CreateIndexOptions { Name = "ix_events_user_time" }),
                // події певного типу за часом
                new CreateIndexModel<GameEvent>(eventKeys.Ascending(e => e.EventType).Descending(e => e.OccurredAt),
                    new CreateIndexOptions { Name = "ix_events_type_time" })
            }, ct);

            var statsKeys = Builders<StatsUser>.IndexKeys;
            await context.StatsUsers.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<StatsUser>(statsKeys.Ascending(s => s.UserId),
                    new CreateIndexOptions { Unique = true, Name = "ux_stats_user_id" }),
                // лідерборд: сортування за перемогами та смертями
                new CreateIndexModel<StatsUser>(
                    statsKeys.Descending("totals.times_won").Ascending("totals.times_killed"),
                    new CreateIndexOptions { Name = "ix_stats_leaderboard" })
            }, ct);

            logger.LogInformation("MongoDB indexes ensured");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Index creation failed (is MongoDB running?)");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}