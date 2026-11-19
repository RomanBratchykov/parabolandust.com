using GameStats.Application.Events;
using Microsoft.Extensions.DependencyInjection;
using Platform.Caching;
using Platform.Messaging;
using Platform.Messaging.Events;
using Xunit;

namespace GameStats.Tests;

public sealed class AccountEventHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cache_invalidation_waits_for_commit_and_survives_post_commit_cancellation(bool cancel)
    {
        await using var provider = CreateProvider();
        var states = provider.GetRequiredService<CacheGroupState>();
        var statistics = states.Capture("gamestats");
        var dashboard = states.Capture("dashboard");
        var unrelated = states.Capture("newsletters");
        var projection = new ControlledProjection();
        var handler = new AccountEventHandler(projection, provider.GetRequiredService<CacheInvalidator>());
        using var cancellation = new CancellationTokenSource();
        var handling = handler.HandleAsync(new AccountCreatedEvent(StatisticsProjectionTests.Account()), cancellation.Token);

        Assert.False(statistics.Token.IsCancellationRequested);
        Assert.False(dashboard.Token.IsCancellationRequested);
        if (cancel) cancellation.Cancel();
        projection.Completion.SetResult();
        await handling;

        Assert.True(statistics.Token.IsCancellationRequested);
        Assert.True(dashboard.Token.IsCancellationRequested);
        Assert.False(unrelated.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Projection_failure_preserves_cache_generations()
    {
        await using var provider = CreateProvider();
        var state = provider.GetRequiredService<CacheGroupState>().Capture("gamestats");
        var projection = new ControlledProjection();
        var handler = new AccountEventHandler(projection, provider.GetRequiredService<CacheInvalidator>());
        var handling = handler.HandleAsync(new AchievementUnlockedEvent(8001, 42, DateTimeOffset.UtcNow), default);
        projection.Completion.SetException(new InvalidOperationException("Transaction aborted"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handling);
        Assert.False(state.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Invalid_events_are_rejected_permanently_before_storage()
    {
        await using var provider = CreateProvider();
        var projection = new ControlledProjection();
        projection.Completion.SetException(new InvalidOperationException("Storage must not be used"));
        var handler = new AccountEventHandler(projection, provider.GetRequiredService<CacheInvalidator>());
        IntegrationEvent[] invalid =
        [
            new AccountCreatedEvent(StatisticsProjectionTests.Account()) { EventId = Guid.Empty },
            new AccountCreatedEvent(StatisticsProjectionTests.Account()) { SchemaVersion = 2 },
            new AccountCreatedEvent(StatisticsProjectionTests.Account() with { SourceVersion = 0 }),
            new AccountDeletedEvent(StatisticsProjectionTests.Account()),
            new AchievementUnlockedEvent(8001, 0, DateTimeOffset.UtcNow)
        ];
        foreach (var value in invalid)
            await Assert.ThrowsAsync<PermanentEventException>(() => handler.HandleAsync(value, default));
    }

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
        public Task ApplyAsync(IntegrationEvent value, CancellationToken cancellationToken) => Completion.Task;
    }
}
