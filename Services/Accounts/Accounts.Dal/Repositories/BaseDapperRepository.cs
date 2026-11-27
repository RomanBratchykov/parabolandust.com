using Dapper;
using Npgsql;

namespace Accounts.Dal.Repositories;

public abstract class BaseDapperRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly NpgsqlConnection Connection;
    private readonly Func<NpgsqlTransaction?> _currentTransaction;
    private readonly string _selectByIdSql;
    private readonly string _selectAllSql;
    private readonly string _insertSql;
    private readonly Func<T, object> _insertParameters;
    private readonly string _updateSql;
    private readonly Func<T, object> _updateParameters;
    private readonly string _deleteSql;

    protected BaseDapperRepository(
        NpgsqlConnection connection,
        Func<NpgsqlTransaction?> currentTransaction,
        string tableName,
        string idColumn,
        string insertSql,
        Func<T, object> insertParameters,
        string updateSql,
        Func<T, object> updateParameters,
        string deleteSql,
        string? activeCondition = null)
    {
        Connection = connection;
        _currentTransaction = currentTransaction;
        var condition = string.IsNullOrWhiteSpace(activeCondition)
            ? string.Empty
            : $" AND ({activeCondition})";
        _selectByIdSql = $"SELECT * FROM {tableName} WHERE {idColumn} = @Id{condition}";
        _selectAllSql = string.IsNullOrWhiteSpace(activeCondition)
            ? $"SELECT * FROM {tableName}"
            : $"SELECT * FROM {tableName} WHERE {activeCondition}";
        _insertSql = insertSql;
        _insertParameters = insertParameters;
        _updateSql = updateSql;
        _updateParameters = updateParameters;
        _deleteSql = deleteSql;
    }

    protected NpgsqlTransaction? Transaction => _currentTransaction();

    public async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(
            _selectByIdSql, new { Id = id }, Transaction, cancellationToken: cancellationToken);
        return await Connection.QuerySingleOrDefaultAsync<T>(command);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(
            _selectAllSql, transaction: Transaction, cancellationToken: cancellationToken);
        var rows = await Connection.QueryAsync<T>(command);
        return rows.AsList();
    }

    public async Task<long> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(
            _insertSql, _insertParameters(entity), Transaction, cancellationToken: cancellationToken);
        return await Connection.QuerySingleAsync<long>(command);
    }

    public async Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(
            _updateSql, _updateParameters(entity), Transaction, cancellationToken: cancellationToken);
        return await Connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(
            _deleteSql, new { Id = id }, Transaction, cancellationToken: cancellationToken);
        return await Connection.ExecuteAsync(command) > 0;
    }

    protected async Task EnsureOpenAsync(CancellationToken cancellationToken)
    {
        if (Connection.State != System.Data.ConnectionState.Open)
        {
            await Connection.OpenAsync(cancellationToken);
        }
    }
}
