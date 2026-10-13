using System.Data;
using Accounts.Dal.Repositories;
using Npgsql;

namespace Accounts.Dal;

public interface IUnitOfWork : IAsyncDisposable
{
    NpgsqlConnection Connection { get; }
    NpgsqlTransaction? Transaction { get; }
    IUserRepository Users { get; }
    IGameLinkingRepository GameLinking { get; }
    IDonationRepository Donations { get; }
    IAchievementRepository Achievements { get; }
    IUserAchievementRepository UserAchievements { get; }

    Task BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
