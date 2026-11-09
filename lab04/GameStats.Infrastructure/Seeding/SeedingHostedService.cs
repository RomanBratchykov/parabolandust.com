using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameStats.Infrastructure.Seeding;

public class SeedingHostedService(
    IEnumerable<IDataSeeder> seeders, ILogger<SeedingHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            foreach (var seeder in seeders)
                await seeder.SeedAsync(ct);

            logger.LogInformation("Database seeding finished");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Database seeding failed (is MongoDB running?)");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}