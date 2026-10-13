using System.Data.Common;
using Npgsql;

namespace Accounts.Dal;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection _connection;

    public UnitOfWork(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
    }

    public DbConnection Connection => _connection;

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
