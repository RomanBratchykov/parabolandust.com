using System.Text;
using Accounts.Bll.Caching;
using Accounts.Bll.Events;
using Accounts.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Platform.Caching;
using Platform.Messaging.Events;
using Xunit;

namespace Accounts.Tests;

public sealed class AccountPublicationTests
{
    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task Events_wait_for_commit_and_use_the_persisted_snapshot_without_authentication_fields(string operation)
    {
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.CommitGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var mutation = AccountEventTestDoubles.MutateAsync(fixture.Service(), operation);
        Assert.Empty(fixture.Published);
        fixture.UnitOfWork.CommitGate.SetResult();
        await mutation;

        Assert.True(fixture.PublicationSawCommit);
        Assert.True(fixture.UnitOfWork.SnapshotReadInsideTransaction);
        var value = Assert.Single(fixture.Published);
        var snapshot = value switch
        {
            AccountCreatedEvent created when operation == "create" => created.Account,
            AccountProfileChangedEvent changed when operation == "update" => changed.Account,
            AccountDeletedEvent deleted when operation == "delete" => deleted.Account,
            _ => throw new InvalidOperationException("Wrong lifecycle event")
        };
        Assert.Equal(1234, snapshot.UserId);
        Assert.Equal("persisted-name", snapshot.UserName);
        Assert.Equal("persisted@example.test", snapshot.UserEmail);
        Assert.Equal("persisted-identity", snapshot.UserWhoAmI);
        Assert.Equal(operation == "create" ? 1L : 12L, snapshot.SourceVersion);
        Assert.Equal(new DateTimeOffset(2026, operation == "create" ? 1 : 2, 1, 0, 0, 0, TimeSpan.Zero), snapshot.SourceUpdatedAt);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), snapshot.CreatedAt);
        Assert.Equal(operation == "delete", snapshot.IsDeleted);
        Assert.Equal(snapshot.SourceUpdatedAt, value.OccurredAt);
        Assert.Equal("request-lab91", value.CorrelationId);
        var json = Encoding.UTF8.GetString(EventJson.Serialize(value));
        Assert.DoesNotContain("private-", json);
        Assert.DoesNotContain("passwordHash", json);
        Assert.DoesNotContain("oauth", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("create", "write")]
    [InlineData("update", "write")]
    [InlineData("delete", "write")]
    [InlineData("create", "commit")]
    [InlineData("update", "commit")]
    [InlineData("delete", "commit")]
    [InlineData("create", "snapshot")]
    [InlineData("update", "snapshot")]
    [InlineData("delete", "snapshot")]
    public async Task Failed_writes_snapshot_reads_and_commits_do_not_publish(string operation, string failure)
    {
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.FailureAt = failure;
        await Assert.ThrowsAnyAsync<Exception>(() => AccountEventTestDoubles.MutateAsync(fixture.Service(), operation));

        Assert.Empty(fixture.Published);
        Assert.False(fixture.UnitOfWork.Committed);
        Assert.True(fixture.UnitOfWork.RolledBack);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task Repository_rejection_does_not_publish_or_commit(string operation)
    {
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.WriteAccepted = false;
        await Assert.ThrowsAsync<BusinessConflictException>(() => AccountEventTestDoubles.MutateAsync(fixture.Service(), operation));
        Assert.Empty(fixture.Published);
        Assert.False(fixture.UnitOfWork.Committed);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task Broker_failure_does_not_roll_back_committed_data_or_skip_source_cache_invalidation(string operation)
    {
        var fixture = new AccountEventTestDoubles { PublishFailure = new IOException("Broker unavailable") };
        await using var provider = CreateProvider();
        var states = provider.GetRequiredService<CacheGroupState>();
        var account = states.Capture("accounts");
        var dashboard = states.Capture("dashboard");
        var cached = new CachedAccountsService(fixture.Service(), provider.GetRequiredService<ICacheAside>(),
            provider.GetRequiredService<CacheInvalidator>());

        await Assert.ThrowsAsync<AccountEventPublicationException>(() => AccountEventTestDoubles.MutateAsync(cached, operation));
        Assert.True(fixture.UnitOfWork.Committed);
        Assert.False(fixture.UnitOfWork.RolledBack);
        Assert.True(account.Token.IsCancellationRequested);
        Assert.True(dashboard.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Caller_cancellation_after_commit_does_not_skip_publication()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new AccountEventTestDoubles();
        fixture.UnitOfWork.AfterCommit = cancellation.Cancel;
        await AccountEventTestDoubles.MutateAsync(fixture.Service(), "create", cancellation.Token);
        Assert.Single(fixture.Published);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddPlatformCaching();
        return services.BuildServiceProvider();
    }
}
