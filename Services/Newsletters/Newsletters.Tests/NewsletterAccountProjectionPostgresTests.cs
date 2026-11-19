using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newsletters.Bll.Events;
using Newsletters.Dal.Data;
using Newsletters.Dal.Events;
using Newsletters.Domain.Models;
using Npgsql;
using Platform.Caching;
using Platform.Messaging.Events;
using Xunit;

namespace Newsletters.Tests;

[Collection("Lab 3 database")]
public sealed class NewsletterAccountProjectionPostgresTests
{
    [PostgresFact]
    public async Task Duplicate_and_stale_events_preserve_the_latest_copy_and_subscription_snapshot()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await using var provider = NewsletterAccountEventHandlerTests.CreateProvider();
        var invalidator = provider.GetRequiredService<CacheInvalidator>();
        var handler = new AccountEventHandler(new AccountEventProjection(context), invalidator);
        var userId = DateTimeOffset.UtcNow.Ticks;
        var account = NewsletterAccountEventHandlerTests.Snapshot(userId);
        var created = new AccountCreatedEvent(account);
        var updated = new AccountProfileChangedEvent(account with
        {
            UserName = "updated_player", UserEmail = "updated@example.test", SourceVersion = 2,
            SourceUpdatedAt = account.SourceUpdatedAt.AddHours(1)
        });
        var deleted = new AccountDeletedEvent(updated.Account with { SourceVersion = 3, IsDeleted = true });
        var stale = new AccountCreatedEvent(account);
        Guid[] eventIds = [created.EventId, updated.EventId, deleted.EventId, stale.EventId];
        var newsletter = new Newsletter(userId, "Event test newsletter", "Snapshot preservation");
        try
        {
            await handler.HandleAsync(created, default);
            context.Newsletters.Add(newsletter);
            var subscription = new Subscription(userId, newsletter.Id, "event@example.test");
            context.Subscriptions.Add(subscription);
            await context.SaveChangesAsync();
            var before = await context.Subscriptions.AsNoTracking().SingleAsync(value => value.UserId == userId);

            await handler.HandleAsync(updated, default);
            var copy = await context.UsersCopy.AsNoTracking().SingleAsync(value => value.Id == userId);
            Assert.Equal("updated_player", copy.UserName);
            Assert.Equal("updated@example.test", copy.Email);
            Assert.Equal(account.CreatedAt, copy.CreatedAt);
            Assert.Equal(updated.Account.SourceUpdatedAt, copy.SourceUpdatedAt);
            Assert.Equal(2, copy.SourceVersion);
            Assert.Equal(2, copy.RowVersion);

            // Redelivery must also repair a cache generation missed after the original commit.
            var generation = provider.GetRequiredService<CacheGroupState>().Capture("dashboard");
            await handler.HandleAsync(updated, default);
            Assert.True(generation.Token.IsCancellationRequested);
            await handler.HandleAsync(deleted, default);
            await handler.HandleAsync(stale, default);
            await handler.HandleAsync(new AccountProfileChangedEvent(updated.Account) { EventId = stale.EventId }, default);
            copy = await context.UsersCopy.AsNoTracking().SingleAsync(value => value.Id == userId);
            Assert.True(copy.IsDeleted);
            Assert.Equal(3, copy.SourceVersion);
            Assert.Equal(3, copy.RowVersion);
            Assert.Equal("updated@example.test", copy.Email);
            Assert.Equal(4, await context.ProcessedEvents.CountAsync(value => eventIds.Contains(value.EventId)));
            var after = await context.Subscriptions.AsNoTracking().SingleAsync(value => value.UserId == userId);
            Assert.Equal(before.UserEmailSnapshot, after.UserEmailSnapshot);
            Assert.Equal(before.ConsentAt, after.ConsentAt);
            Assert.Equal(before.Status, after.Status);
            Assert.Equal(before.UnsubscribeToken, after.UnsubscribeToken);
            Assert.Equal(before.RowVersion, after.RowVersion);
            Assert.Equal(before.UpdatedAt, after.UpdatedAt);
            Assert.Equal(before.IsDeleted, after.IsDeleted);
        }
        finally
        {
            await context.Subscriptions.Where(value => value.UserId == userId).ExecuteDeleteAsync();
            await context.Newsletters.Where(value => value.Id == userId).ExecuteDeleteAsync();
            await context.UsersCopy.Where(value => value.Id == userId).ExecuteDeleteAsync();
            await context.ProcessedEvents.Where(value => eventIds.Contains(value.EventId)).ExecuteDeleteAsync();
        }
    }

    [PostgresFact]
    public async Task Delete_before_create_and_equal_versions_cannot_resurrect_a_copy()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await using var provider = NewsletterAccountEventHandlerTests.CreateProvider();
        var handler = new AccountEventHandler(new AccountEventProjection(context),
            provider.GetRequiredService<CacheInvalidator>());
        var account = NewsletterAccountEventHandlerTests.Snapshot(DateTimeOffset.UtcNow.Ticks, 5, true);
        IntegrationEvent[] events =
        [
            new AccountDeletedEvent(account),
            new AccountCreatedEvent(account with { SourceVersion = 1, IsDeleted = false }),
            new AccountProfileChangedEvent(account with
            {
                SourceVersion = 4, IsDeleted = false, SourceUpdatedAt = account.SourceUpdatedAt.AddDays(1)
            }),
            new AccountIdentityRotatedEvent(account with { SourceVersion = 5, IsDeleted = false })
        ];
        var eventIds = events.Select(value => value.EventId).ToArray();
        try
        {
            foreach (var value in events) await handler.HandleAsync(value, default);
            var copy = await context.UsersCopy.AsNoTracking().SingleAsync(value => value.Id == account.UserId);
            Assert.True(copy.IsDeleted);
            Assert.Equal(5, copy.SourceVersion);
            Assert.Equal(1, copy.RowVersion);
            Assert.Equal(account.SourceUpdatedAt, copy.SourceUpdatedAt);
            Assert.Equal(4, await context.ProcessedEvents.CountAsync(value => eventIds.Contains(value.EventId)));
        }
        finally
        {
            await context.UsersCopy.Where(value => value.Id == account.UserId).ExecuteDeleteAsync();
            await context.ProcessedEvents.Where(value => eventIds.Contains(value.EventId)).ExecuteDeleteAsync();
        }
    }

    [PostgresFact]
    public async Task Failed_projection_rolls_back_the_inbox_so_the_same_event_can_be_retried()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var projection = new AccountEventProjection(context);
        var eventId = Guid.NewGuid();
        var account = NewsletterAccountEventHandlerTests.Snapshot(DateTimeOffset.UtcNow.Ticks);
        try
        {
            await Assert.ThrowsAsync<PostgresException>(() =>
                projection.ApplyAsync(eventId, account with { SourceVersion = -1 }, default));
            Assert.False(await context.ProcessedEvents.AnyAsync(value => value.EventId == eventId));
            Assert.False(await context.UsersCopy.AnyAsync(value => value.Id == account.UserId));

            await projection.ApplyAsync(eventId, account, default);
            Assert.True(await context.ProcessedEvents.AnyAsync(value => value.EventId == eventId));
            Assert.Equal(1, (await context.UsersCopy.AsNoTracking().SingleAsync(value => value.Id == account.UserId)).SourceVersion);
        }
        finally
        {
            await context.UsersCopy.Where(value => value.Id == account.UserId).ExecuteDeleteAsync();
            await context.ProcessedEvents.Where(value => value.EventId == eventId).ExecuteDeleteAsync();
        }
    }

    private static NewslettersDbContext CreateContext() => new(new DbContextOptionsBuilder<NewslettersDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("NEWSLETTERS_TEST_CONNECTION")).Options);
}
