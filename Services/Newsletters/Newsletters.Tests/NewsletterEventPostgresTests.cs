using Microsoft.EntityFrameworkCore;
using Newsletters.Dal.Data;
using Newsletters.Domain.Models;
using Npgsql;
using Xunit;

namespace Newsletters.Tests;

[Collection("Lab 3 database")]
public sealed class NewsletterEventPostgresTests
{
    [PostgresFact]
    public async Task Schema_defaults_preserve_legacy_copies_and_inbox_entries_are_unique_and_transactional()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var userId = DateTimeOffset.UtcNow.Ticks;
        var sourceUpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO newsletter.users_copy (user_id, user_name, user_email, source_updated_at)
            VALUES ({userId}, 'legacy_copy', 'legacy@example.test', {sourceUpdatedAt})
            """);
        var user = await context.UsersCopy.SingleAsync(value => value.Id == userId);
        Assert.Equal(0, user.SourceVersion);
        Assert.Equal(1, user.RowVersion);
        Assert.Equal(sourceUpdatedAt, user.SourceUpdatedAt);
        Assert.Equal("legacy@example.test", user.Email);

        var processed = new ProcessedEvent(Guid.NewGuid());
        context.ProcessedEvents.Add(processed);
        await context.SaveChangesAsync();
        Assert.NotEqual(default, processed.ProcessedAt);
        Assert.Equal(TimeSpan.Zero, processed.ProcessedAt.Offset);
        var inserted = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO newsletter.processed_events (event_id) VALUES ({processed.EventId})
            ON CONFLICT (event_id) DO NOTHING
            """);
        Assert.Equal(0, inserted);
        Assert.Equal(1, await context.ProcessedEvents.CountAsync(value => value.EventId == processed.EventId));

        await transaction.RollbackAsync();
        context.ChangeTracker.Clear();
        Assert.False(await context.ProcessedEvents.AnyAsync(value => value.EventId == processed.EventId));
        Assert.False(await context.UsersCopy.AnyAsync(value => value.Id == userId));
    }

    [PostgresFact]
    public async Task Negative_source_versions_are_rejected_by_postgresql()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var userId = DateTimeOffset.UtcNow.Ticks;
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO newsletter.users_copy (user_id, user_name, source_updated_at, source_version)
            VALUES ({userId}, 'invalid_copy', CURRENT_TIMESTAMP, -1)
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_users_copy_source_version", error.ConstraintName);
    }

    private static NewslettersDbContext CreateContext() => new(new DbContextOptionsBuilder<NewslettersDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("NEWSLETTERS_TEST_CONNECTION")).Options);
}
