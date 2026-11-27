using Accounts.Bll;
using Accounts.Bll.Caching;
using Accounts.Bll.Contracts;
using Accounts.Bll.Events;
using Accounts.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Platform.Caching;
using Platform.Messaging.Events;
using Xunit;

namespace Accounts.Tests;

public sealed class IdentityAndAchievementTests
{
    [Fact]
    public async Task Identity_rotation_publishes_the_persisted_version_only_after_commit()
    {
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.CommitGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var handling = AccountEventTestDoubles.MutateAsync(fixture.Service(), "identity");
        Assert.Empty(fixture.Published);
        fixture.UnitOfWork.CommitGate.SetResult();
        await handling;

        var value = Assert.IsType<AccountIdentityRotatedEvent>(Assert.Single(fixture.Published));
        Assert.True(fixture.PublicationSawCommit);
        Assert.True(fixture.UnitOfWork.SnapshotReadInsideTransaction);
        Assert.Equal("rotated-persisted-identity", value.Account.UserWhoAmI);
        Assert.Equal(12, value.Account.SourceVersion);
        Assert.Equal(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), value.Account.SourceUpdatedAt);
        Assert.Equal("request-lab91", value.CorrelationId);
    }

    [Theory]
    [InlineData("write")]
    [InlineData("snapshot")]
    [InlineData("commit")]
    public async Task Failed_rotation_rolls_back_without_an_event(string failure)
    {
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.FailureAt = failure;
        await Assert.ThrowsAnyAsync<Exception>(() => AccountEventTestDoubles.MutateAsync(fixture.Service(), "identity"));
        Assert.Empty(fixture.Published);
        Assert.True(fixture.UnitOfWork.RolledBack);
        Assert.False(fixture.UnitOfWork.Committed);
    }

    [Fact]
    public async Task Stale_identity_version_is_a_conflict_without_publication()
    {
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.WriteAccepted = false;
        await Assert.ThrowsAsync<BusinessConflictException>(() => AccountEventTestDoubles.MutateAsync(fixture.Service(), "identity"));
        Assert.Empty(fixture.Published);
        Assert.True(fixture.UnitOfWork.RolledBack);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Grants_and_initial_unlocks_publish_the_database_unlock_time_after_commit(bool initial)
    {
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.CommitGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var handling = UnlockAsync(fixture.Service(), initial);
        Assert.Empty(fixture.Published);
        fixture.UnitOfWork.CommitGate.SetResult();
        await handling;

        Assert.True(fixture.PublicationSawCommit);
        Assert.Equal(initial ? 2 : 1, fixture.Published.Count);
        if (initial) Assert.IsType<AccountCreatedEvent>(fixture.Published[0]);
        var value = Assert.IsType<AchievementUnlockedEvent>(fixture.Published.Last());
        Assert.Equal(1234, value.UserId);
        Assert.Equal(42, value.AchievementId);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), value.UnlockedAt);
        Assert.Equal(value.UnlockedAt, value.OccurredAt);
        Assert.Equal("request-lab91", value.CorrelationId);
    }

    [Theory]
    [InlineData(false, "unlock")]
    [InlineData(false, "commit")]
    [InlineData(true, "unlock")]
    [InlineData(true, "commit")]
    public async Task Failed_unlocks_do_not_publish_any_events(bool initial, string failure)
    {
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.FailureAt = failure;
        await Assert.ThrowsAnyAsync<Exception>(() => UnlockAsync(fixture.Service(), initial));
        Assert.Empty(fixture.Published);
        Assert.True(fixture.UnitOfWork.RolledBack);
        Assert.False(fixture.UnitOfWork.Committed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Duplicate_unlocks_are_conflicts_and_publish_nothing(bool initial)
    {
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.WriteAccepted = false;
        await Assert.ThrowsAsync<BusinessConflictException>(() => UnlockAsync(fixture.Service(), initial));
        Assert.Empty(fixture.Published);
        Assert.False(fixture.UnitOfWork.Committed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_achievement_rolls_back_without_publication(bool initial)
    {
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.AchievementExists = false;
        await Assert.ThrowsAsync<NotFoundException>(() => UnlockAsync(fixture.Service(), initial));
        Assert.Empty(fixture.Published);
        Assert.True(fixture.UnitOfWork.RolledBack);
    }

    [Fact]
    public async Task Invalid_identities_are_rejected_before_any_transaction_or_publication()
    {
        foreach (var identity in new[] { "", "   ", new string('x', 129) })
        {
            var fixture = new AccountEventTestDoubles();
            await Assert.ThrowsAsync<ValidationException>(() => fixture.Service().RotateIdentityAsync(1234,
                new RotateIdentityRequest { UserWhoAmI = identity, ExpectedRowVersion = 11 }));
            Assert.False(fixture.UnitOfWork.Active);
            Assert.Empty(fixture.Published);
        }
    }

    [Fact]
    public async Task Missing_user_cannot_receive_an_unlock()
    {
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.UserExists = false;
        await Assert.ThrowsAsync<NotFoundException>(() => UnlockAsync(fixture.Service(), false));
        Assert.Empty(fixture.Published);
        Assert.True(fixture.UnitOfWork.RolledBack);
    }

    [Fact]
    public async Task Initial_unlock_publication_failure_does_not_roll_back_the_committed_registration()
    {
        var fixture = new AccountEventTestDoubles
        {
            PublishFailure = new IOException("Broker unavailable"), PublishFailureAt = 2
        };
        await Assert.ThrowsAsync<AccountEventPublicationException>(() => UnlockAsync(fixture.Service(), true));
        Assert.True(fixture.UnitOfWork.Committed);
        Assert.False(fixture.UnitOfWork.RolledBack);
        Assert.IsType<AccountCreatedEvent>(fixture.Published[0]);
        Assert.IsType<AchievementUnlockedEvent>(fixture.Published[1]);
    }

    [Fact]
    public async Task Committed_rotation_invalidates_source_and_dashboard_caches_even_when_publication_fails()
    {
        var fixture = new AccountEventTestDoubles { PublishFailure = new IOException("Broker unavailable") };
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddPlatformCaching();
        await using var provider = services.BuildServiceProvider();
        var states = provider.GetRequiredService<CacheGroupState>();
        var account = states.Capture("accounts");
        var dashboard = states.Capture("dashboard");
        var cached = new CachedAccountsService(fixture.Service(), provider.GetRequiredService<ICacheAside>(),
            provider.GetRequiredService<CacheInvalidator>());

        await Assert.ThrowsAsync<AccountEventPublicationException>(() => AccountEventTestDoubles.MutateAsync(cached, "identity"));
        Assert.True(fixture.UnitOfWork.Committed);
        Assert.False(fixture.UnitOfWork.RolledBack);
        Assert.True(account.Token.IsCancellationRequested);
        Assert.True(dashboard.Token.IsCancellationRequested);
    }

    private static Task UnlockAsync(IAccountsService service, bool initial) => initial
        ? service.CreateUserAsync(new CreateUserRequest
        {
            UserName = "requested-name", UserEmail = "requested@example.test", UserWhoAmI = "requested-identity",
            PasswordHash = "private-password", InitialAchievementId = 42
        })
        : service.GrantAchievementAsync(new GrantAchievementRequest(1234, 42));
}
