using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newsletters.Bll.Events;
using Newsletters.Dal.Data;
using Newsletters.Dal.Events;
using Platform.Caching;
using Platform.Messaging;
using Platform.Messaging.Events;
using Xunit;

namespace Newsletters.Tests;

public sealed class NewsletterAccountEventHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cache_generations_change_only_after_the_projection_commits(bool cancelAfterCommit)
    {
        await using var provider = CreateProvider();
        var states = provider.GetRequiredService<CacheGroupState>();
        var newsletter = states.Capture("newsletters");
        var dashboard = states.Capture("dashboard");
        var accounts = states.Capture("accounts");
        var projection = new ControlledProjection();
        var handler = new AccountEventHandler(projection, provider.GetRequiredService<CacheInvalidator>());
        using var cancellation = new CancellationTokenSource();

        var handling = handler.HandleAsync(new AccountProfileChangedEvent(Snapshot()), cancellation.Token);
        Assert.False(newsletter.Token.IsCancellationRequested);
        Assert.False(dashboard.Token.IsCancellationRequested);
        if (cancelAfterCommit) cancellation.Cancel();
        projection.Completion.SetResult();
        await handling;

        Assert.True(newsletter.Token.IsCancellationRequested);
        Assert.True(dashboard.Token.IsCancellationRequested);
        Assert.False(accounts.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Failed_projection_does_not_invalidate_caches()
    {
        await using var provider = CreateProvider();
        var snapshot = provider.GetRequiredService<CacheGroupState>().Capture("newsletters");
        var projection = new ControlledProjection();
        var handler = new AccountEventHandler(projection, provider.GetRequiredService<CacheInvalidator>());
        var handling = handler.HandleAsync(new AccountCreatedEvent(Snapshot()), default);
        projection.Completion.SetException(new InvalidOperationException("Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handling);
        Assert.False(snapshot.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Invalid_and_unrelated_events_are_permanent_failures_before_database_access()
    {
        await using var provider = CreateProvider();
        await using var context = new NewslettersDbContext(new DbContextOptionsBuilder<NewslettersDbContext>()
            .UseNpgsql("Host=localhost;Port=1;Database=unused;Username=unused;Timeout=1").Options);
        var handler = new AccountEventHandler(new AccountEventProjection(context),
            provider.GetRequiredService<CacheInvalidator>());
        IntegrationEvent[] invalid =
        [
            new AccountCreatedEvent(Snapshot()) { EventId = Guid.Empty },
            new AccountCreatedEvent(Snapshot()) { SchemaVersion = 2 },
            new AccountCreatedEvent(Snapshot() with { SourceVersion = 0 }),
            new AccountCreatedEvent(Snapshot() with { UserName = new string('x', 51) }),
            new AccountCreatedEvent(Snapshot() with { UserEmail = new string('x', 255) }),
            new AccountDeletedEvent(Snapshot()),
            new AchievementUnlockedEvent(1001, 1, DateTimeOffset.UtcNow)
        ];
        foreach (var value in invalid)
            await Assert.ThrowsAsync<PermanentEventException>(() => handler.HandleAsync(value, default));
    }

    internal static AccountSnapshot Snapshot(long userId = 8001, long version = 1, bool deleted = false) =>
        new(userId, "event_player", "event@example.test", "event-identity",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), version, deleted);

    internal static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddPlatformCaching();
        return services.BuildServiceProvider();
    }

    private sealed class ControlledProjection : IAccountEventProjection
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task ApplyAsync(Guid eventId, AccountSnapshot account, CancellationToken cancellationToken) =>
            Completion.Task;
    }
}
