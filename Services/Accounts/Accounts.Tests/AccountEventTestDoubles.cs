using System.Data;
using Accounts.Bll;
using Accounts.Bll.Contracts;
using Accounts.Bll.Events;
using Accounts.Dal;
using Accounts.Dal.Repositories;
using Accounts.Domain.Models;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Platform.Messaging;
using Platform.Messaging.Events;

namespace Accounts.Tests;

internal sealed class AccountEventTestDoubles : IEventCorrelation, IEventPublisher
{
    public string? Current => "request-lab91";
    public StubUnitOfWork UnitOfWork { get; } = new();
    public List<IntegrationEvent> Published { get; } = [];
    public bool PublicationSawCommit { get; private set; }
    public Exception? PublishFailure { get; set; }
    public int PublishFailureAt { get; set; } = 1;

    public AccountsService Service() => new(UnitOfWork, new MapperConfiguration(
        options => options.AddProfile<AccountMappingProfile>(), NullLoggerFactory.Instance).CreateMapper(),
        NullLogger<AccountsService>.Instance, this, this);

    public Task PublishAsync(IntegrationEvent value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PublicationSawCommit = UnitOfWork.Committed && !UnitOfWork.Active;
        Published.Add(value);
        return PublishFailure is null || Published.Count != PublishFailureAt
            ? Task.CompletedTask : Task.FromException(PublishFailure);
    }

    public static Task MutateAsync(IAccountsService service, string operation, CancellationToken token = default) =>
        operation switch
        {
            "create" => service.CreateUserAsync(new CreateUserRequest
            {
                UserName = "requested-name", UserEmail = "requested@example.test",
                UserWhoAmI = "requested-identity", PasswordHash = "private-password"
            }, token),
            "update" => service.UpdateUserAsync(1234, new UpdateUserRequest
            {
                UserName = "requested-name", UserEmail = "requested@example.test", ExpectedRowVersion = 11
            }, token),
            "delete" => service.DeleteUserAsync(1234, 11, token),
            "identity" => service.RotateIdentityAsync(1234, new RotateIdentityRequest
            {
                UserWhoAmI = "requested-identity", ExpectedRowVersion = 11
            }, token),
            _ => throw new ArgumentException("Unknown test mutation.", nameof(operation))
        };
}

internal sealed class StubUnitOfWork : IUnitOfWork, IUserRepository
{
    public IUserRepository Users => this;
    public NpgsqlConnection Connection => throw new NotSupportedException();
    public NpgsqlTransaction? Transaction => throw new NotSupportedException();
    public IGameLinkingRepository GameLinking => throw new NotSupportedException();
    public IDonationRepository Donations => throw new NotSupportedException();
    public IAchievementRepository Achievements => new StubAchievements(this);
    public IUserAchievementRepository UserAchievements => new StubUnlocks(this);
    public bool Active { get; private set; }
    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }
    public bool SnapshotReadInsideTransaction { get; private set; }
    public string? FailureAt { get; set; }
    public bool WriteAccepted { get; set; } = true;
    public bool AchievementExists { get; set; } = true;
    public bool UserExists { get; set; } = true;
    public TaskCompletionSource? CommitGate { get; set; }
    public Action? AfterCommit { get; set; }
    private string operation = "update";

    public Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        Active = true;
        return Task.CompletedTask;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (CommitGate is not null) await CommitGate.Task;
        if (FailureAt == "commit") throw new InvalidOperationException("Commit failed");
        Committed = true;
        Active = false;
        AfterCommit?.Invoke();
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RolledBack = true;
        Active = false;
        return Task.CompletedTask;
    }

    public Task<AccountUser?> GetByIdAsync(long userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<AccountUser?>(UserExists ? Row() : null);

    public Task<AccountUser?> GetIncludingDeletedAsync(long userId, CancellationToken cancellationToken = default)
    {
        SnapshotReadInsideTransaction = Active && !Committed;
        return Task.FromResult<AccountUser?>(FailureAt == "snapshot" ? null : Row());
    }

    public Task<long> AddAsync(AccountUser user, CancellationToken cancellationToken = default)
    {
        Write("create");
        return Task.FromResult(1234L);
    }

    public Task<bool> UpdateProfileAsync(AccountUser user, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        Write("update");
        return Task.FromResult(WriteAccepted);
    }

    public Task<bool> SoftDeleteAsync(long userId, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        Write("delete");
        return Task.FromResult(WriteAccepted);
    }

    public Task<bool> RotateIdentityAsync(long userId, string identity, long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        Write("identity");
        return Task.FromResult(WriteAccepted);
    }

    private void Write(string mutation)
    {
        if (FailureAt == "write") throw new PostgresException("Write conflict", "ERROR", "ERROR", "P0001");
        operation = mutation;
    }

    private AccountUser Row() => new()
    {
        UserId = 1234, UserName = "persisted-name", UserEmail = "persisted@example.test",
        UserWhoAmI = operation == "identity" ? "rotated-persisted-identity" : "persisted-identity", PasswordHash = "private-password",
        OAuthProvider = "private-provider", OAuthId = "private-oauth-id",
        CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        UpdatedAt = operation == "create" ? null : new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
        RowVersion = operation == "create" ? 1 : 12, IsDeleted = operation == "delete"
    };

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class StubAchievements(StubUnitOfWork owner) : IAchievementRepository
    {
        public Task<Achievement?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(owner.AchievementExists ? new Achievement { AchievementId = id } : null);
        public Task<IReadOnlyList<Achievement>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> AddAsync(Achievement entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(Achievement entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubUnlocks(StubUnitOfWork owner) : IUserAchievementRepository
    {
        public Task<bool> AddAsync(UserAchievement value, CancellationToken cancellationToken = default)
        {
            if (owner.FailureAt is "write" or "unlock") throw new InvalidOperationException("Unlock write failed");
            value.UnlockedAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
            return Task.FromResult(owner.WriteAccepted);
        }
        public Task<IReadOnlyList<UserAchievement>> GetForUserAsync(long userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(long userId, long achievementId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
