using System.Data;
using Accounts.Dal.Repositories;
using Npgsql;

namespace Accounts.Dal;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection _connection;
    private NpgsqlTransaction? _transaction;

    public UnitOfWork(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
        Users = new UserRepository(_connection, () => _transaction);
        GameLinking = new GameLinkingRepository(_connection, () => _transaction);
        Donations = new DonationRepository(_connection, () => _transaction);
        Achievements = new AchievementRepository(_connection, () => _transaction);
        UserAchievements = new UserAchievementRepository(_connection, () => _transaction);
    }

    public NpgsqlConnection Connection => _connection;
    public NpgsqlTransaction? Transaction => _transaction;
    public IUserRepository Users { get; }
    public IGameLinkingRepository GameLinking { get; }
    public IDonationRepository Donations { get; }
    public IAchievementRepository Achievements { get; }
    public IUserAchievementRepository UserAchievements { get; }

    public async Task BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active for this unit of work.");
        }

        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        _transaction = await _connection.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        var transaction = _transaction
            ?? throw new InvalidOperationException("There is no active transaction to commit.");

        await transaction.CommitAsync(cancellationToken);
        await transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }

        await _connection.DisposeAsync();
    }
}
