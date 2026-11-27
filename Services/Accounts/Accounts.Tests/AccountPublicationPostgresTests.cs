using Accounts.Bll;
using Accounts.Bll.Contracts;
using Accounts.Bll.Events;
using Accounts.Dal;
using Accounts.Domain.Exceptions;
using Accounts.Domain.Models;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Platform.Messaging;
using Platform.Messaging.Events;
using Xunit;

namespace Accounts.Tests;

public sealed class AccountsPostgresFactAttribute : FactAttribute
{
    public AccountsPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ACCOUNTS_TEST_CONNECTION")))
            Skip = "Set ACCOUNTS_TEST_CONNECTION to a disposable PostgreSQL database initialized with db/p1/schema.sql and procedures.sql.";
    }
}

public sealed class AccountPublicationPostgresTests
{
    [AccountsPostgresFact]
    public async Task Published_lifecycle_snapshots_match_persisted_versions_and_deleted_rows_remain_hidden()
    {
        await using var unitOfWork = new UnitOfWork(Environment.GetEnvironmentVariable("ACCOUNTS_TEST_CONNECTION")!);
        var publisher = new RecordingPublisher(unitOfWork);
        var service = CreateService(unitOfWork, publisher);
        long userId = 0;
        try
        {
            userId = await service.CreateUserAsync(Request());
            var created = Assert.IsType<AccountCreatedEvent>(Assert.Single(publisher.Events));
            AssertSnapshot(created.Account, (await unitOfWork.Users.GetByIdAsync(userId))!);
            Assert.Equal(1, created.Account.SourceVersion);
            await service.UpdateUserAsync(userId, new UpdateUserRequest
            {
                UserName = "updated_" + userId, UserEmail = userId + "@updated.example.test", ExpectedRowVersion = 1
            });
            var changed = Assert.IsType<AccountProfileChangedEvent>(publisher.Events[1]);
            AssertSnapshot(changed.Account, (await unitOfWork.Users.GetByIdAsync(userId))!);
            Assert.Equal(2, changed.Account.SourceVersion);

            await Assert.ThrowsAsync<BusinessConflictException>(() => service.UpdateUserAsync(userId, new UpdateUserRequest
            {
                UserName = "stale_" + userId, UserEmail = userId + "@stale.example.test", ExpectedRowVersion = 1
            }));
            Assert.Equal(2, publisher.Events.Count);
            await service.DeleteUserAsync(userId, 2);
            var deleted = Assert.IsType<AccountDeletedEvent>(publisher.Events[2]);
            Assert.Null(await unitOfWork.Users.GetByIdAsync(userId));
            AssertSnapshot(deleted.Account, (await unitOfWork.Users.GetIncludingDeletedAsync(userId))!);
            Assert.True(deleted.Account.IsDeleted);
            Assert.Equal(3, deleted.Account.SourceVersion);
        }
        finally { if (userId != 0) await DeleteTestUserAsync(unitOfWork, userId); }
    }

    [AccountsPostgresFact]
    public async Task Publication_failure_leaves_the_successful_source_write_committed()
    {
        await using var unitOfWork = new UnitOfWork(Environment.GetEnvironmentVariable("ACCOUNTS_TEST_CONNECTION")!);
        var publisher = new RecordingPublisher(unitOfWork);
        var service = CreateService(unitOfWork, publisher);
        var userId = await service.CreateUserAsync(Request());
        try
        {
            publisher.Fail = true;
            await Assert.ThrowsAsync<AccountEventPublicationException>(() => service.UpdateUserAsync(userId, new UpdateUserRequest
            {
                UserName = "committed_" + userId, UserEmail = userId + "@committed.example.test", ExpectedRowVersion = 1
            }));
            var persisted = await unitOfWork.Users.GetByIdAsync(userId);
            Assert.NotNull(persisted);
            Assert.Equal("committed_" + userId, persisted.UserName);
            Assert.Equal(2, persisted.RowVersion);
            Assert.Null(unitOfWork.Transaction);
        }
        finally { await DeleteTestUserAsync(unitOfWork, userId); }
    }

