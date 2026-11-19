using Accounts.Domain.Models;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace Accounts.Dal.Repositories;

public sealed class UserAchievementRepository : IUserAchievementRepository
{
    private readonly NpgsqlConnection _connection;
    private readonly Func<NpgsqlTransaction?> _currentTransaction;

    public UserAchievementRepository(
        NpgsqlConnection connection,
        Func<NpgsqlTransaction?> currentTransaction)
    {
        _connection = connection;
        _currentTransaction = currentTransaction;
    }

    public async Task<IReadOnlyList<UserAchievement>> GetForUserAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        const string sql = """
            SELECT user_id, achievement_id, unlocked_at
            FROM accounts.user_achievements
            WHERE user_id = @UserId
            ORDER BY unlocked_at DESC
            """;
        var command = new CommandDefinition(
            sql, new { UserId = userId }, _currentTransaction(), cancellationToken: cancellationToken);
        var rows = await _connection.QueryAsync<UserAchievement>(command);
        return rows.AsList();
    }

    public async Task<bool> AddAsync(
        UserAchievement userAchievement,
        CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        const string sql = """
            INSERT INTO accounts.user_achievements (user_id, achievement_id)
            VALUES (@UserId, @AchievementId)
            ON CONFLICT (user_id, achievement_id) DO NOTHING
            RETURNING unlocked_at
            """;
        await using var command = new NpgsqlCommand(sql, _connection, _currentTransaction());
        command.Parameters.AddWithValue("UserId", NpgsqlDbType.Bigint, userAchievement.UserId);
        command.Parameters.AddWithValue("AchievementId", NpgsqlDbType.Bigint, userAchievement.AchievementId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return false;
        userAchievement.UnlockedAt = reader.GetFieldValue<DateTimeOffset>(0);
        return true;
    }

    public async Task<bool> DeleteAsync(
        long userId,
        long achievementId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync(cancellationToken);
        const string sql = """
            DELETE FROM accounts.user_achievements
            WHERE user_id = @UserId AND achievement_id = @AchievementId
            """;
        var command = new CommandDefinition(
            sql, new { UserId = userId, AchievementId = achievementId },
            _currentTransaction(), cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(command) > 0;
    }

    private async Task EnsureOpenAsync(CancellationToken cancellationToken)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }
    }
}