    private static AccountsService CreateService(IUnitOfWork unitOfWork, RecordingPublisher publisher) =>
        new(unitOfWork, new MapperConfiguration(options => options.AddProfile<AccountMappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper(), NullLogger<AccountsService>.Instance, publisher, publisher);

    [AccountsPostgresFact]
    public async Task Identity_and_unlock_events_use_source_versions_and_timestamps_and_duplicate_grants_publish_nothing()
    {
        await using var unitOfWork = new UnitOfWork(Environment.GetEnvironmentVariable("ACCOUNTS_TEST_CONNECTION")!);
        var publisher = new RecordingPublisher(unitOfWork);
        var service = CreateService(unitOfWork, publisher);
        var achievementId = await unitOfWork.Achievements.AddAsync(new Achievement
        {
            AchievementName = "lab92_" + Guid.NewGuid().ToString("N"), AchievementDescription = "Event test"
        });
        long userId = 0;
        try
        {
            var request = Request();
            userId = await service.CreateUserAsync(new CreateUserRequest
            {
                UserName = request.UserName, UserEmail = request.UserEmail, UserWhoAmI = request.UserWhoAmI,
                PasswordHash = request.PasswordHash, InitialAchievementId = achievementId
            });
            Assert.Equal(2, publisher.Events.Count);
            var unlock = Assert.IsType<AchievementUnlockedEvent>(publisher.Events[1]);
            Assert.Equal(achievementId, unlock.AchievementId);
            Assert.Equal(userId, unlock.UserId);
            await using (var command = new NpgsqlCommand(
                "SELECT unlocked_at FROM accounts.user_achievements WHERE user_id = @id AND achievement_id = @achievement", unitOfWork.Connection))
            {
                command.Parameters.AddWithValue("id", userId);
                command.Parameters.AddWithValue("achievement", achievementId);
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(reader.GetFieldValue<DateTimeOffset>(0), unlock.UnlockedAt);
            }
            var identity = "lab92-" + Guid.NewGuid().ToString("N");
            await service.RotateIdentityAsync(userId, new RotateIdentityRequest { UserWhoAmI = identity, ExpectedRowVersion = 1 });
            var rotated = Assert.IsType<AccountIdentityRotatedEvent>(publisher.Events[2]);
            Assert.Equal(identity, rotated.Account.UserWhoAmI);
            Assert.Equal(2, rotated.Account.SourceVersion);
            AssertSnapshot(rotated.Account, (await unitOfWork.Users.GetByIdAsync(userId))!);
            await Assert.ThrowsAsync<BusinessConflictException>(() => service.RotateIdentityAsync(userId,
                new RotateIdentityRequest { UserWhoAmI = "stale-" + userId, ExpectedRowVersion = 1 }));
            await Assert.ThrowsAsync<BusinessConflictException>(() =>
                service.GrantAchievementAsync(new GrantAchievementRequest(userId, achievementId)));
            Assert.Equal(3, publisher.Events.Count);
            Assert.Equal(2, (await unitOfWork.Users.GetByIdAsync(userId))!.RowVersion);
        }
        finally
        {
            if (userId != 0)
            {
                await using var command = new NpgsqlCommand("DELETE FROM accounts.user_achievements WHERE user_id = @id", unitOfWork.Connection);
                command.Parameters.AddWithValue("id", userId);
                await command.ExecuteNonQueryAsync();
                await DeleteTestUserAsync(unitOfWork, userId);
            }
            await using var cleanup = new NpgsqlCommand("DELETE FROM accounts.achievement WHERE achievement_id = @id", unitOfWork.Connection);
            cleanup.Parameters.AddWithValue("id", achievementId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static CreateUserRequest Request()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new CreateUserRequest
        {
            UserName = "lab91_" + suffix, UserEmail = suffix + "@example.test",
            PasswordHash = "private-test-hash", UserWhoAmI = "lab91-" + suffix
        };
    }

    private static void AssertSnapshot(AccountSnapshot snapshot, AccountUser row)
    {
        Assert.Equal(row.UserId, snapshot.UserId);
        Assert.Equal(row.UserName, snapshot.UserName);
        Assert.Equal(row.UserEmail, snapshot.UserEmail);
        Assert.Equal(row.UserWhoAmI, snapshot.UserWhoAmI);
        Assert.Equal(row.CreatedAt, snapshot.CreatedAt);
        Assert.Equal(row.UpdatedAt ?? row.CreatedAt, snapshot.SourceUpdatedAt);
        Assert.Equal(row.RowVersion, snapshot.SourceVersion);
        Assert.Equal(row.IsDeleted, snapshot.IsDeleted);
    }

    private static async Task DeleteTestUserAsync(IUnitOfWork unitOfWork, long userId)
    {
        await using var command = new NpgsqlCommand("DELETE FROM accounts.users WHERE user_id = @userId", unitOfWork.Connection);
        command.Parameters.AddWithValue("userId", userId);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class RecordingPublisher(IUnitOfWork unitOfWork) : IEventPublisher, IEventCorrelation
    {
        public string? Current => "postgres-lab91";
        public List<IntegrationEvent> Events { get; } = [];
        public bool Fail { get; set; }
        public Task PublishAsync(IntegrationEvent value, CancellationToken cancellationToken = default)
        {
            Assert.Null(unitOfWork.Transaction);
            Events.Add(value);
            return Fail ? Task.FromException(new IOException("Broker unavailable")) : Task.CompletedTask;
        }
    }
}
